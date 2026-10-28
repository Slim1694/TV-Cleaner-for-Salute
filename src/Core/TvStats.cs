using System;
using System.Collections.Generic;
using System.Linq;
using TVCleaner.Adb;

namespace TVCleaner.Core
{
    /// <summary>One reading of TV load: CPU jiffy counters, memory and free space on /data.</summary>
    public sealed class TvSnapshot
    {
        public long CpuTotal { get; set; }
        public long CpuIdle { get; set; }
        public long MemTotalKb { get; set; }
        public long MemAvailableKb { get; set; }
        public long DataTotalKb { get; set; }
        public long DataFreeKb { get; set; }
    }

    /// <summary>Reads CPU, memory and storage with a single shell command.</summary>
    public static class TvStats
    {
        public const string Command =
            "head -1 /proc/stat; grep -E '^(MemTotal|MemAvailable):' /proc/meminfo; df -k /data";

        public static TvSnapshot Read(IAdbShell shell)
        {
            return Parse(shell.Exec(Command));
        }

        public static TvSnapshot Parse(string output)
        {
            var s = new TvSnapshot();
            foreach (string raw in output.Split('\n'))
            {
                string line = raw.Trim();
                string[] f = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (f.Length == 0) continue;
                if (f[0] == "cpu" && f.Length >= 5)
                {
                    // user nice system idle iowait irq softirq steal (guest time is already inside user)
                    List<long> v = f.Skip(1).Take(8).Select(ToLong).ToList();
                    s.CpuTotal = v.Sum();
                    s.CpuIdle = v[3] + (v.Count > 4 ? v[4] : 0);
                }
                else if (f[0] == "MemTotal:" && f.Length >= 2) s.MemTotalKb = ToLong(f[1]);
                else if (f[0] == "MemAvailable:" && f.Length >= 2) s.MemAvailableKb = ToLong(f[1]);
                else if (f.Length >= 6 && f[f.Length - 1] == "/data")
                {
                    s.DataTotalKb = ToLong(f[1]);
                    s.DataFreeKb = ToLong(f[3]);
                }
            }
            return s;
        }

        /// <summary>CPU load in percent between two readings, or -1 if it cannot be computed.</summary>
        public static int CpuPercent(TvSnapshot previous, TvSnapshot current)
        {
            if (previous == null || current == null) return -1;
            long total = current.CpuTotal - previous.CpuTotal;
            long idle = current.CpuIdle - previous.CpuIdle;
            if (total <= 0) return -1;
            return (int)Math.Round(100.0 * (total - idle) / total);
        }

        private static long ToLong(string s)
        {
            long v;
            return long.TryParse(s, out v) ? v : 0;
        }
    }
}
