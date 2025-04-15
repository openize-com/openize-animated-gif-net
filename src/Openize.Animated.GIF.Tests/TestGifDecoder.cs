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

using System.Drawing.Imaging;
using Xunit;

namespace Openize.Animated.GIF.Tests
{
    public class TestGifDecoder : TestBase
    {
        public TestGifDecoder() : base() { }

        [Fact]
        public void DecodesGifWithDeferredClearCodes()
        {
            var inputName = "brucelee.gif";
            var outputName = "brucelee-frame.gif";

            var decoder = new GifDecoder();
            var path = Path.Combine(testDataFolder, inputName);
            var stream = File.OpenRead(path);

            decoder.Read(stream);

            var image = decoder.GetImage();
            byte[] actualBytes;
            using (var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Gif);
                actualBytes = ms.ToArray();
                File.WriteAllBytes(Path.Combine(testResultsFolder, outputName), actualBytes);
            }

            var expectedBytes = File.ReadAllBytes(GetGoldPath(outputName));

            Assert.Equal(expectedBytes, actualBytes);
        }

        [Fact]
        public void DecodesAndWritesAnimatedGif()
        {
            var inputName = "anim.gif";
            var outputName = "anim-frame.gif";

            var decoder = new GifDecoder();

            var path = Path.Combine(testDataFolder, inputName);
            var stream = File.OpenRead(path);

            decoder.Read(stream);

            var image = decoder.GetImage();
            byte[] actualBytes;
            using (var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Gif);
                actualBytes = ms.ToArray();
                var outputPath = Path.Combine(testResultsFolder, outputName);
                File.WriteAllBytes(outputPath, actualBytes);
            }

            var expectedBytes = File.ReadAllBytes(GetGoldPath(outputName));
            Assert.Equal(expectedBytes, actualBytes);
        }
    }
}
