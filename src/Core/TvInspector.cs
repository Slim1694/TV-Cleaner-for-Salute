using System.Collections.Generic;
using System.Linq;
using System.Text;
using TVCleaner.Adb;

namespace TVCleaner.Core
{
    public sealed class TvInfo
    {
        public string Model { get; set; }
        public string BuildInfo { get; set; }
        public string Firmware { get; set; }
        public string AndroidVersion { get; set; }
        public string Serial { get; set; }
    }

    public sealed class TvPackage
    {
        public string Name { get; set; }
        public bool IsSystem { get; set; }
        public bool IsDisabled { get; set; }
        public PackageInfo Info { get; set; }
        public bool Locked { get; set; }
        public string LockReason { get; set; }

        /// <summary>App name read from the APK (user apps only); null if unknown.</summary>
        public string Label { get; set; }

        public string DisplayName
        {
            get { return Info != null ? Info.Name : Label ?? Name; }
        }

        /// <summary>Installed by the user (not built in, not in packages.json): selectable only manually.</summary>
        public bool IsUserApp
        {
            get { return !IsSystem && Info == null; }
        }
    }

    /// <summary>Reads TV identity and the package list, and decides which packages must stay untouched.</summary>
    public sealed class TvInspector
    {
        public const string HomeCommand =
            "cmd package resolve-activity --brief -a android.intent.action.MAIN -c android.intent.category.HOME";

        public const string KeyboardCommand = "settings get secure default_input_method";

        private readonly IAdbShell shell;
        private readonly PackageDatabase db;

        public TvInspector(IAdbShell shell, PackageDatabase db)
        {
            this.shell = shell;
            this.db = db;
        }

        public TvInfo ReadInfo()
        {
            string serial = Prop("ro.boot.serialno");
            if (serial.Length == 0) serial = Prop("ro.serialno");
            if (serial.Length == 0) serial = "unknown";
            return new TvInfo
            {
                Model = Prop("ro.product.model"),
                BuildInfo = Prop("ro.build.info"),
                Firmware = Prop("ro.build.version.incremental"),
                AndroidVersion = Prop("ro.build.version.release"),
                Serial = SafeName(serial)
            };
        }

        public string ReadHomePackage()
        {
            return ParseHome(shell.Exec(HomeCommand));
        }

        public List<TvPackage> ReadPackages(string homePackage)
        {
            List<string> all = ParsePackageLines(shell.Exec("pm list packages"));
            var system = new HashSet<string>(ParsePackageLines(shell.Exec("pm list packages -s")));
            var disabled = new HashSet<string>(ParsePackageLines(shell.Exec("pm list packages -d")));
            string keyboard = ParseHome(shell.Exec(KeyboardCommand)); // same "pkg/.Class" format
            var result = new List<TvPackage>();
            foreach (string name in all.Distinct().OrderBy(n => n))
            {
                var p = new TvPackage
                {
                    Name = name,
                    IsSystem = system.Contains(name),
                    IsDisabled = disabled.Contains(name),
                    Info = db.Find(name)
                };
                if (Blocklist.IsBlocked(name)) Lock(p, "системный, не трогать");
                else if (homePackage != null && name == homePackage) Lock(p, "текущий главный экран (лаунчер)");
                else if (keyboard != null && name == keyboard) Lock(p, "текущая клавиатура, без неё не будет ввода текста");
                else if (p.Info == null && p.IsSystem) Lock(p, "неизвестный пакет, не трогать");
                else if (p.Info != null && p.Info.Category == PackageCategory.System) Lock(p, "системный, не трогать");
                result.Add(p);
            }
            return result;
        }

        /// <summary>Last non-empty line "pkg/.Activity" → "pkg"; null when nothing handles HOME.</summary>
        public static string ParseHome(string output)
        {
            string last = null;
            foreach (string line in output.Split('\n'))
            {
                string t = line.Trim();
                if (t.Length > 0) last = t;
            }
            if (last == null) return null;
            int slash = last.IndexOf('/');
            return slash > 0 ? last.Substring(0, slash) : null;
        }

        /// <summary>Lines "package:xxx" (pm list packages, pm path) → "xxx".</summary>
        public static List<string> ParsePackageLines(string output)
        {
            var result = new List<string>();
            foreach (string line in output.Split('\n'))
            {
                string t = line.Trim();
                if (t.StartsWith("package:") && t.Length > 8) result.Add(t.Substring(8).Trim());
            }
            return result;
        }

        public static string SafeName(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return sb.ToString();
        }

        private string Prop(string name)
        {
            return shell.Exec("getprop " + name).Trim();
        }

        private static void Lock(TvPackage p, string reason)
        {
            p.Locked = true;
            p.LockReason = reason;
        }
    }
}
