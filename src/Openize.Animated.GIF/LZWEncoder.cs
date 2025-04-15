// Copyright © 2025 Openize Pty Ltd
//
// Licensed under the Apache License, Version 2.0 (the “License”);
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an “AS IS” BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// C# port of LZWEncoder from Java GIF implementation
// Adapted from Kevin Weiner's version with origin in compress.c

namespace Openize.Animated.GIF
{
    public class LZWEncoder
    {
        const int EOF = -1;
        const int BITS = 12;
        const int HSIZE = 5003;

        int imgW, imgH;
        byte[] pixAry;
        int initCodeSize;
        int remaining;
        int curPixel;

        int n_bits;
        int maxbits = BITS;
        int maxcode;
        int maxmaxcode = 1 << BITS;

        int[] htab = new int[HSIZE];
        int[] codetab = new int[HSIZE];
        int hsize = HSIZE;
        int free_ent = 0;
        bool clear_flg = false;

        int g_init_bits;
        int ClearCode;
        int EOFCode;

        int cur_accum = 0;
        int cur_bits = 0;

        int[] masks = new int[] {
            0x0000, 0x0001, 0x0003, 0x0007, 0x000F,
            0x001F, 0x003F, 0x007F, 0x00FF, 0x01FF,
            0x03FF, 0x07FF, 0x0FFF, 0x1FFF, 0x3FFF,
            0x7FFF, 0xFFFF
        };

        int a_count;
        byte[] accum = new byte[256];

        public LZWEncoder(int width, int height, byte[] pixels, int colorDepth)
        {
            imgW = width;
            imgH = height;
            pixAry = pixels;
            initCodeSize = Math.Max(2, colorDepth);
        }

        public void Encode(Stream os)
        {
            os.WriteByte((byte)initCodeSize);
            remaining = imgW * imgH;
            curPixel = 0;
            Compress(initCodeSize + 1, os);
            os.WriteByte(0); // block terminator
        }

        void Compress(int init_bits, Stream outs)
        {
            int fcode, i, c, ent, disp, hsize_reg, hshift;

            g_init_bits = init_bits;
            clear_flg = false;
            n_bits = g_init_bits;
            maxcode = MAXCODE(n_bits);

            ClearCode = 1 << (init_bits - 1);
            EOFCode = ClearCode + 1;
            free_ent = ClearCode + 2;

            a_count = 0;
            ent = NextPixel();

            hshift = 0;
            for (fcode = hsize; fcode < 65536; fcode *= 2) ++hshift;
            hshift = 8 - hshift;
            hsize_reg = hsize;
            ClHash(hsize_reg);

            Output(ClearCode, outs);

            while ((c = NextPixel()) != EOF)
            {
                fcode = (c << maxbits) + ent;
                i = (c << hshift) ^ ent;

                if (htab[i] == fcode)
                {
                    ent = codetab[i];
                    continue;
                }
                else if (htab[i] >= 0)
                {
                    disp = hsize_reg - i;
                    if (i == 0) disp = 1;

                    bool found = false;
                    do
                    {
                        i -= disp;
                        if (i < 0) i += hsize_reg;

                        if (htab[i] == fcode)
                        {
                            ent = codetab[i];
                            found = true;
                            break;
                        }
                    } while (htab[i] >= 0);

                    if (found)
                        continue;
                }

                Output(ent, outs);
                ent = c;

                if (free_ent < maxmaxcode)
                {
                    codetab[i] = free_ent++;
                    htab[i] = fcode;
                }
                else
                {
                    ClBlock(outs);
                }
            }

            Output(ent, outs);
            Output(EOFCode, outs);
        }

        int NextPixel()
        {
            if (remaining == 0) return EOF;
            --remaining;
            return pixAry[curPixel++] & 0xff;
        }

        void Output(int code, Stream outs)
        {
            cur_accum &= masks[cur_bits];

            if (cur_bits > 0)
                cur_accum |= (code << cur_bits);
            else
                cur_accum = code;

            cur_bits += n_bits;

            while (cur_bits >= 8)
            {
                CharOut((byte)(cur_accum & 0xff), outs);
                cur_accum >>= 8;
                cur_bits -= 8;
            }

            if (free_ent > maxcode || clear_flg)
            {
                if (clear_flg)
                {
                    maxcode = MAXCODE(n_bits = g_init_bits);
                    clear_flg = false;
                }
                else
                {
                    ++n_bits;
                    maxcode = n_bits == maxbits ? maxmaxcode : MAXCODE(n_bits);
                }
            }

            if (code == EOFCode)
            {
                while (cur_bits > 0)
                {
                    CharOut((byte)(cur_accum & 0xff), outs);
                    cur_accum >>= 8;
                    cur_bits -= 8;
                }
                FlushChar(outs);
            }
        }

        void CharOut(byte c, Stream outs)
        {
            accum[a_count++] = c;
            if (a_count >= 254) FlushChar(outs);
        }

        void FlushChar(Stream outs)
        {
            if (a_count > 0)
            {
                outs.WriteByte((byte)a_count);
                outs.Write(accum, 0, a_count);
                a_count = 0;
            }
        }

        void ClBlock(Stream outs)
        {
            ClHash(hsize);
            free_ent = ClearCode + 2;
            clear_flg = true;
            Output(ClearCode, outs);
        }

        void ClHash(int hsize)
        {
            for (int i = 0; i < hsize; ++i)
                htab[i] = -1;
        }

        int MAXCODE(int n_bits)
        {
            return (1 << n_bits) - 1;
        }
    }
}
