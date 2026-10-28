using TVCleaner.Core;

namespace TVCleaner.Tests
{
    public static class HealthCheckTests
    {
        private static FakeShell Healthy()
        {
            var s = new FakeShell();
            s.Responses["echo ok"] = "ok\n";
            s.Responses[TvInspector.HomeCommand] = "x\ncom.spocky.projengmenu/.Main\n";
            s.Responses[HealthCheck.NetworkCommand] = "2\n";
            return s;
        }

        public static void TestHealthy()
        {
            Assert.Equal(null, new HealthCheck(Healthy(), "com.spocky.projengmenu").Check());
        }

        public static void TestNoEcho()
        {
            FakeShell s = Healthy();
            s.Responses["echo ok"] = "";
            Assert.True(new HealthCheck(s, null).Check() != null, "no echo must be a problem");
        }

        public static void TestHomeMissing()
        {
            FakeShell s = Healthy();
            s.Responses[TvInspector.HomeCommand] = "No activity found\n";
            Assert.True(new HealthCheck(s, null).Check().Contains("лаунчер"), "home missing");
        }

        public static void TestHomeChanged()
        {
            FakeShell s = Healthy();
            s.Responses[TvInspector.HomeCommand] = "android/com.android.internal.app.ResolverActivity\n";
            Assert.True(new HealthCheck(s, "com.spocky.projengmenu").Check() != null, "home changed");
        }

        public static void TestNoNetwork()
        {
            FakeShell s = Healthy();
            s.Responses[HealthCheck.NetworkCommand] = "0\n";
            Assert.True(new HealthCheck(s, null).Check().Contains("сеть"), "network lost");
        }
    }
}
