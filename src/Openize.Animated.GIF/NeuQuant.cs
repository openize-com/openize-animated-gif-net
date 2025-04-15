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

// C# port of NeuQuant Neural-Net Quantization Algorithm by Anthony Dekker
// Java version by Kevin Weiner, 2000. This version adapted for C#.

namespace Openize.Animated.GIF
{
    public class NeuQuant
    {
        protected static readonly int netsize = 256;
        protected static readonly int prime1 = 499;
        protected static readonly int prime2 = 491;
        protected static readonly int prime3 = 487;
        protected static readonly int prime4 = 503;
        protected static readonly int minpicturebytes = 3 * prime4;

        protected static readonly int maxnetpos = netsize - 1;
        protected static readonly int netbiasshift = 4;
        protected static readonly int ncycles = 100;

        protected static readonly int intbiasshift = 16;
        protected static readonly int intbias = 1 << intbiasshift;
        protected static readonly int gammashift = 10;
        protected static readonly int gamma = 1 << gammashift;
        protected static readonly int betashift = 10;
        protected static readonly int beta = intbias >> betashift;
        protected static readonly int betagamma = intbias << (gammashift - betashift);

        protected static readonly int initrad = netsize >> 3;
        protected static readonly int radiusbiasshift = 6;
        protected static readonly int radiusbias = 1 << radiusbiasshift;
        protected static readonly int initradius = initrad * radiusbias;
        protected static readonly int radiusdec = 30;

        protected static readonly int alphabiasshift = 10;
        protected static readonly int initalpha = 1 << alphabiasshift;

        protected int alphadec;

        protected static readonly int radbiasshift = 8;
        protected static readonly int radbias = 1 << radbiasshift;
        protected static readonly int alpharadbshift = alphabiasshift + radbiasshift;
        protected static readonly int alpharadbias = 1 << alpharadbshift;

        protected byte[] thepicture;
        protected int lengthcount;
        protected int samplefac;

        protected int[][] network;
        protected int[] netindex = new int[256];
        protected int[] bias = new int[netsize];
        protected int[] freq = new int[netsize];
        protected int[] radpower = new int[initrad];

        public NeuQuant(byte[] thepic, int len, int sample)
        {
            thepicture = thepic;
            lengthcount = len;
            samplefac = sample;

            network = new int[netsize][];
            for (int i = 0; i < netsize; i++)
            {
                network[i] = new int[4];
                int[] p = network[i];
                p[0] = p[1] = p[2] = (i << (netbiasshift + 8)) / netsize;
                freq[i] = intbias / netsize;
                bias[i] = 0;
            }
        }

        public byte[] Process()
        {
            Learn();
            Unbiasnet();
            Inxbuild();
            return ColorMap();
        }

        public byte[] ColorMap()
        {
            byte[] map = new byte[3 * netsize];
            int[] index = new int[netsize];
            for (int i = 0; i < netsize; i++) index[network[i][3]] = i;
            int k = 0;
            for (int i = 0; i < netsize; i++)
            {
                int j = index[i];
                map[k++] = (byte)(network[j][0]);
                map[k++] = (byte)(network[j][1]);
                map[k++] = (byte)(network[j][2]);
            }
            return map;
        }

