using TVCleaner.Adb;

namespace TVCleaner.Core
{
    /// <summary>Quick check after every change: TV answers, a launcher exists and is unchanged, network is up.</summary>
    public sealed class HealthCheck
    {
        public const string NetworkCommand = "dumpsys connectivity | grep -c 'CONNECTED/CONNECTED'";

        private readonly IAdbShell shell;
        private readonly string expectedHome;

        public HealthCheck(IAdbShell shell, string expectedHome)
        {
            this.shell = shell;
            this.expectedHome = expectedHome;
        }

        /// <summary>Returns null when everything is fine, otherwise a human-readable problem.</summary>
        public string Check()
        {
            if (shell.Exec("echo ok").Trim() != "ok") return "Телевизор не отвечает на команды";

            string home = TvInspector.ParseHome(shell.Exec(TvInspector.HomeCommand));
            if (home == null || home == "android") return "На телевизоре не осталось лаунчера (главного экрана)";
            if (expectedHome != null && home != expectedHome) return "Сменился лаунчер: теперь " + home;

            int connected;
            if (!int.TryParse(shell.Exec(NetworkCommand).Trim(), out connected) || connected == 0)
                return "Телевизор потерял сеть";
            return null;
        }
    }
}
