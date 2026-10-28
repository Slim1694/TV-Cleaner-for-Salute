using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TVCleaner.Core
{
    public enum RestoreKind { Disabled, Uninstalled }

    /// <summary>A package the user can bring back: enable it, or reinstall it from a saved APK copy.</summary>
    public sealed class RestoreItem
    {
        public string Package { get; set; }
        public RestoreKind Kind { get; set; }
        public bool ByProgram { get; set; }
        public List<string> Backups { get; set; }
    }

    public sealed class RestoreResult
    {
        public string Package { get; set; }
        public bool Ok { get; set; }
        public string Message { get; set; }
    }

    /// <summary>Builds the "disabled and removed" list: every disabled package plus program-removed ones with a copy.</summary>
    public static class RestoreList
    {
        public static List<RestoreItem> Build(IEnumerable<TvPackage> packages, TvHistory history)
        {
            List<TvPackage> onTv = packages.ToList();
            List<HistoryEntry> active = history == null || history.Entries == null
                ? new List<HistoryEntry>()
                : history.Entries.Where(e => e.Result == HistoryEntry.Ok).ToList();

            var items = onTv.Where(p => p.IsDisabled).Select(p => new RestoreItem
            {
                Package = p.Name,
                Kind = RestoreKind.Disabled,
                ByProgram = active.Any(e => e.Package == p.Name && e.Action == HistoryEntry.ActionDisable)
            }).ToList();

            var installed = new HashSet<string>(onTv.Select(p => p.Name));
            foreach (HistoryEntry e in active.Where(e => e.Action == HistoryEntry.ActionUninstall && !installed.Contains(e.Package)))
            {
                if (e.Backups == null || e.Backups.Count == 0 || !e.Backups.All(File.Exists)) continue;
                if (items.Any(i => i.Package == e.Package)) continue;
                items.Add(new RestoreItem { Package = e.Package, Kind = RestoreKind.Uninstalled, ByProgram = true, Backups = e.Backups });
            }
            return items;
        }
    }
}
