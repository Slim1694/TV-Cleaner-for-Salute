using System;
using System.Text;
using TVCleaner.Adb;

namespace TVCleaner.Tools
{
    /// <summary>Usage: TVCleaner.Probe.exe IP command... — runs one command on the TV and prints the output.</summary>
    static class AdbProbe
    {
        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (args.Length < 2)
            {
                Console.WriteLine("Использование: TVCleaner.Probe.exe <IP> <команда>");
                return 2;
            }
            try
            {
                AdbKeyStore keys = AdbKeyStore.LoadOrCreate(AppInfo.KeyFile);
                using (AdbConnection c = AdbConnection.Connect(args[0], AdbConnection.DefaultPort, keys,
                    () => Console.WriteLine("Подтвердите запрос на экране ТВ...")))
                {
                    Console.WriteLine("banner: " + c.DeviceBanner);
                    Console.Write(c.Exec(string.Join(" ", args, 1, args.Length - 1)));
                }
                return 0;
            }
            catch (AdbException e)
            {
                Console.WriteLine("ОШИБКА (" + e.Error + "): " + e.Message);
                return 1;
            }
        }
    }
}
