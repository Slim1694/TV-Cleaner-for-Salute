using System;
using System.IO;

namespace TVCleaner
{
    public static class AppInfo
    {
        public const string Name = "TV Cleaner for Salute";
        public const string Version = "1.1.0";

        public static string Root
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TVCleaner"); }
        }

        public static string KeyFile
        {
            get { return Path.Combine(Root, "adbkey"); }
        }
    }
}
