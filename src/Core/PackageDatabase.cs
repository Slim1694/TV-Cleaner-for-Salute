using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace TVCleaner.Core
{
    public enum PackageCategory { Ads, Junk, Optional, System }

    public enum RiskLevel { Low, Medium, High }

    public sealed class PackageInfo
    {
        public string Package { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Breaks { get; set; }
        public PackageCategory Category { get; set; }
        public RiskLevel Risk { get; set; }
        public bool CanUninstall { get; set; }
    }

    public sealed class PackageDatabaseException : Exception
    {
        public PackageDatabaseException(string message) : base(message) { }
    }

    /// <summary>Knowledge base loaded from packages.json: what each package is and how risky it is.</summary>
    public sealed class PackageDatabase
    {
        private readonly Dictionary<string, PackageInfo> byName = new Dictionary<string, PackageInfo>();

        public int Version { get; private set; }
        public string Updated { get; private set; }
        public List<string> TestedModels { get; private set; }

        public int Count
        {
            get { return byName.Count; }
        }

        public PackageInfo Find(string package)
        {
            PackageInfo p;
            return byName.TryGetValue(package, out p) ? p : null;
        }

        public static PackageDatabase Load(string path)
        {
            if (!File.Exists(path)) throw new PackageDatabaseException("Не найден файл базы: " + path);
            return Parse(File.ReadAllText(path));
        }

        public static PackageDatabase Parse(string json)
        {
            Dictionary<string, object> root;
            try
            {
                root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            }
            catch (ArgumentException e)
            {
                throw new PackageDatabaseException("packages.json повреждён: " + e.Message);
            }
            catch (InvalidOperationException e)
            {
                throw new PackageDatabaseException("packages.json повреждён: " + e.Message);
            }
            if (root == null) throw new PackageDatabaseException("packages.json пустой");

            var db = new PackageDatabase();
            db.Version = Convert.ToInt32(Field(root, "version", 0, true));
            db.Updated = Field(root, "updated", 0, false);
            db.TestedModels = new List<string>();
            object models;
            if (root.TryGetValue("testedModels", out models) && models is IEnumerable && !(models is string))
                foreach (object m in (IEnumerable)models) db.TestedModels.Add(Convert.ToString(m));

            object packages;
            if (!root.TryGetValue("packages", out packages) || !(packages is IEnumerable) || packages is string)
                throw new PackageDatabaseException("packages.json: поле packages должно быть списком");

            int index = 0;
            foreach (object item in (IEnumerable)packages)
            {
                index++;
                var d = item as Dictionary<string, object>;
                if (d == null) throw new PackageDatabaseException("packages.json: запись №" + index + " не является объектом");
                var p = new PackageInfo
                {
                    Package = Field(d, "package", index, true),
                    Name = Field(d, "name", index, true),
                    Description = Field(d, "description", index, false),
                    Breaks = Field(d, "breaks", index, false),
                    Category = ParseCategory(Field(d, "category", index, true), index),
                    Risk = ParseRisk(Field(d, "risk", index, true), index),
                    CanUninstall = d.ContainsKey("canUninstall") && Convert.ToBoolean(d["canUninstall"])
                };
                if (db.byName.ContainsKey(p.Package))
                    throw new PackageDatabaseException("packages.json: пакет " + p.Package + " указан дважды");
                db.byName.Add(p.Package, p);
            }
            return db;
        }

        private static string Field(Dictionary<string, object> d, string key, int index, bool required)
        {
            object v;
            if (!d.TryGetValue(key, out v) || v == null)
            {
                if (!required) return "";
                string where = index > 0 ? "запись №" + index + ": " : "";
                throw new PackageDatabaseException("packages.json: " + where + "нет поля " + key);
            }
            return Convert.ToString(v);
        }

        private static PackageCategory ParseCategory(string value, int index)
        {
            switch (value.ToLowerInvariant())
            {
                case "ads": return PackageCategory.Ads;
                case "junk": return PackageCategory.Junk;
                case "optional": return PackageCategory.Optional;
                case "system": return PackageCategory.System;
            }
            throw new PackageDatabaseException("packages.json: запись №" + index + ": неизвестная категория " + value);
        }

        private static RiskLevel ParseRisk(string value, int index)
        {
            switch (value.ToLowerInvariant())
            {
                case "low": return RiskLevel.Low;
                case "medium": return RiskLevel.Medium;
                case "high": return RiskLevel.High;
            }
            throw new PackageDatabaseException("packages.json: запись №" + index + ": неизвестный риск " + value);
        }
    }
}
