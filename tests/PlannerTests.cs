using System.Collections.Generic;
using System.Linq;
using TVCleaner.Core;

namespace TVCleaner.Tests
{
    public static class PlannerTests
    {
        private static List<TvPackage> Packages()
        {
            PackageDatabase db = PackageDatabase.Parse(TestData.DbJson);
            return new List<TvPackage>
            {
                new TvPackage { Name = "ru.sberdevices.survey", IsSystem = false, Info = db.Find("ru.sberdevices.survey") },
                new TvPackage { Name = "com.android.printspooler", IsSystem = true, Info = db.Find("com.android.printspooler") },
                new TvPackage { Name = "ru.sberdevices.music", IsSystem = false, Info = db.Find("ru.sberdevices.music") },
                new TvPackage { Name = "ru.sberdevices.starlauncher", IsSystem = true, Info = db.Find("ru.sberdevices.starlauncher"), Locked = true },
                new TvPackage { Name = "com.example.unknown", Locked = true }
            };
        }

        private static List<string> Names(List<PlanItem> plan)
        {
            return plan.Select(i => i.Package.Name).OrderBy(n => n).ToList();
        }

        public static void TestRecommendedTakesAdsAndJunk()
        {
            List<PlanItem> plan = Planner.Build(Packages(), CleanMode.Recommended, null, false);
            Assert.Equal("com.android.printspooler,ru.sberdevices.survey", string.Join(",", Names(plan)));
            Assert.True(plan.All(i => i.Action == ChangeAction.Disable), "without uninstall flag everything is disabled");
        }

        public static void TestMaximumAddsOptionalButNotLocked()
        {
            List<string> names = Names(Planner.Build(Packages(), CleanMode.Maximum, null, false));
            Assert.Equal("com.android.printspooler,ru.sberdevices.music,ru.sberdevices.survey", string.Join(",", names));
        }

        public static void TestManualNeverTakesLocked()
        {
            var chosen = new HashSet<string> { "ru.sberdevices.music", "com.example.unknown", "ru.sberdevices.starlauncher" };
            Assert.Equal("ru.sberdevices.music", string.Join(",", Names(Planner.Build(Packages(), CleanMode.Manual, chosen, false))));
        }

        public static void TestUninstallOnlyForUserAppsAllowedByDb()
        {
            List<PlanItem> plan = Planner.Build(Packages(), CleanMode.Maximum, null, true);
            Assert.Equal(ChangeAction.Uninstall, plan.Single(i => i.Package.Name == "ru.sberdevices.survey").Action);
            Assert.Equal(ChangeAction.Uninstall, plan.Single(i => i.Package.Name == "ru.sberdevices.music").Action);
            Assert.Equal(ChangeAction.Disable, plan.Single(i => i.Package.Name == "com.android.printspooler").Action);
        }

        public static void TestUserAppsOnlyManual()
        {
            List<TvPackage> pkgs = Packages();
            pkgs.Add(new TvPackage { Name = "com.user.app", IsSystem = false });
            Assert.True(Planner.Build(pkgs, CleanMode.Maximum, null, true).All(i => i.Package.Name != "com.user.app"), "never in presets");
            var chosen = new HashSet<string> { "com.user.app" };
            Assert.Equal(ChangeAction.Disable, Planner.Build(pkgs, CleanMode.Manual, chosen, false).Single().Action);
            Assert.Equal(ChangeAction.Uninstall, Planner.Build(pkgs, CleanMode.Manual, chosen, true).Single().Action);
        }

        public static void TestAlreadyDisabledSkippedUnlessUninstall()
        {
            List<TvPackage> pkgs = Packages();
            pkgs[0].IsDisabled = true; // survey
            pkgs[1].IsDisabled = true; // printspooler
            Assert.Equal(0, Planner.Build(pkgs, CleanMode.Recommended, null, false).Count);
            List<PlanItem> withUninstall = Planner.Build(pkgs, CleanMode.Recommended, null, true);
            Assert.Equal("ru.sberdevices.survey", string.Join(",", Names(withUninstall)));
        }
    }
}
