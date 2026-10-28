using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TVCleaner.Adb;
using TVCleaner.Core;

namespace TVCleaner.UI
{
    public sealed class AppSession
    {
        public PackageDatabase Db;
        public HistoryStore Store;
        public AdbConnection Connection;
        public string Host;
        public TvInfo Info;
        public string Home;
        public List<TvPackage> Packages;
        public TvHistory History;

        /// <summary>Re-reads launcher, packages and journal from the TV (call on a background thread).</summary>
        public void Refresh()
        {
            var inspector = new TvInspector(Connection, Db);
            Home = inspector.ReadHomePackage();
            Packages = inspector.ReadPackages(Home);
            new AppLabelReader(Connection, Path.Combine(AppInfo.Root, "labels.json"))
                .Fill(Packages.Where(p => p.IsUserApp));
            History = Store.Load(Info.Serial);
        }

        /// <summary>Human-readable name: from the TV list, the database, or the saved APK copy; else the package.</summary>
        public string NameOf(string package)
        {
            TvPackage p = Packages == null ? null : Packages.FirstOrDefault(x => x.Name == package);
            if (p != null) return p.DisplayName;
            PackageInfo info = Db.Find(package);
            if (info != null) return info.Name;
            HistoryEntry e = History == null ? null : History.Entries.LastOrDefault(x => x.Package == package && x.Backups != null && x.Backups.Count > 0);
            if (e != null && File.Exists(e.Backups[0]))
            {
                try
                {
                    string label = ApkLabel.Read(new LocalFileReader(e.Backups[0]), "ru");
                    if (!string.IsNullOrEmpty(label)) return label;
                }
                catch (Exception)
                {
                    // fall back to the package name
                }
            }
            return package;
        }

        /// <summary>Creates the original-state snapshot on first use, then a runner bound to it.</summary>
        public ChangeRunner CreateRunner(bool dryRun, Action<string> log)
        {
            if (History == null)
            {
                History = Store.CreateSnapshot(Info, Packages);
                Store.Save(History);
            }
            return new ChangeRunner(Connection, Store, History, new HealthCheck(Connection, Home), dryRun, log);
        }
    }
}
