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

// This is a partial port of the Java AnimatedGifEncoder to C#.

using System.Drawing;
using System.Drawing.Imaging;

namespace Openize.Animated.GIF
{
    public class AnimatedGifEncoder
    {
        protected int width;
        protected int height;
        protected Color? transparent = null;
        protected bool transparentExactMatch = false;
        protected Color? background = null;
        protected int transIndex;
        protected int repeat = -1;
        protected int delay = 0;
        protected bool started = false;
        protected Stream? outStream;
        protected Bitmap? image;
        protected byte[]? pixels;
        protected byte[]? indexedPixels;
        protected int colorDepth;
        protected byte[]? colorTab;
        protected bool[] usedEntry = new bool[256];
        protected int palSize = 7;
        protected int dispose = -1;
        protected bool closeStream = false;
        protected bool firstFrame = true;
        protected bool sizeSet = false;
        protected int sample = 10;

        public void SetDelay(int ms) => delay = (int)Math.Round(ms / 10.0);

        public void SetDispose(int code)
        {
            if (code >= 0) dispose = code;
        }

        public void SetRepeat(int iter)
        {
            if (iter >= 0) repeat = iter;
        }

        public void SetTransparent(Color c, bool exactMatch = false)
        {
            transparent = c;
            transparentExactMatch = exactMatch;
        }

        public void SetBackground(Color c) => background = c;

        public bool AddFrame(Bitmap im)
        {
            if (im == null || !started) return false;

            bool ok = true;
            try
            {
                if (!sizeSet) SetSize(im.Width, im.Height);
                image = im;
                GetImagePixels();
                AnalyzePixels();

                if (firstFrame)
                {
                    WriteLSD();
                    WritePalette();
                    if (repeat >= 0) WriteNetscapeExt();
                }

                WriteGraphicCtrlExt();
                WriteImageDesc();
                if (!firstFrame) WritePalette();
                WritePixels();
                firstFrame = false;
            }
            catch
            {
                ok = false;
            }

            return ok;
        }

        public bool Finish()
        {
            if (!started) return false;
            bool ok = true;
            started = false;

            try
            {
                outStream?.WriteByte(0x3b); // GIF trailer
                outStream?.Flush();
                if (closeStream) outStream?.Close();
            }
            catch
            {
                ok = false;
            }

            transIndex = 0;
            outStream = null;
            image = null;
            pixels = null;
            indexedPixels = null;
            colorTab = null;
            closeStream = false;
            firstFrame = true;

            return ok;
        }

        public void SetFrameRate(float fps)
        {
            if (fps != 0f) delay = (int)Math.Round(100f / fps);
        }

        public void SetQuality(int quality)
        {
            sample = Math.Max(1, quality);
        }

        public void SetSize(int w, int h)
        {
            if (started && !firstFrame) return;
            width = w <= 0 ? 320 : w;
            height = h <= 0 ? 240 : h;
            sizeSet = true;
        }

        public bool Start(Stream os)
        {
            if (os == null) return false;
            bool ok = true;
            closeStream = false;
            outStream = os;
            try
            {
                WriteString("GIF89a");
            }
            catch
            {
                ok = false;
            }
            return started = ok;
        }

        public bool Start(string file)
        {
            bool ok = true;
            try
            {
                outStream = new BufferedStream(new FileStream(file, FileMode.Create));
                ok = Start(outStream);
                closeStream = true;
            }
            catch
            {
                ok = false;
            }
            return started = ok;
        }

        public bool IsStarted() => started;

        protected void AnalyzePixels()
        {
            int len = pixels.Length;
            int nPix = len / 3;
            indexedPixels = new byte[nPix];

            NeuQuant nq = new NeuQuant(pixels, len, sample);
            colorTab = nq.Process();

            for (int i = 0; i < colorTab.Length; i += 3)
            {
                byte temp = colorTab[i];
                colorTab[i] = colorTab[i + 2];
                colorTab[i + 2] = temp;
                usedEntry[i / 3] = false;
            }

            int k = 0;
            for (int i = 0; i < nPix; i++)
            {
                int index = nq.Map(pixels[k++] & 0xff, pixels[k++] & 0xff, pixels[k++] & 0xff);
                usedEntry[index] = true;
                indexedPixels[i] = (byte)index;
            }

            pixels = null;
            colorDepth = 8;
            palSize = 7;

            if (transparent.HasValue)
            {
                transIndex = transparentExactMatch ? FindExact(transparent.Value) : FindClosest(transparent.Value);
            }
        }

