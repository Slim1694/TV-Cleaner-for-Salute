using TVCleaner.Core;

namespace TVCleaner.Tests
{
    public static class BlocklistTests
    {
        public static void TestCriticalPackagesBlocked()
        {
            Assert.True(Blocklist.IsBlocked("com.android.shell"), "shell");
            Assert.True(Blocklist.IsBlocked("android"), "android");
            Assert.True(Blocklist.IsBlocked("ru.sberdevices.SberDevicesServiceSystem"), "device owner");
            Assert.True(Blocklist.IsBlocked("ru.sberdevices.webview"), "webview");
            Assert.True(Blocklist.IsBlocked("com.android.providers.tv"), "providers prefix");
            Assert.True(Blocklist.IsBlocked("com.mediatek.hotkey.dispatcher"), "mediatek prefix");
        }

        public static void TestRegularPackagesNotBlocked()
        {
            Assert.True(!Blocklist.IsBlocked("ru.sberdevices.survey"), "survey");
            Assert.True(!Blocklist.IsBlocked("com.android.printspooler"), "printspooler");
            Assert.True(!Blocklist.IsBlocked("com.mediatekx.fake"), "prefix must include the dot");
        }
    }
}
