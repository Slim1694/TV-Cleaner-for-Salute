using System;
using System.IO;
using TVCleaner.Adb;

namespace TVCleaner.Core
{
    /// <summary>Installs an APK file from the PC by streaming it to "cmd package install -S".</summary>
    public static class ApkInstaller
    {
        /// <summary>Result.Package holds the app name read from the APK (or the file name), for the results window.</summary>
        public static RestoreResult Install(IAdbShell shell, string localApk)
        {
            string name = NameOf(localApk);
            byte[] apk = File.ReadAllBytes(localApk);
            string output = shell.ExecWithInput("cmd package install -S " + apk.Length, apk).Trim();
            bool ok = output.IndexOf("Success", StringComparison.Ordinal) >= 0;
            return new RestoreResult { Package = name, Ok = ok, Message = ok ? "установлено" : Explain(output) };
        }

        private static string NameOf(string localApk)
        {
            try
            {
                string label = ApkLabel.Read(new LocalFileReader(localApk), "ru");
                if (!string.IsNullOrEmpty(label)) return label;
            }
            catch (Exception)
            {
                // not a readable APK: the installer will report the real error
            }
            return Path.GetFileName(localApk);
        }

        /// <summary>Common Android install errors in plain words, with the original code kept.</summary>
        private static string Explain(string output)
        {
            string hint =
                output.Contains("INSTALL_FAILED_OLDER_SDK") ? "нужна более новая версия Android" :
                output.Contains("INSTALL_FAILED_NO_MATCHING_ABIS") ? "приложение не для процессора этого ТВ" :
                output.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE") ? "уже установлена версия с другой подписью" :
                output.Contains("INSTALL_FAILED_VERSION_DOWNGRADE") ? "уже установлена более новая версия" :
                output.Contains("INSTALL_FAILED_INSUFFICIENT_STORAGE") ? "не хватает места на ТВ" :
                output.Contains("INSTALL_PARSE_FAILED") ? "файл повреждён или это не APK" :
                "не удалось";
            return hint + (output.Length > 0 ? " (" + output + ")" : "");
        }
    }
}