        protected int FindClosest(Color c)
        {
            if (colorTab == null) return -1;
            int r = c.R, g = c.G, b = c.B;
            int minpos = 0;
            int dmin = int.MaxValue;

            for (int i = 0; i < colorTab.Length;)
            {
                int dr = r - (colorTab[i++] & 0xff);
                int dg = g - (colorTab[i++] & 0xff);
                int db = b - (colorTab[i] & 0xff);
                int d = dr * dr + dg * dg + db * db;
                int index = i / 3;
                if (usedEntry[index] && d < dmin)
                {
                    dmin = d;
                    minpos = index;
                }
                i++;
            }
            return minpos;
        }

        public bool IsColorUsed(Color c)
        {
            return FindExact(c) != -1;
        }

        protected int FindExact(Color c)
        {
            if (colorTab == null) return -1;
            int r = c.R, g = c.G, b = c.B;
            int len = colorTab.Length / 3;
            for (int index = 0; index < len; index++)
            {
                int i = index * 3;
                if (usedEntry[index] &&
                    r == (colorTab[i] & 0xff) &&
                    g == (colorTab[i + 1] & 0xff) &&
                    b == (colorTab[i + 2] & 0xff))
                {
                    return index;
                }
            }
            return -1;
        }

        protected void GetImagePixels()
        {
            int w = image.Width;
            int h = image.Height;

            if (w != width || h != height || image.PixelFormat != PixelFormat.Format24bppRgb)
            {
                Bitmap temp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(temp))
                {
                    g.Clear(background ?? Color.White);
                    g.DrawImage(image, 0, 0);
                }
                image.Dispose();
                image = temp;
            }

            BitmapData data = image.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            int stride = data.Stride;
            pixels = new byte[height * width * 3];

            int bytes = Math.Abs(data.Stride) * data.Height;
            byte[] raw = new byte[bytes];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, raw, 0, bytes);

            pixels = new byte[width * height * 3];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int srcIdx = y * stride + x * 3;
                    int dstIdx = (y * width + x) * 3;
                    pixels[dstIdx] = raw[srcIdx];     // B
                    pixels[dstIdx + 1] = raw[srcIdx + 1]; // G
                    pixels[dstIdx + 2] = raw[srcIdx + 2]; // R
                }
            }
            image.UnlockBits(data);
        }

        protected void WriteGraphicCtrlExt()
        {
            outStream.WriteByte(0x21);
            outStream.WriteByte(0xf9);
            outStream.WriteByte(4);

            int transp = transparent.HasValue ? 1 : 0;
            int disp = transparent.HasValue ? 2 : 0;
            if (dispose >= 0) disp = dispose & 7;
            disp <<= 2;

            outStream.WriteByte((byte)(disp | transp));
            WriteShort(delay);
            outStream.WriteByte((byte)transIndex);
            outStream.WriteByte(0);
        }

        protected void WriteImageDesc()
        {
            outStream.WriteByte(0x2c);
            WriteShort(0);
            WriteShort(0);
            WriteShort(width);
            WriteShort(height);
            outStream.WriteByte(firstFrame ? (byte)0 : (byte)(0x80 | palSize));
        }

        protected void WriteLSD()
        {
            WriteShort(width);
            WriteShort(height);
            outStream.WriteByte((byte)(0x80 | 0x70 | palSize));
            outStream.WriteByte(0);
            outStream.WriteByte(0);
        }

        protected void WriteNetscapeExt()
        {
            outStream.WriteByte(0x21);
            outStream.WriteByte(0xff);
            outStream.WriteByte(11);
            WriteString("NETSCAPE2.0");
            outStream.WriteByte(3);
            outStream.WriteByte(1);
            WriteShort(repeat);
            outStream.WriteByte(0);
        }

        protected void WritePalette()
        {
            outStream.Write(colorTab, 0, colorTab.Length);
            for (int i = colorTab.Length; i < 768; i++)
            {
                outStream.WriteByte(0);
            }
        }

        protected void WritePixels()
        {
            new LZWEncoder(width, height, indexedPixels, colorDepth).Encode(outStream);
        }

        protected void WriteShort(int value)
        {
            outStream.WriteByte((byte)(value & 0xff));
            outStream.WriteByte((byte)((value >> 8) & 0xff));
        }

        protected void WriteString(string s)
        {
            foreach (char c in s)
            {
                outStream.WriteByte((byte)c);
            }
        }
    }
}
