using System.Collections.Generic;
using System.Linq;
using TVCleaner.Core;

namespace TVCleaner.Tests
{
    public static class TvInspectorTests
    {
        private static FakeShell Tv()
        {
            var s = new FakeShell();
            s.Responses["getprop ro.product.model"] = "SaluteTV\n";
            s.Responses["getprop ro.build.info"] = "MODEL_A\n";
            s.Responses["getprop ro.build.version.incremental"] = "TVOS-1\n";
            s.Responses["getprop ro.build.version.release"] = "9\n";
            s.Responses["getprop ro.boot.serialno"] = "00ab:CD/12\n";
            s.Responses["pm list packages"] = "package:ru.sberdevices.survey\npackage:com.android.printspooler\npackage:ru.sberdevices.music\n" +
                "package:ru.sberdevices.starlauncher\npackage:com.android.shell\npackage:mediatek.factorymenu.ui\npackage:com.example.unknown\n" +
                "package:com.vendor.mystery\npackage:com.example.keyboard\n";
            s.Responses["pm list packages -s"] = "package:com.android.printspooler\npackage:ru.sberdevices.starlauncher\npackage:com.android.shell\n" +
                "package:mediatek.factorymenu.ui\npackage:com.vendor.mystery\n";
            s.Responses[TvInspector.KeyboardCommand] = "com.example.keyboard/.Ime\n";
            s.Responses["pm list packages -d"] = "package:com.android.printspooler\n";
            s.Responses[TvInspector.HomeCommand] = "priority=0 preferredOrder=0\nru.sberdevices.starlauncher/.MainActivity\n";
            return s;
        }

        private static Dictionary<string, TvPackage> Read(FakeShell s)
        {
            var insp = new TvInspector(s, PackageDatabase.Parse(TestData.DbJson));
            return insp.ReadPackages(insp.ReadHomePackage()).ToDictionary(p => p.Name);
        }

        public static void TestReadInfo()
        {
            TvInfo info = new TvInspector(Tv(), PackageDatabase.Parse(TestData.DbJson)).ReadInfo();
            Assert.Equal("SaluteTV", info.Model);
            Assert.Equal("MODEL_A", info.BuildInfo);
            Assert.Equal("00ab_CD_12", info.Serial);
        }

        public static void TestParseHome()
        {
            Assert.Equal("com.spocky.projengmenu", TvInspector.ParseHome("x\ncom.spocky.projengmenu/.ui.home.MainActivity\n"));
            Assert.Equal(null, TvInspector.ParseHome("No activity found\n"));
        }

        public static void TestPackageFlags()
        {
            Dictionary<string, TvPackage> p = Read(Tv());
            Assert.Equal(9, p.Count);
            Assert.True(p["com.android.printspooler"].IsSystem && p["com.android.printspooler"].IsDisabled, "printspooler system+disabled");
            Assert.True(!p["ru.sberdevices.survey"].IsSystem, "survey is a user app");
            Assert.Equal(false, p["ru.sberdevices.survey"].Locked);
        }

        public static void TestLocking()
        {
            Dictionary<string, TvPackage> p = Read(Tv());
            Assert.True(p["com.android.shell"].Locked, "blocklist wins over packages.json");
            Assert.True(p["ru.sberdevices.starlauncher"].Locked, "current home is locked");
            Assert.True(p["com.vendor.mystery"].Locked, "unknown system package is locked");
            Assert.True(p["mediatek.factorymenu.ui"].Locked, "system category is locked");
            Assert.True(p["com.vendor.mystery"].LockReason.Contains("неизвестн"), p["com.vendor.mystery"].LockReason);
        }

        public static void TestUserAppIsSelectableNotLocked()
        {
            Dictionary<string, TvPackage> p = Read(Tv());
            Assert.True(p["com.example.unknown"].IsUserApp, "non-system package outside the database is a user app");
            Assert.Equal(false, p["com.example.unknown"].Locked);
            Assert.True(!p["ru.sberdevices.survey"].IsUserApp, "known package is not in the user group");
        }

        public static void TestCurrentKeyboardIsLocked()
        {
            Dictionary<string, TvPackage> p = Read(Tv());
            Assert.True(p["com.example.keyboard"].Locked, "current keyboard is locked");
            Assert.True(p["com.example.keyboard"].LockReason.Contains("клавиатур"), p["com.example.keyboard"].LockReason);
        }
    }
}
