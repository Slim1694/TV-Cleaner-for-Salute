using System;
using System.Collections.Generic;
using System.Linq;

namespace TVCleaner.Core
{
    /// <summary>TV model as shown to people, and in the form used to compare with packages.json.</summary>
    public static class ModelName
    {
        /// <summary>"SBER_55_CV9632-N55_ST5461D09-1_..._TK2211-119.txt" → "SBER 55 CV9632-N55"; falls back to ro.product.model.</summary>
        public static string Pretty(string buildInfo, string productModel)
        {
            string n = Normalize(buildInfo);
            if (n.Length == 0) return productModel ?? "";
            string[] parts = n.Split('_');
            return string.Join(" ", parts.Take(Math.Min(3, parts.Length)));
        }

        /// <summary>ro.build.info without the ".txt" suffix some firmwares add.</summary>
        public static string Normalize(string buildInfo)
        {
            string s = (buildInfo ?? "").Trim();
            return s.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? s.Substring(0, s.Length - 4) : s;
        }

        public static bool IsTested(IEnumerable<string> testedModels, string buildInfo)
        {
            string n = Normalize(buildInfo);
            return n.Length > 0 && testedModels.Any(m => Normalize(m) == n);
        }
    }
}
