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

// C# port of GifDecoder class (Kevin Weiner, FM Software)
// This version mimics the original Java implementation
// and uses System.Drawing for Bitmap and Color handling.

using System.Drawing;
using System.Drawing.Imaging;
using System.Net;

namespace Openize.Animated.GIF
{
    public class GifDecoder
    {
        public const int STATUS_OK = 0;
        public const int STATUS_FORMAT_ERROR = 1;
        public const int STATUS_OPEN_ERROR = 2;

        protected BufferedStream inStream;
        protected int status;

        protected int width;
        protected int height;
        protected bool gctFlag;
        protected int gctSize;
        protected int loopCount = 1;

        protected int[] gct;
        protected int[] lct;
        protected int[] act;

        protected int bgIndex;
        protected int bgColor;
        protected int lastBgColor;
        protected int pixelAspect;

        protected bool lctFlag;
        protected bool interlace;
        protected int lctSize;

        protected int ix, iy, iw, ih;
        protected Rectangle lastRect;
        protected Bitmap image;
        protected Bitmap lastImage;

        protected byte[] block = new byte[256];
        protected int blockSize = 0;

        protected int dispose = 0;
        protected int lastDispose = 0;
        protected bool transparency = false;
        protected int delay = 0;
        protected int transIndex;

        protected const int MaxStackSize = 4096;

        protected short[] prefix;
        protected byte[] suffix;
        protected byte[] pixelStack;
        protected byte[] pixels;

        protected List<GifFrame> frames = new List<GifFrame>();
        protected int frameCount;

        protected class GifFrame
        {
            public GifFrame(Bitmap im, int del)
            {
                Image = im;
                Delay = del;
            }
            public Bitmap Image;
            public int Delay;
        }

        public int GetDelay(int n) =>
            (n >= 0 && n < frameCount) ? frames[n].Delay : -1;

        public int GetFrameCount() => frameCount;

        public Bitmap GetImage() => GetFrame(0);

        public int GetLoopCount() => loopCount;

        public Bitmap GetFrame(int n) =>
            (n >= 0 && n < frameCount) ? frames[n].Image : null;

        public Size GetFrameSize() => new Size(width, height);

        public int Read(string name)
        {
            status = STATUS_OK;
            try
            {
                name = name.Trim().ToLower();
                if (name.Contains("file:") || name.Contains(":/"))
                {
                    inStream = new BufferedStream(WebRequest.Create(name).GetResponse().GetResponseStream());
                }
                else
                {
                    inStream = new BufferedStream(new FileStream(name, FileMode.Open, FileAccess.Read));
                }
                status = Read(inStream);
            }
            catch
            {
                status = STATUS_OPEN_ERROR;
            }
            return status;
        }

        public int Read(Stream input)
        {
            Init();
            if (input != null)
            {
                inStream = input as BufferedStream ?? new BufferedStream(input);
                ReadHeader();
                if (!Err())
                {
                    ReadContents();
                    if (frameCount < 0) status = STATUS_FORMAT_ERROR;
                }
            }
            else
            {
                status = STATUS_OPEN_ERROR;
            }
            inStream?.Close();
            return status;
        }

        protected void Init()
        {
            status = STATUS_OK;
            frameCount = 0;
            frames = new List<GifFrame>();
            gct = null;
            lct = null;
        }

