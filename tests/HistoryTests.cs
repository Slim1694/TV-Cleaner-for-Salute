using System;
using System.Collections.Generic;
using System.IO;
using TVCleaner.Core;

namespace TVCleaner.Tests
{
    public static class HistoryTests
    {
        public static string TempRoot()
        {
            return Path.Combine(Path.GetTempPath(), "tvcleaner-tests-" + Guid.NewGuid());
        }

        public static void TestBackupPathsFollowMovedDataFolder()
        {
            string root = TempRoot();
            try
            {
                var store = new HistoryStore(root);
                string backup = Path.Combine(store.BackupDir("SN1", "com.x"), "base.apk");
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.WriteAllText(backup, "apk");
                var h = new TvHistory
                {
                    Serial = "SN1",
                    OriginallyDisabled = new List<string>(),
                    OriginallyInstalled = new List<string>(),
                    Entries = new List<HistoryEntry>
                    {
                        new HistoryEntry { Package = "com.x", Action = HistoryEntry.ActionUninstall, Result = HistoryEntry.Ok,
                            Backups = new List<string> { @"C:\Users\someone\AppData\Roaming\OldName\backups\SN1\com.x\base.apk" } }
                    }
                };
                store.Save(h);
                Assert.Equal(backup, store.Load("SN1").Entries[0].Backups[0]);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        public static void TestLoadMissingReturnsNull()
        {
            Assert.Equal(null, new HistoryStore(TempRoot()).Load("nope"));
        }

        public static void TestSnapshotSaveLoadRoundTrip()
        {
            string root = TempRoot();
            try
            {
                var store = new HistoryStore(root);
                var info = new TvInfo { Serial = "SN1", Model = "SaluteTV", BuildInfo = "MODEL_A" };
                var pkgs = new List<TvPackage>
                {
                    new TvPackage { Name = "a", IsDisabled = true },
                    new TvPackage { Name = "b" }
                };
                TvHistory h = store.CreateSnapshot(info, pkgs);
                h.Entries.Add(new HistoryEntry { Time = "t", Package = "b", Action = HistoryEntry.ActionUninstall,
                    Result = HistoryEntry.Ok, Backups = new List<string> { "x.apk" } });
                store.Save(h);

                TvHistory loaded = store.Load("SN1");
                Assert.Equal("MODEL_A", loaded.Model);
                Assert.Equal("a", string.Join(",", loaded.OriginallyDisabled));
                Assert.Equal("a,b", string.Join(",", loaded.OriginallyInstalled));
                Assert.Equal("x.apk", loaded.Entries[0].Backups[0]);
                Assert.True(loaded.HasActiveChanges, "ok entry means active changes");
                Assert.True(store.BackupDir("SN1", "b").EndsWith(Path.Combine("backups", "SN1", "b")), "backup dir layout");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
