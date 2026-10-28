using System;
using System.Collections.Generic;

namespace TVCleaner.Core
{
    /// <summary>Packages that must never be disabled or removed, regardless of packages.json.</summary>
    public static class Blocklist
    {
        private static readonly HashSet<string> Exact = new HashSet<string>
        {
            "android",
            "com.android.shell",                       // ADB itself
            "com.android.systemui",
            "com.android.tv.settings",
            "com.android.bluetooth",                   // remote control
            "com.android.packageinstaller",
            "com.android.defcontainer",
            "com.android.inputdevices",
            "com.android.externalstorage",
            "com.android.keychain",
            "com.android.certinstaller",
            "com.android.se",
            "com.ctv.android.tvboot",
            "ru.sberdevices.SberDevicesServiceSystem",  // device owner
            "ru.sberdevices.services",
            "ru.sberdevices.services.core",
            "ru.sberdevices.resources",
            "ru.sberdevices.webview",
            "ru.sberdevices.tvinputs",                  // HDMI inputs
            "ru.sberdevices.settings",
            "ru.sberdevices.app.setup",
            "ru.sberdevices.app.starkeyboard"           // the only on-screen keyboard
        };

        private static readonly string[] Prefixes = { "com.android.providers.", "com.mediatek.", "android.ext." };

        public static bool IsBlocked(string package)
        {
            if (Exact.Contains(package)) return true;
            foreach (string prefix in Prefixes)
                if (package.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
