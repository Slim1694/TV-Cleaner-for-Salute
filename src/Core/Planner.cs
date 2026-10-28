using System.Collections.Generic;

namespace TVCleaner.Core
{
    public enum CleanMode { Recommended, Maximum, Manual }

    public enum ChangeAction { Disable, Uninstall }

    public sealed class PlanItem
    {
        public TvPackage Package { get; set; }
        public ChangeAction Action { get; set; }
    }

    /// <summary>Turns a mode (or a manual selection) into an ordered list of actions.</summary>
    public static class Planner
    {
        public static bool IsInMode(TvPackage p, CleanMode mode)
        {
            if (p.Locked || p.Info == null) return false;
            PackageCategory c = p.Info.Category;
            if (mode == CleanMode.Recommended) return c == PackageCategory.Ads || c == PackageCategory.Junk;
            if (mode == CleanMode.Maximum)
                return c == PackageCategory.Ads || c == PackageCategory.Junk || c == PackageCategory.Optional;
            return false;
        }

        public static List<PlanItem> Build(IEnumerable<TvPackage> packages, CleanMode mode,
            ICollection<string> manualSelection, bool uninstallWhenPossible)
        {
            var plan = new List<PlanItem>();
            foreach (TvPackage p in packages)
            {
                if (p.Locked || (p.Info == null && !p.IsUserApp)) continue;
                bool chosen = mode == CleanMode.Manual
                    ? manualSelection != null && manualSelection.Contains(p.Name)
                    : IsInMode(p, mode); // user apps are never in presets: IsInMode needs Info
                if (!chosen) continue;
                bool uninstall = uninstallWhenPossible && !p.IsSystem && (p.Info == null || p.Info.CanUninstall);
                if (!uninstall && p.IsDisabled) continue; // nothing to do
                plan.Add(new PlanItem { Package = p, Action = uninstall ? ChangeAction.Uninstall : ChangeAction.Disable });
            }
            return plan;
        }
    }
}