        protected void SetPixels()
        {
            var data = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int stride = data.Stride;
            byte[] dest = new byte[stride * height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, dest, 0, dest.Length);

            // Handle disposal method
            if (lastDispose > 0)
            {
                if (lastDispose == 3)
                {
                    int n = frameCount - 2;
                    lastImage = n > 0 ? GetFrame(n - 1) : null;
                }

                if (lastImage != null)
                {
                    var lastData = lastImage.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    byte[] prev = new byte[stride * height];
                    System.Runtime.InteropServices.Marshal.Copy(lastData.Scan0, prev, 0, prev.Length);
                    lastImage.UnlockBits(lastData);
                    Array.Copy(prev, 0, dest, 0, prev.Length);

                    if (lastDispose == 2)
                    {
                        using (Graphics g = Graphics.FromImage(image))
                        {
                            Color c = transparency ? Color.FromArgb(0, 0, 0, 0) : Color.FromArgb(lastBgColor);
                            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                            using (Brush brush = new SolidBrush(c))
                            {
                                g.FillRectangle(brush, lastRect);
                            }
                        }
                    }
                }
            }

            int pass = 1, inc = 8, iline = 0;
            for (int i = 0; i < ih; i++)
            {
                int line = i;
                if (interlace)
                {
                    if (iline >= ih)
                    {
                        pass++;
                        switch (pass)
                        {
                            case 2: iline = 4; break;
                            case 3: iline = 2; inc = 4; break;
                            case 4: iline = 1; inc = 2; break;
                        }
                    }
                    line = iline;
                    iline += inc;
                }

                line += iy;
                if (line < height)
                {
                    int k = line * stride;
                    int dx = k + ix * 4;
                    int dlim = dx + iw * 4;
                    if ((k + width * 4) < dlim)
                        dlim = k + width * 4;
                    int sx = i * iw;
                    while (dx < dlim)
                    {
                        int index = pixels[sx++] & 0xff;
                        int c = act[index];
                        if (c != 0)
                        {
                            dest[dx + 0] = (byte)(c & 0xff);         // B
                            dest[dx + 1] = (byte)((c >> 8) & 0xff);  // G
                            dest[dx + 2] = (byte)((c >> 16) & 0xff); // R
                            dest[dx + 3] = (byte)((c >> 24) & 0xff); // A
                        }
                        dx += 4;
                    }
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(dest, 0, data.Scan0, dest.Length);
            image.UnlockBits(data);
        }

        protected void DecodeImageData()
        {
            const int NullCode = -1;
            int npix = iw * ih;
            int available, clear, code_mask, code_size, end_of_information;
            int in_code, old_code, bits, code, count, i;
            int datum, data_size, first, top, bi, pi;

            if (pixels == null || pixels.Length < npix)
                pixels = new byte[npix];
            if (prefix == null) prefix = new short[MaxStackSize];
            if (suffix == null) suffix = new byte[MaxStackSize];
            if (pixelStack == null) pixelStack = new byte[MaxStackSize + 1];

            data_size = ReadByte();
            clear = 1 << data_size;
            end_of_information = clear + 1;
            available = clear + 2;
            old_code = NullCode;
            code_size = data_size + 1;
            code_mask = (1 << code_size) - 1;

            for (code = 0; code < clear; code++)
            {
                prefix[code] = 0;
                suffix[code] = (byte)code;
            }

            datum = bits = count = first = top = pi = bi = 0;

            for (i = 0; i < npix;)
            {
                if (top == 0)
                {
                    if (bits < code_size)
                    {
                        if (count == 0)
                        {
                            count = ReadBlock();
                            if (count <= 0) break;
                            bi = 0;
                        }
                        datum += (block[bi] & 0xff) << bits;
                        bits += 8;
                        bi++;
                        count--;
                        continue;
                    }

                    code = datum & code_mask;
                    datum >>= code_size;
                    bits -= code_size;

                    if (code > available || code == end_of_information) break;
                    if (code == clear)
                    {
                        code_size = data_size + 1;
                        code_mask = (1 << code_size) - 1;
                        available = clear + 2;
                        old_code = NullCode;
                        continue;
                    }
                    if (old_code == NullCode)
                    {
                        pixelStack[top++] = suffix[code];
                        old_code = code;
                        first = code;
                        continue;
                    }
                    in_code = code;
                    if (code == available)
                    {
                        pixelStack[top++] = (byte)first;
                        code = old_code;
                    }
                    while (code > clear)
                    {
                        pixelStack[top++] = suffix[code];
                        code = prefix[code];
                    }
                    first = suffix[code] & 0xff;
                    pixelStack[top++] = (byte)first;

                    if (available < MaxStackSize)
                    {
                        prefix[available] = (short)old_code;
                        suffix[available] = (byte)first;
                        available++;
                        if ((available & code_mask) == 0 && available < MaxStackSize)
                        {
                            code_size++;
                            code_mask = (1 << code_size) - 1;
                        }
                    }
                    old_code = in_code;
                }

                top--;
                pixels[pi++] = pixelStack[top];
                i++;
            }

            for (i = pi; i < npix; i++)
            {
                pixels[i] = 0;
            }
        }


        protected bool Err() => status != STATUS_OK;


        protected int ReadByte()
        {
            try
            {
                return inStream.ReadByte();
            }
            catch
            {
                status = STATUS_FORMAT_ERROR;
                return 0;
            }
        }

        protected int ReadBlock()
        {
            blockSize = ReadByte();
            int n = 0;
            if (blockSize > 0)
            {
                try
                {
                    while (n < blockSize)
                    {
                        int count = inStream.Read(block, n, blockSize - n);
                        if (count == 0) break;
                        n += count;
                    }
                }
                catch { }

                if (n < blockSize)
                    status = STATUS_FORMAT_ERROR;
            }
            return n;
        }

        protected int[] ReadColorTable(int ncolors)
        {
            int nbytes = 3 * ncolors;
            int[] tab = null;
            byte[] c = new byte[nbytes];
            try
            {
                int n = inStream.Read(c, 0, c.Length);
                if (n < nbytes)
                {
                    status = STATUS_FORMAT_ERROR;
                }
                else
                {
                    tab = new int[256];
                    int i = 0, j = 0;
                    while (i < ncolors)
                    {
                        int r = c[j++] & 0xff;
                        int g = c[j++] & 0xff;
                        int b = c[j++] & 0xff;
                        tab[i++] = unchecked((int)0xff000000 | (r << 16) | (g << 8) | b);
                    }
                }
            }
            catch
            {
                status = STATUS_FORMAT_ERROR;
            }
            return tab;
        }

        protected void ReadContents()
        {
            bool done = false;
            while (!done && !Err())
            {
                int code = ReadByte();
                switch (code)
                {
                    case 0x2C:
                        ReadImage();
                        break;
                    case 0x21:
                        code = ReadByte();
                        if (code == 0xf9)
                        {
                            ReadGraphicControlExt();
                        }
                        else if (code == 0xff)
                        {
                            ReadBlock();
                            string app = System.Text.Encoding.ASCII.GetString(block, 0, 11);
                            if (app == "NETSCAPE2.0")
                                ReadNetscapeExt();
                            else
                                Skip();
                        }
                        else
                        {
                            Skip();
                        }
                        break;
                    case 0x3b:
                        done = true;
                        break;
                    case 0x00:
                        break;
                    default:
                        status = STATUS_FORMAT_ERROR;
                        break;
                }
            }
        }

        protected void ReadGraphicControlExt()
        {
            ReadByte();
            int packed = ReadByte();
            dispose = (packed & 0x1c) >> 2;
            if (dispose == 0) dispose = 1;
            transparency = (packed & 1) != 0;
            delay = ReadShort() * 10;
            transIndex = ReadByte();
            ReadByte();
        }

        protected void ReadHeader()
        {
            string id = "";
            for (int i = 0; i < 6; i++)
                id += (char)ReadByte();
            if (!id.StartsWith("GIF"))
            {
                status = STATUS_FORMAT_ERROR;
                return;
            }
            ReadLSD();
            if (gctFlag && !Err())
            {
                gct = ReadColorTable(gctSize);
                bgColor = gct[bgIndex];
            }
        }

        protected void ReadImage()
        {
            ix = ReadShort();
            iy = ReadShort();
            iw = ReadShort();
            ih = ReadShort();

            int packed = ReadByte();
            lctFlag = (packed & 0x80) != 0;
            interlace = (packed & 0x40) != 0;
            lctSize = 2 << (packed & 7);

            act = lctFlag ? ReadColorTable(lctSize) : gct;
            if (bgIndex == transIndex) bgColor = 0;

            int save = 0;
            if (transparency)
            {
                save = act[transIndex];
                act[transIndex] = 0;
            }

            if (act == null)
            {
                status = STATUS_FORMAT_ERROR;
                return;
            }

            if (Err()) return;
            DecodeImageData();
            Skip();
            if (Err()) return;

            frameCount++;
            image = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            SetPixels();
            frames.Add(new GifFrame(image, delay));

            if (transparency)
                act[transIndex] = save;

            ResetFrame();
        }

        protected void ReadLSD()
        {
            width = ReadShort();
            height = ReadShort();
            int packed = ReadByte();
            gctFlag = (packed & 0x80) != 0;
            gctSize = 2 << (packed & 7);
            bgIndex = ReadByte();
            pixelAspect = ReadByte();
        }

        protected void ReadNetscapeExt()
        {
            do
            {
                ReadBlock();
                if (block[0] == 1)
                {
                    int b1 = block[1] & 0xff;
                    int b2 = block[2] & 0xff;
                    loopCount = (b2 << 8) | b1;
                }
            } while (blockSize > 0 && !Err());
        }

        protected int ReadShort()
        {
            return ReadByte() | (ReadByte() << 8);
        }

        protected void ResetFrame()
        {
            lastDispose = dispose;
            lastRect = new Rectangle(ix, iy, iw, ih);
            lastImage = image;
            lastBgColor = bgColor;
            dispose = 0;
            transparency = false;
            delay = 0;
            lct = null;
        }

        protected void Skip()
        {
            do
            {
                ReadBlock();
            } while (blockSize > 0 && !Err());
        }
    }
}
