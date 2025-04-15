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

using System.Drawing;
using Xunit;

namespace Openize.Animated.GIF.Tests
{
    public class TestAnimatedGifEncoder : TestBase
    {
        private MemoryStream outputStream;
        private AnimatedGifEncoder encoder;
        private Bitmap sonic1;
        private Bitmap sonic2;
        private Bitmap agif;
        private Bitmap bgif;

        public TestAnimatedGifEncoder() : base()
        {
            sonic1 = LoadImage("sonic1.png");
            sonic2 = LoadImage("sonic2.png");
            agif = LoadImage("a.gif");
            bgif = LoadImage("b.gif");

            outputStream = new MemoryStream();
            encoder = new AnimatedGifEncoder();
            encoder.Start(outputStream);
        }

        [Fact]
        public void BasicOutputTest()
        {
            EncodeSampleSonicFrames();
            AssertEncodedImageEquals("sonic-normal.gif");
        }

        [Fact]
        public void NullBackgroundWorks()
        {
            encoder.SetTransparent(Color.Empty); // No transparency
            EncodeSampleSonicFrames();
            AssertEncodedImageEquals("sonic-normal_null.gif");
        }

        [Fact]
        public void BackgroundColorOversizeImage()
        {
            encoder.SetSize(600, 600);
            encoder.SetBackground(Color.Red);
            EncodeSampleSonicFrames();
            AssertEncodedImageEquals("sonic-big-and-red.gif");
        }

        [Fact]
        public void TransparentColorBlue()
        {
            encoder.SetTransparent(Color.Blue);
            EncodeSampleSonicFrames();
            AssertEncodedImageEquals("sonic-blue-transparent.gif");
        }

        [Fact]
        public void TransparentColorExactMagenta()
        {
            encoder.SetTransparent(Color.Magenta, true);
            EncodeSampleExactFrames();
            Assert.False(encoder.IsColorUsed(Color.Magenta));
            encoder.Finish();
            AssertEncodedImageEquals("AandB.gif");
        }

        [Fact]
        public void TransparentColorCloseToBlue()
        {
            encoder.SetTransparent(Color.Blue);
            EncodeSampleExactFrames();
            encoder.Finish();
            AssertEncodedImageEquals("AandBCloseToBlue.gif");
        }

        [Fact]
        public void TransparentColorExactBlack()
        {
            encoder.SetTransparent(Color.Black, true);
            EncodeSampleExactFrames();
            Assert.True(encoder.IsColorUsed(Color.Black));
            encoder.Finish();
            AssertEncodedImageEquals("AandB-transparent.gif");
        }

        [Fact]
        public void BackgroundAndTransparentColor()
        {
            encoder.SetSize(600, 600);
            encoder.SetBackground(Color.Green);
            encoder.SetTransparent(Color.Blue);
            EncodeSampleSonicFrames();
            AssertEncodedImageEquals("sonic-green-bg-blue-transparent.gif");
        }

        private void EncodeSampleSonicFrames()
        {
            encoder.SetRepeat(0);
            encoder.SetDelay(400);
            encoder.AddFrame(sonic1);
            encoder.AddFrame(sonic2);
            encoder.Finish();
        }

        private void EncodeSampleExactFrames()
        {
            encoder.SetRepeat(0);
            encoder.SetDelay(1000);
            encoder.AddFrame(agif);
            encoder.AddFrame(bgif);
        }

        private void AssertEncodedImageEquals(string expectedFileName)
        {
            var actual = outputStream.ToArray();
            File.WriteAllBytes(Path.Combine(testResultsFolder, expectedFileName), actual);
            var expected = File.ReadAllBytes(GetGoldPath(expectedFileName));
            Assert.Equal(expected.Length, actual.Length);
            Assert.Equal(expected, actual);
        }
    }
}
