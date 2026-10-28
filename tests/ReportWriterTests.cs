using System.Collections.Generic;
using TVCleaner.Core;

namespace TVCleaner.Tests
{
    public static class ReportWriterTests
    {
        public static void TestReportHasPackagesButNoSerial()
        {
            var info = new TvInfo { Model = "SaluteTV", BuildInfo = "MODEL_A", Firmware = "F1", AndroidVersion = "9", Serial = "SECRET123" };
            var pkgs = new List<TvPackage> { new TvPackage { Name = "com.example.unknown" } };
            string json = ReportWriter.Build(info, pkgs);
            Assert.True(json.Contains("com.example.unknown"), "package listed");
            Assert.True(json.Contains("MODEL_A"), "model listed");
            Assert.True(!json.Contains("SECRET123"), "serial must not be in the report");
        }
    }
}
