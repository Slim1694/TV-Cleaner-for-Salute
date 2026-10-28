using System;
using System.Collections.Generic;
using System.IO;
using TVCleaner.Core;

namespace TVCleaner.Tests
{
    public static class AppLabelTests
    {
        private static string Fixture(string name)
        {
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "tests", "fixtures", name));
        }

        public static void TestLiteralLabel()
        {
            Assert.Equal("Literal Label", ApkLabel.Read(new LocalFileReader(Fixture("label-literal.apk")), "ru"));
        }

        public static void TestReferenceLabelPrefersRussian()
        {
            Assert.Equal("Тест названия", ApkLabel.Read(new LocalFileReader(Fixture("label-reference.apk")), "ru"));
        }

        public static void TestReferenceLabelFallsBackToDefault()
        {
            Assert.Equal("Label Test", ApkLabel.Read(new LocalFileReader(Fixture("label-reference.apk")), "de"));
        }

        public static void TestMissingZipEntryIsNull()
        {
            var zip = new ZipDirectory(new LocalFileReader(Fixture("label-literal.apk")));
            Assert.Equal(null, zip.Read("no/such/file"));
            Assert.True(zip.Read("AndroidManifest.xml").Length > 0, "manifest is readable");
        }

        public static void TestNotAZipIsRejected()
        {
            string path = Path.Combine(Path.GetTempPath(), "tvcleaner-notzip-" + Guid.NewGuid());
            File.WriteAllBytes(path, new byte[100]);
            try
            {
                Assert.Throws<InvalidDataException>(() => new ZipDirectory(new LocalFileReader(path)));
            }
            finally
            {
                File.Delete(path);
            }
        }

        public static void TestRemoteReaderCommands()
        {
            var s = new FakeShell();
            s.Responses["stat -c %s '/data/app/x/base.apk'"] = "1234\n";
            var r = new RemoteFileReader(s, "/data/app/x/base.apk");
            Assert.Equal(1234L, r.Size);
            r.Read(100, 50);
            Assert.Equal("tail -c +101 '/data/app/x/base.apk' | head -c 50", s.Commands[s.Commands.Count - 1]);
        }

        public static void TestReaderFillsUserAppsAndCaches()
        {
            string root = HistoryTests.TempRoot();
            try
            {
                byte[] apk = File.ReadAllBytes(Fixture("label-reference.apk"));
                var s = new FakeShell();
                s.Responses["pm path com.user.app"] = "package:/data/app/com.user.app-1/base.apk\n";
                s.Responses["stat -c %s '/data/app/com.user.app-1/base.apk'"] = apk.Length + "\n";
                s.BytesHandler = cmd => SliceFromCommand(cmd, apk);
                var p = new TvPackage { Name = "com.user.app" };
                string cache = Path.Combine(root, "labels.json");

                new AppLabelReader(s, cache).Fill(new List<TvPackage> { p });
                Assert.Equal("Тест названия", p.DisplayName);

                var s2 = new FakeShell();
                s2.Responses["pm path com.user.app"] = s.Responses["pm path com.user.app"];
                s2.Responses["stat -c %s '/data/app/com.user.app-1/base.apk'"] = apk.Length + "\n";
                var p2 = new TvPackage { Name = "com.user.app" };
                new AppLabelReader(s2, cache).Fill(new List<TvPackage> { p2 });
                Assert.Equal("Тест названия", p2.DisplayName);
                Assert.True(!s2.Commands.Exists(c => c.StartsWith("tail")), "second run uses the cache");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        public static void TestBrokenApkKeepsPackageName()
        {
            var s = new FakeShell();
            s.Responses["pm path com.broken"] = "package:/data/app/com.broken-1/base.apk\n";
            s.Responses["stat -c %s '/data/app/com.broken-1/base.apk'"] = "100\n";
            s.BytesHandler = cmd => new byte[100];
            var p = new TvPackage { Name = "com.broken" };
            new AppLabelReader(s, null).Fill(new List<TvPackage> { p });
            Assert.Equal("com.broken", p.DisplayName);
        }

        /// <summary>Emulates "tail -c +N 'path' | head -c M" on a local byte array.</summary>
        private static byte[] SliceFromCommand(string cmd, byte[] file)
        {
            string[] parts = cmd.Split(' ');
            long start = long.Parse(parts[2].TrimStart('+')) - 1;
            int count = int.Parse(parts[parts.Length - 1]);
            int n = (int)Math.Max(0, Math.Min(count, file.Length - start));
            var result = new byte[n];
            Buffer.BlockCopy(file, (int)start, result, 0, n);
            return result;
        }
    }
}
