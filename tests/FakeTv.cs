using System.Collections.Generic;
using System.Text;
using TVCleaner.Core;

namespace TVCleaner.Tests
{
    /// <summary>Simulates pm on a TV. A fake APK's bytes are the ASCII package name.</summary>
    public sealed class FakeTv
    {
        public readonly FakeShell Shell = new FakeShell();
        public readonly HashSet<string> Installed = new HashSet<string>();
        public readonly HashSet<string> Disabled = new HashSet<string>();
        public string Home = "com.spocky.projengmenu";
        public bool Network = true;
        public bool BreakNetworkOnNextDisable;
        public int StatSizeOffset;

        public FakeTv()
        {
            Shell.Handler = Handle;
            Shell.BytesHandler = cmd => cmd.StartsWith("cat '") ? Encoding.ASCII.GetBytes(PackageFromPath(cmd)) : null;
            Shell.InputHandler = (cmd, input) =>
            {
                if (!cmd.StartsWith("cmd package install -S ")) return null;
                Installed.Add(Encoding.ASCII.GetString(input));
                return "Success\n";
            };
        }

        private static string PackageFromPath(string cmd)
        {
            int start = cmd.IndexOf("/data/app/") + 10;
            return cmd.Substring(start, cmd.IndexOf("-1/", start) - start);
        }

        private string Handle(string cmd)
        {
            if (cmd == "echo ok") return "ok\n";
            if (cmd == TvInspector.HomeCommand) return Home == null ? "No activity found\n" : Home + "/.Main\n";
            if (cmd == HealthCheck.NetworkCommand) return Network ? "2\n" : "0\n";
            if (cmd.StartsWith("pm disable-user --user 0 "))
            {
                string p = cmd.Substring(25);
                Disabled.Add(p);
                if (BreakNetworkOnNextDisable) { Network = false; BreakNetworkOnNextDisable = false; }
                return "Package " + p + " new state: disabled-user\n";
            }
            if (cmd.StartsWith("pm enable "))
            {
                string p = cmd.Substring(10);
                Disabled.Remove(p);
                Network = true;
                return "Package " + p + " new state: enabled\n";
            }
            if (cmd.StartsWith("pm path "))
            {
                string p = cmd.Substring(8);
                return Installed.Contains(p) ? "package:/data/app/" + p + "-1/base.apk\n" : "";
            }
            if (cmd.StartsWith("stat -c %s ")) return (PackageFromPath(cmd).Length + StatSizeOffset) + "\n";
            if (cmd.StartsWith("pm uninstall "))
            {
                Installed.Remove(cmd.Substring(13));
                return "Success\n";
            }
            return null;
        }
    }
}
