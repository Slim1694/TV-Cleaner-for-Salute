using System.Collections.Generic;
using System.IO;
using System.Linq;
using TVCleaner.Core;

namespace TVCleaner.Tests
{
    public static class ChangeRunnerTests
    {
        private sealed class Setup
        {
            public FakeTv Tv;
            public HistoryStore Store;
            public TvHistory History;
            public string Root;
            public PackageDatabase Db;

            public ChangeRunner Runner(bool dryRun)
            {
                return new ChangeRunner(Tv.Shell, Store, History, new HealthCheck(Tv.Shell, "com.spocky.projengmenu"), dryRun, null);
            }

            public PlanItem Item(string name, bool isSystem, ChangeAction action)
            {
                return new PlanItem { Package = new TvPackage { Name = name, IsSystem = isSystem, Info = Db.Find(name) }, Action = action };
            }

            public void Cleanup()
            {
                if (Directory.Exists(Root)) Directory.Delete(Root, true);
            }
        }

        private static Setup Create()
        {
            var s = new Setup { Tv = new FakeTv(), Root = HistoryTests.TempRoot(), Db = PackageDatabase.Parse(TestData.DbJson) };
            s.Tv.Installed.UnionWith(new[] { "ru.sberdevices.survey", "ru.sberdevices.music", "com.android.printspooler" });
            s.Store = new HistoryStore(s.Root);
            s.History = s.Store.CreateSnapshot(new TvInfo { Serial = "SN1", Model = "M" },
                s.Tv.Installed.Select(n => new TvPackage { Name = n }));
            return s;
        }

        public static void TestDisable()
        {
            Setup s = Create();
            try
            {
                StepResult r = s.Runner(false).Apply(s.Item("com.android.printspooler", true, ChangeAction.Disable));
                Assert.Equal(StepOutcome.Ok, r.Outcome);
                Assert.True(s.Tv.Disabled.Contains("com.android.printspooler"), "disabled on TV");
                Assert.Equal(HistoryEntry.Ok, s.Store.Load("SN1").Entries.Single().Result);
            }
            finally { s.Cleanup(); }
        }

        public static void TestUninstallKeepsBackup()
        {
            Setup s = Create();
            try
            {
                StepResult r = s.Runner(false).Apply(s.Item("ru.sberdevices.music", false, ChangeAction.Uninstall));
                Assert.Equal(StepOutcome.Ok, r.Outcome);
                Assert.True(!s.Tv.Installed.Contains("ru.sberdevices.music"), "removed from TV");
                string backup = Path.Combine(s.Store.BackupDir("SN1", "ru.sberdevices.music"), "base.apk");
                Assert.Equal("ru.sberdevices.music", File.ReadAllText(backup));
                Assert.Equal(backup, s.History.Entries.Single().Backups.Single());
            }
            finally { s.Cleanup(); }
        }

        public static void TestBadBackupFallsBackToDisable()
        {
            Setup s = Create();
            try
            {
                s.Tv.StatSizeOffset = 5; // size on TV differs from copied bytes
                s.Runner(false).Apply(s.Item("ru.sberdevices.music", false, ChangeAction.Uninstall));
                Assert.True(s.Tv.Installed.Contains("ru.sberdevices.music"), "not uninstalled without a good copy");
                Assert.True(s.Tv.Disabled.Contains("ru.sberdevices.music"), "disabled instead");
                Assert.Equal(HistoryEntry.ActionDisable, s.History.Entries.Single().Action);
            }
            finally { s.Cleanup(); }
        }

        public static void TestRollbackAllRestoresEverything()
        {
            Setup s = Create();
            try
            {
                ChangeRunner runner = s.Runner(false);
                runner.Apply(s.Item("com.android.printspooler", true, ChangeAction.Disable));
                runner.Apply(s.Item("ru.sberdevices.music", false, ChangeAction.Uninstall));
                List<RestoreResult> results = runner.RollbackAll();
                Assert.Equal(2, results.Count);
                Assert.True(results.All(r => r.Ok), "all restored");
                Assert.True(!s.Tv.Disabled.Contains("com.android.printspooler"), "re-enabled");
                Assert.True(s.Tv.Installed.Contains("ru.sberdevices.music"), "reinstalled from backup");
                Assert.True(s.History.Entries.All(e => e.Result == HistoryEntry.RolledBack), "entries marked");
                Assert.True(!s.Store.Load("SN1").HasActiveChanges, "nothing left to roll back");
            }
            finally { s.Cleanup(); }
        }

