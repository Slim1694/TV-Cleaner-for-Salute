using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using TVCleaner.Adb;

namespace TVCleaner.Core
{
    /// <summary>Human-readable app name from an APK: manifest label, resolved through resources.arsc if needed.</summary>
    public static class ApkLabel
    {
        public static string Read(IRangeReader apk, string language)
        {
            var zip = new ZipDirectory(apk);
            byte[] manifest = zip.Read("AndroidManifest.xml");
            if (manifest == null) return null;
            string literal;
            uint resourceId;
            if (!BinaryXml.FindApplicationLabel(manifest, out literal, out resourceId)) return null;
            if (literal != null) return literal;
            byte[] arsc = zip.Read("resources.arsc");
            return arsc == null ? null : ResourceTable.ResolveString(arsc, resourceId, language);
        }
    }

    /// <summary>Fills TvPackage.Label for the given packages, with a cache keyed by package and APK size.</summary>
    public sealed class AppLabelReader
    {
        private readonly IAdbShell shell;
        private readonly string cacheFile;

        public AppLabelReader(IAdbShell shell, string cacheFile)
        {
            this.shell = shell;
            this.cacheFile = cacheFile;
        }

        public void Fill(IEnumerable<TvPackage> packages)
        {
            Dictionary<string, string> cache = LoadCache();
            bool changed = false;
            foreach (TvPackage p in packages)
            {
                try
                {
                    List<string> paths = TvInspector.ParsePackageLines(shell.Exec("pm path " + p.Name));
                    string apk = paths.FirstOrDefault(x => x.EndsWith("/base.apk")) ?? paths.FirstOrDefault();
                    if (apk == null) continue;
                    var reader = new RemoteFileReader(shell, apk);
                    string key = p.Name + "|" + reader.Size;
                    string label;
                    if (!cache.TryGetValue(key, out label))
                    {
                        label = ApkLabel.Read(reader, "ru") ?? "";
                        cache[key] = label;
                        changed = true;
                    }
                    if (label.Trim().Length > 0) p.Label = label.Trim();
                }
                catch (AdbException)
                {
                    throw; // connection problems must reach the UI
                }
                catch (Exception)
                {
                    // unusual APK layout: keep showing the package name
                }
            }
            if (changed) SaveCache(cache);
        }

        private Dictionary<string, string> LoadCache()
        {
            try
            {
                if (cacheFile != null && File.Exists(cacheFile))
                    return new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText(cacheFile))
                           ?? new Dictionary<string, string>();
            }
            catch (Exception)
            {
                // a broken cache is simply rebuilt
            }
            return new Dictionary<string, string>();
        }

        private void SaveCache(Dictionary<string, string> cache)
        {
            if (cacheFile == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(cacheFile));
            File.WriteAllText(cacheFile, new JavaScriptSerializer().Serialize(cache));
        }
    }
}
