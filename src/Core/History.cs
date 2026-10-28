using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace TVCleaner.Core
{
    public sealed class HistoryEntry
    {
        public const string ActionDisable = "disable";
        public const string ActionUninstall = "uninstall";
        public const string Pending = "pending";
        public const string Ok = "ok";
        public const string Failed = "failed";
        public const string RolledBack = "rolledback";

        public string Time { get; set; }
        public string Package { get; set; }
        public string Action { get; set; }
        public string Result { get; set; }
        public List<string> Backups { get; set; }
    }

    /// <summary>Original state of one TV plus every change the program made to it.</summary>
    public sealed class TvHistory
    {
        public string Serial { get; set; }
        public string Model { get; set; }
        public string SnapshotTime { get; set; }
        public List<string> OriginallyDisabled { get; set; }
        public List<string> OriginallyInstalled { get; set; }
        public List<HistoryEntry> Entries { get; set; }

        public bool HasActiveChanges
        {
            get { return Entries != null && Entries.Any(e => e.Result == HistoryEntry.Ok); }
        }
    }

    /// <summary>Stores history\SERIAL.json and backups\SERIAL\PACKAGE\*.apk under a root folder.</summary>
    public sealed class HistoryStore
    {
        private readonly string root;

        public HistoryStore(string root)
        {
            this.root = root;
        }

        public string BackupDir(string serial, string package)
        {
            return Path.Combine(root, "backups", serial, package);
        }

        public TvHistory Load(string serial)
        {
            string file = FilePath(serial);
            if (!File.Exists(file)) return null;
            TvHistory h = new JavaScriptSerializer().Deserialize<TvHistory>(File.ReadAllText(file));
            if (h.OriginallyDisabled == null) h.OriginallyDisabled = new List<string>();
            if (h.OriginallyInstalled == null) h.OriginallyInstalled = new List<string>();
            if (h.Entries == null) h.Entries = new List<HistoryEntry>();
            RelocateBackups(h);
            return h;
        }

        /// <summary>Writes to a temp file first, so a crash never leaves a half-written journal.</summary>
        public void Save(TvHistory history)
        {
            string file = FilePath(history.Serial);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string tmp = file + ".tmp";
            File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(history));
            if (File.Exists(file)) File.Delete(file);
            File.Move(tmp, file);
        }

        public TvHistory CreateSnapshot(TvInfo info, IEnumerable<TvPackage> packages)
        {
            List<TvPackage> list = packages.ToList();
            return new TvHistory
            {
                Serial = info.Serial,
                Model = string.IsNullOrEmpty(info.BuildInfo) ? info.Model : info.BuildInfo,
                SnapshotTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                OriginallyDisabled = list.Where(p => p.IsDisabled).Select(p => p.Name).ToList(),
                OriginallyInstalled = list.Select(p => p.Name).ToList(),
                Entries = new List<HistoryEntry>()
            };
        }

        /// <summary>
        /// Backup paths are absolute; if the data folder was moved or renamed, point them at the same file
        /// inside this store (backups\SERIAL\PACKAGE\file.apk).
        /// </summary>
        private void RelocateBackups(TvHistory h)
        {
            foreach (HistoryEntry e in h.Entries)
            {
                if (e.Backups == null) continue;
                for (int i = 0; i < e.Backups.Count; i++)
                {
                    if (File.Exists(e.Backups[i])) continue;
                    string here = Path.Combine(BackupDir(h.Serial, e.Package), Path.GetFileName(e.Backups[i]));
                    if (File.Exists(here)) e.Backups[i] = here;
                }
            }
        }

        private string FilePath(string serial)
        {
            return Path.Combine(root, "history", serial + ".json");
        }
    }
}