        public static void TestRollbackCollapsesRepeatedEntries()
        {
            Setup s = Create();
            try
            {
                ChangeRunner runner = s.Runner(false);
                runner.Apply(s.Item("com.android.printspooler", true, ChangeAction.Disable));
                s.Tv.Disabled.Remove("com.android.printspooler"); // re-enabled outside the program
                runner.Apply(s.Item("com.android.printspooler", true, ChangeAction.Disable));
                List<RestoreResult> results = runner.RollbackAll();
                Assert.Equal(1, results.Count);
                Assert.Equal("com.android.printspooler", results[0].Package);
                Assert.Equal(1, s.Tv.Shell.Commands.Count(c => c == "pm enable com.android.printspooler"));
                Assert.True(s.History.Entries.All(e => e.Result == HistoryEntry.RolledBack), "both entries marked");
            }
            finally { s.Cleanup(); }
        }

        public static void TestRestoreSelectedItems()
        {
            Setup s = Create();
            try
            {
                ChangeRunner runner = s.Runner(false);
                runner.Apply(s.Item("ru.sberdevices.music", false, ChangeAction.Uninstall));
                s.Tv.Disabled.Add("com.example.old"); // disabled before the program
                List<RestoreItem> items = RestoreList.Build(new List<TvPackage>
                {
                    new TvPackage { Name = "com.example.old", IsDisabled = true },
                    new TvPackage { Name = "ru.sberdevices.survey" }
                }, s.History);
                List<RestoreResult> results = runner.Restore(items);
                Assert.Equal(2, results.Count(r => r.Ok));
                Assert.True(!s.Tv.Disabled.Contains("com.example.old"), "enabled");
                Assert.True(s.Tv.Installed.Contains("ru.sberdevices.music"), "reinstalled");
                Assert.Equal(HistoryEntry.RolledBack, s.History.Entries.Single().Result);
            }
            finally { s.Cleanup(); }
        }

        public static void TestRestoreListContents()
        {
            Setup s = Create();
            try
            {
                ChangeRunner runner = s.Runner(false);
                runner.Apply(s.Item("com.android.printspooler", true, ChangeAction.Disable));
                runner.Apply(s.Item("ru.sberdevices.music", false, ChangeAction.Uninstall));
                var onTv = new List<TvPackage>
                {
                    new TvPackage { Name = "com.android.printspooler", IsDisabled = true },
                    new TvPackage { Name = "com.example.old", IsDisabled = true },
                    new TvPackage { Name = "ru.sberdevices.survey" }
                };
                Dictionary<string, RestoreItem> items = RestoreList.Build(onTv, s.History).ToDictionary(i => i.Package);
                Assert.Equal(3, items.Count);
                Assert.True(items["com.android.printspooler"].ByProgram, "disabled by the program");
                Assert.True(!items["com.example.old"].ByProgram, "disabled before the program");
                Assert.Equal(RestoreKind.Uninstalled, items["ru.sberdevices.music"].Kind);

                File.Delete(items["ru.sberdevices.music"].Backups[0]);
                Assert.True(RestoreList.Build(onTv, s.History).All(i => i.Package != "ru.sberdevices.music"), "no backup, nothing to restore");
            }
            finally { s.Cleanup(); }
        }

        public static void TestUnhealthyStepCanBeRolledBack()
        {
            Setup s = Create();
            try
            {
                s.Tv.BreakNetworkOnNextDisable = true;
                ChangeRunner runner = s.Runner(false);
                StepResult r = runner.Apply(s.Item("ru.sberdevices.survey", false, ChangeAction.Disable));
                Assert.Equal(StepOutcome.Unhealthy, r.Outcome);
                Assert.True(r.Message.Contains("сеть"), r.Message);
                runner.RollbackLast();
                Assert.True(!s.Tv.Disabled.Contains("ru.sberdevices.survey"), "last step rolled back");
            }
            finally { s.Cleanup(); }
        }

        public static void TestDryRunChangesNothing()
        {
            Setup s = Create();
            try
            {
                StepResult r = s.Runner(true).Apply(s.Item("ru.sberdevices.music", false, ChangeAction.Uninstall));
                Assert.Equal(StepOutcome.Ok, r.Outcome);
                Assert.True(s.Tv.Shell.Commands.All(c => !c.StartsWith("pm uninstall") && !c.StartsWith("pm disable")), "no changing commands");
                Assert.Equal(0, s.History.Entries.Count);
            }
            finally { s.Cleanup(); }
        }

        public static void TestLockedPackageRefused()
        {
            Setup s = Create();
            try
            {
                var item = new PlanItem { Package = new TvPackage { Name = "com.android.shell", Locked = true }, Action = ChangeAction.Disable };
                Assert.Equal(StepOutcome.Failed, s.Runner(false).Apply(item).Outcome);
                Assert.True(s.Tv.Shell.Commands.Count == 0, "nothing sent to TV");
            }
            finally { s.Cleanup(); }
        }
    }
}
