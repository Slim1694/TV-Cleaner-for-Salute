using System.Text.RegularExpressions;
using TVCleaner.Adb;

namespace TVCleaner.Core
{
    /// <summary>App cache: Android's own estimate and a clear that reports the space really freed.</summary>
    public static class CacheInfo
    {
        public const string DiskstatsCommand = "dumpsys diskstats";
        public const string DataFreeCommand = "df -k /data";

        /// <summary>Total app cache in bytes as last counted by Android (refreshed by the system about daily); -1 if unknown.</summary>
        public static long ReadCacheBytes(IAdbShell shell)
        {
            Match m = Regex.Match(shell.Exec(DiskstatsCommand), @"App Cache Size:\s*(\d+)");
            long v;
            return m.Success && long.TryParse(m.Groups[1].Value, out v) ? v : -1;
        }

        /// <summary>Asks Android to trim all app caches; returns bytes freed on /data (0 if none).</summary>
        public static long Clear(IAdbShell shell)
        {
            long before = FreeKb(shell);
            shell.Exec("pm trim-caches 999G");
            long after = FreeKb(shell);
            return before >= 0 && after > before ? (after - before) * 1024 : 0;
        }

        private static long FreeKb(IAdbShell shell)
        {
            TvSnapshot s = TvStats.Parse(shell.Exec(DataFreeCommand));
            return s.DataTotalKb > 0 ? s.DataFreeKb : -1;
        }
    }
}
