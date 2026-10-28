using TVCleaner.Core;

namespace TVCleaner.Tests
{
    public static class PackageDatabaseTests
    {
        public static void TestParsesValidFile()
        {
            PackageDatabase db = PackageDatabase.Parse(TestData.DbJson);
            Assert.Equal(1, db.Version);
            Assert.Equal(6, db.Count);
            Assert.Equal("MODEL_A", db.TestedModels[0]);
            PackageInfo p = db.Find("ru.sberdevices.survey");
            Assert.Equal(PackageCategory.Ads, p.Category);
            Assert.Equal(RiskLevel.Low, p.Risk);
            Assert.Equal(true, p.CanUninstall);
            Assert.Equal(RiskLevel.Medium, db.Find("ru.sberdevices.starlauncher").Risk);
        }

        public static void TestUnknownPackageIsNull()
        {
            Assert.Equal(null, PackageDatabase.Parse(TestData.DbJson).Find("no.such.package"));
        }

        public static void TestBrokenJsonGivesReadableError()
        {
            Assert.Throws<PackageDatabaseException>(() => PackageDatabase.Parse("{ \"version\": 1, "));
        }

        public static void TestMissingPackageField()
        {
            string json = "{\"version\":1,\"packages\":[{\"name\":\"x\",\"category\":\"ads\",\"risk\":\"low\"}]}";
            try
            {
                PackageDatabase.Parse(json);
                throw new System.Exception("ожидалась ошибка");
            }
            catch (PackageDatabaseException e)
            {
                Assert.True(e.Message.Contains("№1") && e.Message.Contains("package"), e.Message);
            }
        }

        public static void TestUnknownCategoryRejected()
        {
            string json = "{\"version\":1,\"packages\":[{\"package\":\"a\",\"name\":\"x\",\"category\":\"spam\",\"risk\":\"low\"}]}";
            Assert.Throws<PackageDatabaseException>(() => PackageDatabase.Parse(json));
        }

        public static void TestDuplicateRejected()
        {
            string e = "{\"package\":\"a\",\"name\":\"x\",\"category\":\"ads\",\"risk\":\"low\"}";
            Assert.Throws<PackageDatabaseException>(() => PackageDatabase.Parse("{\"version\":1,\"packages\":[" + e + "," + e + "]}"));
        }

        public static void TestRealDatabaseLoads()
        {
            string path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "packages.json");
            PackageDatabase db = PackageDatabase.Load(path);
            Assert.True(db.Count >= 40, "в базе мало пакетов: " + db.Count);
            Assert.Equal(PackageCategory.Ads, db.Find("ru.sberdevices.survey").Category);
            Assert.Equal(PackageCategory.Optional, db.Find("ru.sberdevices.starlauncher").Category);
            foreach (string blocked in new[] { "com.android.shell", "ru.sberdevices.services", "ru.sberdevices.webview" })
            {
                PackageInfo p = db.Find(blocked);
                Assert.True(p == null || p.Category == PackageCategory.System, blocked + " не должен быть в удаляемых категориях");
            }
        }
    }
}
