using System;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using TVCleaner.Adb;

namespace TVCleaner.Tests
{
    public static class AdbKeyStoreTests
    {
        public static void TestModInverse()
        {
            Assert.Equal(new BigInteger(4), AdbKeyStore.ModInverse(3, 11));
        }

        public static void TestAndroidKeyLayout()
        {
            var rsa = new RSACryptoServiceProvider(2048);
            rsa.PersistKeyInCsp = false;
            RSAParameters p = rsa.ExportParameters(false);
            byte[] k = AdbKeyStore.EncodeAndroidKey(p.Modulus, p.Exponent);

            Assert.Equal(524, k.Length);
            Assert.Equal(64u, AdbMessage.ReadUInt(k, 0));
            uint n0inv = AdbMessage.ReadUInt(k, 4);
            uint n0 = AdbMessage.ReadUInt(k, 8);
            Assert.Equal(0xFFFFFFFFu, unchecked(n0inv * n0));
            Assert.Equal(p.Modulus[p.Modulus.Length - 1], k[8]);
            Assert.Equal(65537u, AdbMessage.ReadUInt(k, 520));

            BigInteger n = AdbKeyStore.FromBigEndian(p.Modulus);
            var rr = new byte[257];
            Buffer.BlockCopy(k, 264, rr, 0, 256);
            Assert.Equal(BigInteger.ModPow(2, 4096, n), new BigInteger(rr));
        }

        public static void TestKeyIsPersistedAndSignsDeterministically()
        {
            string path = Path.Combine(Path.GetTempPath(), "tvcleaner-key-" + Guid.NewGuid() + ".xml");
            try
            {
                var token = new byte[20];
                new Random(1).NextBytes(token);
                AdbKeyStore a = AdbKeyStore.LoadOrCreate(path);
                AdbKeyStore b = AdbKeyStore.LoadOrCreate(path);
                Assert.Equal(a.AndroidPublicKey("x"), b.AndroidPublicKey("x"));
                byte[] sa = a.SignToken(token);
                Assert.Equal(256, sa.Length);
                Assert.Equal(Convert.ToBase64String(sa), Convert.ToBase64String(b.SignToken(token)));
                Assert.True(a.AndroidPublicKey("me@pc").EndsWith(" me@pc"), "имя в конце ключа");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
