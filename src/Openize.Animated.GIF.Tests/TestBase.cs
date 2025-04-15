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
using System.Reflection;

namespace Openize.Animated.GIF.Tests
{
    public class TestBase
    {
        public string testDataFolder;
        public string testResultsFolder;

        public TestBase()
        {
            string fullPath = Assembly.GetExecutingAssembly().Location;
            string srcFolder = Path.DirectorySeparatorChar + "src" + Path.DirectorySeparatorChar;
            int srcIndex = fullPath.IndexOf(srcFolder, StringComparison.OrdinalIgnoreCase);

            if (srcIndex != -1)
            {
                var solutionFolder = fullPath.Substring(0, srcIndex);
                testDataFolder = Path.Combine(solutionFolder, "testdata");
                testResultsFolder = Path.Combine(solutionFolder, "testresults");
            }
            else
            {
                throw new Exception("`\\src\\` folder not found in path.");
            }
        }

        protected Bitmap LoadImage(string name)
        {
            string path = Path.Combine(testDataFolder, name);
            var gif = Image.FromFile(path);
            return new Bitmap(gif);
        }

        protected string GetGoldPath(string fileName)
        {
            return Path.Combine(testDataFolder, "gold", fileName);
        }
    }
}
