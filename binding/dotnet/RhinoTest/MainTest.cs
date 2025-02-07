/*
    Copyright 2020-2021 Picovoice Inc.

    You may not use this file except in compliance with the license. A copy of the license is located in the "LICENSE"
    file accompanying this source.

    Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
    an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the
    specific language governing permissions and limitations under the License.
*/

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Pv;

namespace RhinoTest
{
    [TestClass]
    public class MainTest
    {
        private static string ACCESS_KEY;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            if (testContext.Properties.Contains("pvTestAccessKey"))
            {
                ACCESS_KEY = testContext.Properties["pvTestAccessKey"].ToString();
            }
        }

        [TestMethod]
        public void TestFrameLength()
        {
            using Rhino r = SetUpClass();
            Assert.IsTrue(r?.FrameLength > 0, "Specified frame length was not a valid number.");
        }

        [TestMethod]
        public void TestVersion()
        {
            using Rhino r = SetUpClass();
            Assert.IsFalse(string.IsNullOrWhiteSpace(r?.Version), "Rhino did not return a valid version number.");
        }

        [TestMethod]
        public void TestContextInfo()
        {
            using Rhino r = SetUpClass();
            Assert.IsFalse(string.IsNullOrWhiteSpace(r?.ContextInfo), "Rhino did not return any context information.");
        }

        [TestMethod]
        public void TestWithinContext()
        {
            using Rhino r = SetUpClass();
            int frameLen = r.FrameLength;

            string testAudioPath = Path.Combine(_relativeDir, "resources/audio_samples/test_within_context.wav");
            List<short> data = GetPcmFromFile(testAudioPath, r.SampleRate);

            bool isFinalized = false;
            int framecount = (int)Math.Floor((float)(data.Count / frameLen));
            var results = new List<int>();
            for (int i = 0; i < framecount; i++)
            {
                int start = i * r.FrameLength;
                int count = r.FrameLength;
                List<short> frame = data.GetRange(start, count);
                isFinalized = r.Process(frame.ToArray());
                if (isFinalized)
                {
                    break;
                }
            }
            Assert.IsTrue(isFinalized, "Failed to finalize.");

            Inference inference = r.GetInference();
            Assert.IsTrue(inference.IsUnderstood, "Couldn't understand.");
            Assert.AreEqual("orderBeverage", inference.Intent, "Incorrect intent.");

            Dictionary<string, string> expectedSlotValues = new Dictionary<string, string>()
            {
                {"size", "medium"},
                {"numberOfShots", "double shot"},
                {"beverage", "americano"},
            };
            Assert.IsTrue(inference.Slots.All((keyValuePair) =>
                                          expectedSlotValues.ContainsKey(keyValuePair.Key) &&
                                          expectedSlotValues[keyValuePair.Key] == keyValuePair.Value));
        }

        [TestMethod]
        public void TestOutOfContext()
        {
            using Rhino r = SetUpClass();
            int frameLen = r.FrameLength;

            string testAudioPath = Path.Combine(_relativeDir, "resources/audio_samples/test_out_of_context.wav");
            List<short> data = GetPcmFromFile(testAudioPath, r.SampleRate);

            bool isFinalized = false;
            int framecount = (int)Math.Floor((float)(data.Count / frameLen));
            var results = new List<int>();
            for (int i = 0; i < framecount; i++)
            {
                int start = i * r.FrameLength;
                int count = r.FrameLength;
                List<short> frame = data.GetRange(start, count);
                isFinalized = r.Process(frame.ToArray());
                if (isFinalized)
                {
                    break;
                }
            }
            Assert.IsTrue(isFinalized, "Failed to finalize.");

            Inference inference = r.GetInference();
            Assert.IsFalse(inference.IsUnderstood, "Shouldn't be able to understand.");
        }

        private List<short> GetPcmFromFile(string audioFilePath, int expectedSampleRate)
        {
            List<short> data = new List<short>();
            using (BinaryReader reader = new BinaryReader(File.Open(audioFilePath, FileMode.Open)))
            {
                reader.ReadBytes(24); // skip over part of the header
                Assert.AreEqual(reader.ReadInt32(), expectedSampleRate, "Specified sample rate did not match test file.");
                reader.ReadBytes(16); // skip over the rest of the header

                while (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    data.Add(reader.ReadInt16());
                }
            }

            return data;
        }

        private static string _relativeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private static Architecture _arch => RuntimeInformation.ProcessArchitecture;
        private static string _env => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "mac" :
                                                 RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" :
                                                 RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && _arch == Architecture.X64 ? "linux" :
                                                 RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
                                                    (_arch == Architecture.Arm || _arch == Architecture.Arm64) ? PvLinuxEnv() : "";

        private Rhino SetUpClass() => Rhino.Create(ACCESS_KEY, Path.Combine(_relativeDir, $"resources/contexts/{_env}/coffee_maker_{_env}.rhn"));

        public static string PvLinuxEnv()
        {
            string cpuInfo = File.ReadAllText("/proc/cpuinfo");
            string[] cpuPartList = cpuInfo.Split('\n').Where(x => x.Contains("CPU part")).ToArray();
            if (cpuPartList.Length == 0)
                throw new PlatformNotSupportedException($"Unsupported CPU.\n{cpuInfo}");

            string cpuPart = cpuPartList[0].Split(' ').Last().ToLower();

            switch (cpuPart)
            {
                case "0xc07":
                case "0xd03":
                case "0xd08": return "raspberry-pi";
                case "0xd07": return "jetson";
                case "0xc08": return "beaglebone";
                default:
                    throw new PlatformNotSupportedException($"This device (CPU part = {cpuPart}) is not supported by Picovoice.");
            }
        }
    }
}