        public void Inxbuild()
        {
            int previouscol = 0, startpos = 0;
            for (int i = 0; i < netsize; i++)
            {
                int[] p = network[i];
                int smallpos = i;
                int smallval = p[1];
                for (int j = i + 1; j < netsize; j++)
                {
                    int[] q = network[j];
                    if (q[1] < smallval) { smallpos = j; smallval = q[1]; }
                }
                int[] q2 = network[smallpos];
                if (i != smallpos)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        int t = p[k];
                        p[k] = q2[k];
                        q2[k] = t;
                    }
                }
                if (smallval != previouscol)
                {
                    netindex[previouscol] = (startpos + i) >> 1;
                    for (int j = previouscol + 1; j < smallval; j++)
                        netindex[j] = i;
                    previouscol = smallval;
                    startpos = i;
                }
            }
            netindex[previouscol] = (startpos + maxnetpos) >> 1;
            for (int j = previouscol + 1; j < 256; j++) netindex[j] = maxnetpos;
        }

        public void Learn()
        {
            int step;
            if (lengthcount < minpicturebytes) samplefac = 1;
            alphadec = 30 + ((samplefac - 1) / 3);
            int samplepixels = lengthcount / (3 * samplefac);
            int delta = samplepixels / ncycles;
            int alpha = initalpha;
            int radius = initradius;
            int rad = radius >> radiusbiasshift;
            if (rad <= 1) rad = 0;
            for (int i = 0; i < rad; i++)
                radpower[i] = alpha * ((rad * rad - i * i) * radbias) / (rad * rad);

            if (lengthcount < minpicturebytes) step = 3;
            else if ((lengthcount % prime1) != 0) step = 3 * prime1;
            else if ((lengthcount % prime2) != 0) step = 3 * prime2;
            else if ((lengthcount % prime3) != 0) step = 3 * prime3;
            else step = 3 * prime4;

            int pix = 0;
            for (int i = 0; i < samplepixels; i++)
            {
                int b = (thepicture[pix + 0] & 0xff) << netbiasshift;
                int g = (thepicture[pix + 1] & 0xff) << netbiasshift;
                int r = (thepicture[pix + 2] & 0xff) << netbiasshift;
                int j = Contest(b, g, r);
                Altersingle(alpha, j, b, g, r);
                if (rad != 0) Alterneigh(rad, j, b, g, r);
                pix += step;
                if (pix >= lengthcount) pix -= lengthcount;
                if (delta == 0) delta = 1;
                if (i % delta == 0)
                {
                    alpha -= alpha / alphadec;
                    radius -= radius / radiusdec;
                    rad = radius >> radiusbiasshift;
                    if (rad <= 1) rad = 0;
                    for (j = 0; j < rad; j++)
                        radpower[j] = alpha * ((rad * rad - j * j) * radbias) / (rad * rad);
                }
            }
        }

        public void Unbiasnet()
        {
            for (int i = 0; i < netsize; i++)
            {
                network[i][0] >>= netbiasshift;
                network[i][1] >>= netbiasshift;
                network[i][2] >>= netbiasshift;
                network[i][3] = i;
            }
        }

        public int Map(int b, int g, int r)
        {
            int bestd = int.MaxValue;
            int best = -1;
            int i = netindex[g];
            int j = i - 1;
            while ((i < netsize) || (j >= 0))
            {
                if (i < netsize)
                {
                    int[] p = network[i];
                    int dist = Math.Abs(p[1] - g);
                    if (dist >= bestd) break;
                    dist += Math.Abs(p[0] - b);
                    dist += Math.Abs(p[2] - r);
                    if (dist < bestd) { bestd = dist; best = p[3]; }
                    i++;
                }
                if (j >= 0)
                {
                    int[] p = network[j];
                    int dist = Math.Abs(g - p[1]);
                    if (dist >= bestd) break;
                    dist += Math.Abs(p[0] - b);
                    dist += Math.Abs(p[2] - r);
                    if (dist < bestd) { bestd = dist; best = p[3]; }
                    j--;
                }
            }
            return best;
        }

        protected void Altersingle(int alpha, int i, int b, int g, int r)
        {
            int[] n = network[i];
            n[0] -= (alpha * (n[0] - b)) / initalpha;
            n[1] -= (alpha * (n[1] - g)) / initalpha;
            n[2] -= (alpha * (n[2] - r)) / initalpha;
        }

        protected void Alterneigh(int rad, int i, int b, int g, int r)
        {
            int lo = Math.Max(i - rad, -1);
            int hi = Math.Min(i + rad, netsize);
            int j = i + 1;
            int k = i - 1;
            int m = 1;
            while (j < hi || k > lo)
            {
                int a = radpower[m++];
                if (j < hi)
                {
                    int[] p = network[j++];
                    p[0] -= (a * (p[0] - b)) / alpharadbias;
                    p[1] -= (a * (p[1] - g)) / alpharadbias;
                    p[2] -= (a * (p[2] - r)) / alpharadbias;
                }
                if (k > lo)
                {
                    int[] p = network[k--];
                    p[0] -= (a * (p[0] - b)) / alpharadbias;
                    p[1] -= (a * (p[1] - g)) / alpharadbias;
                    p[2] -= (a * (p[2] - r)) / alpharadbias;
                }
            }
        }

        protected int Contest(int b, int g, int r)
        {
            int bestd = int.MaxValue, bestbiasd = int.MaxValue;
            int bestpos = -1, bestbiaspos = -1;
            for (int i = 0; i < netsize; i++)
            {
                int[] n = network[i];
                int dist = Math.Abs(n[0] - b) + Math.Abs(n[1] - g) + Math.Abs(n[2] - r);
                if (dist < bestd) { bestd = dist; bestpos = i; }
                int biasdist = dist - (bias[i] >> (intbiasshift - netbiasshift));
                if (biasdist < bestbiasd) { bestbiasd = biasdist; bestbiaspos = i; }
                int betafreq = freq[i] >> betashift;
                freq[i] -= betafreq;
                bias[i] += betafreq << gammashift;
            }
            freq[bestpos] += beta;
            bias[bestpos] -= betagamma;
            return bestbiaspos;
        }
    }
}