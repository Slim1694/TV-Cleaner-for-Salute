using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace TVCleaner.Core
{
    /// <summary>JSON report for growing packages.json. Contains no serial number or account data.</summary>
    public static class ReportWriter
    {
        public static string Build(TvInfo info, IEnumerable<TvPackage> packages)
        {
            var list = packages.Select(p => new Dictionary<string, object>
            {
                { "package", p.Name },
                { "system", p.IsSystem },
                { "disabled", p.IsDisabled },
                { "category", p.Info != null ? p.Info.Category.ToString().ToLowerInvariant() : p.IsUserApp ? "user" : "unknown" }
            }).ToList();
            var root = new Dictionary<string, object>
            {
                { "tool", "TVCleaner " + AppInfo.Version },
                { "model", info.Model },
                { "buildInfo", info.BuildInfo },
                { "firmware", info.Firmware },
                { "android", info.AndroidVersion },
                { "packages", list }
            };
            return new JavaScriptSerializer().Serialize(root);
        }
    }
}
