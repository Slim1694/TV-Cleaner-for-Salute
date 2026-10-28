using System.Collections.Generic;
using System.IO;
using TVCleaner.Core;

namespace TVCleaner.Tests
{
    public static class TvStatsTests
    {
        // Real output captured from the author's TV, joined the way TvStats.Command prints it.
        private const string Sample =
            "cpu  81243 12935 76509 1076327 4463 0 891 0 0 0\n" +
            "MemTotal:        1359764 kB\n" +
            "MemFree:          176888 kB\n" +
            "MemAvailable:     594836 kB\n" +
            "Filesystem            1K-blocks    Used Available Use% Mounted on\n" +
            "/dev/block/mmcblk0p32   3733256 1617500   2022184  45% /data\n";

        public static void TestParseSnapshot()
        {
            TvSnapshot s = TvStats.Parse(Sample);
            Assert.Equal(81243L + 12935 + 76509 + 1076327 + 4463 + 891, s.CpuTotal);
            Assert.Equal(1076327L + 4463, s.CpuIdle);
            Assert.Equal(1359764L, s.MemTotalKb);
            Assert.Equal(594836L, s.MemAvailableKb);
            Assert.Equal(3733256L, s.DataTotalKb);
            Assert.Equal(2022184L, s.DataFreeKb);
        }

        public static void TestCpuPercentBetweenSnapshots()
        {
            var a = new TvSnapshot { CpuTotal = 1000, CpuIdle = 800 };
            var b = new TvSnapshot { CpuTotal = 1200, CpuIdle = 850 };
            Assert.Equal(75, TvStats.CpuPercent(a, b)); // 150 busy of 200
            Assert.Equal(-1, TvStats.CpuPercent(null, b));
            Assert.Equal(-1, TvStats.CpuPercent(b, b));
        }

        public static void TestReadUsesOneCommand()
        {
            var s = new FakeShell();
            s.Responses[TvStats.Command] = Sample;
            TvSnapshot snap = TvStats.Read(s);
            Assert.Equal(1, s.Commands.Count);
            Assert.Equal(594836L, snap.MemAvailableKb);
        }

        public static void TestBrokenOutputGivesZeros()
        {
            TvSnapshot s = TvStats.Parse("garbage\n");
            Assert.Equal(0L, s.MemTotalKb);
            Assert.Equal(0L, s.CpuTotal);
        }
    }

    public static class CacheInfoTests
    {
        public static void TestCacheSizeFromDiskstats()
        {
            var s = new FakeShell();
            s.Responses[CacheInfo.DiskstatsCommand] = "Latency: 1ms\nApp Size: 1460662272\nApp Cache Size: 4653056\nSystem Size: 1\n";
            Assert.Equal(4653056L, CacheInfo.ReadCacheBytes(s));
        }

        public static void TestCacheSizeUnknown()
        {
            Assert.Equal(-1L, CacheInfo.ReadCacheBytes(new FakeShell()));
        }

        public static void TestClearReportsFreedSpace()
        {
            var s = new FakeShell();
            int dfCalls = 0;
            s.Handler = cmd =>
            {
                if (cmd != CacheInfo.DataFreeCommand) return null;
                dfCalls++;
                return dfCalls == 1
                    ? "Filesystem 1K-blocks Used Available Use% Mounted on\n/dev/x 3733256 1617500 2022184 45% /data\n"
                    : "Filesystem 1K-blocks Used Available Use% Mounted on\n/dev/x 3733256 1517500 2122184 41% /data\n";
            };
            long freed = CacheInfo.Clear(s);
            Assert.Equal(100000L * 1024, freed);
            Assert.True(s.Commands.Contains("pm trim-caches 999G"), "trim-caches sent");
        }
    }

    public static class ModelNameTests
    {
        public static void TestPretty()
        {
            Assert.Equal("SBER 55 CV9632-N55", ModelName.Pretty("SBER_55_CV9632-N55_ST5461D09-1_RTK88X2CU_TK2211-119.txt", "SaluteTV"));
            Assert.Equal("SaluteTV", ModelName.Pretty("", "SaluteTV"));
        }

        public static void TestNormalizeMatchesTestedModels()
        {
            Assert.Equal("SBER_55_CV9632-N55_ST5461D09-1_RTK88X2CU_TK2211-119",
                ModelName.Normalize("SBER_55_CV9632-N55_ST5461D09-1_RTK88X2CU_TK2211-119.txt"));
            Assert.True(ModelName.IsTested(new List<string> { "SBER_55_X" }, "SBER_55_X.txt"), "tested ignoring .txt");
            Assert.True(!ModelName.IsTested(new List<string> { "SBER_55_X" }, "SBER_65_Y.txt"), "other model");
        }
    }

    public static class ApkInstallerTests
    {
        private static string Fixture(string name)
        {
            return Path.GetFullPath(Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "..", "tests", "fixtures", name));
        }

        public static void TestInstallSendsWholeFile()
        {
            string apk = Fixture("label-reference.apk");
            var s = new FakeShell();
            byte[] sent = null;
            s.InputHandler = (cmd, input) => { sent = input; return "Success\n"; };
            RestoreResult r = ApkInstaller.Install(s, apk);
            Assert.True(r.Ok, r.Message);
            Assert.Equal("cmd package install -S " + new FileInfo(apk).Length, s.Commands[0]);
            Assert.Equal(new FileInfo(apk).Length, (long)sent.Length);
            Assert.Equal("Тест названия", r.Package); // shown name comes from the APK
        }

        public static void TestInstallFailureMessage()
        {
            var s = new FakeShell();
            s.InputHandler = (cmd, input) => "Failure [INSTALL_FAILED_OLDER_SDK]\n";
            RestoreResult r = ApkInstaller.Install(s, Fixture("label-literal.apk"));
            Assert.True(!r.Ok, "must fail");
            Assert.True(r.Message.Contains("INSTALL_FAILED_OLDER_SDK"), r.Message);
        }
    }
}
