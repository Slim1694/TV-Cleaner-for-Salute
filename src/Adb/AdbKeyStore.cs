using System;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;

namespace TVCleaner.Adb
{
    /// <summary>RSA-2048 key that authorizes this PC on the TV ("Allow debugging?").</summary>
    public sealed class AdbKeyStore
    {
        public const int KeyBits = 2048;
        private readonly RSACryptoServiceProvider rsa;

        private AdbKeyStore(RSACryptoServiceProvider rsa)
        {
            this.rsa = rsa;
        }

        /// <summary>Loads the key (XML) from path, or creates and saves a new one.</summary>
        public static AdbKeyStore LoadOrCreate(string path)
        {
            var rsa = new RSACryptoServiceProvider(KeyBits);
            rsa.PersistKeyInCsp = false;
            if (File.Exists(path))
            {
                rsa.FromXmlString(File.ReadAllText(path));
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, rsa.ToXmlString(true));
            }
            return new AdbKeyStore(rsa);
        }

        /// <summary>adbd verifies the 20-byte token as if it were a SHA-1 digest (PKCS#1 v1.5).</summary>
        public byte[] SignToken(byte[] token)
        {
            return rsa.SignHash(token, CryptoConfig.MapNameToOID("SHA1"));
        }

        public string AndroidPublicKey(string name)
        {
            RSAParameters p = rsa.ExportParameters(false);
            return Convert.ToBase64String(EncodeAndroidKey(p.Modulus, p.Exponent)) + " " + name;
        }

        /// <summary>
        /// Android RSAPublicKey struct: uint32 size in words, uint32 n0inv (-1/n[0] mod 2^32),
        /// modulus little-endian, rr = R^2 mod n (R = 2^bits) little-endian, uint32 exponent.
        /// </summary>
        public static byte[] EncodeAndroidKey(byte[] modulusBigEndian, byte[] exponentBigEndian)
        {
            int size = modulusBigEndian.Length;
            BigInteger n = FromBigEndian(modulusBigEndian);
            BigInteger r32 = BigInteger.One << 32;
            BigInteger n0 = n % r32;
            uint n0inv = (uint)((r32 - ModInverse(n0, r32)) % r32);
            BigInteger rr = BigInteger.ModPow(2, size * 8 * 2, n);

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((uint)(size / 4));
                w.Write(n0inv);
                w.Write(ToLittleEndian(n, size));
                w.Write(ToLittleEndian(rr, size));
                w.Write((uint)FromBigEndian(exponentBigEndian));
                w.Flush();
                return ms.ToArray();
            }
        }

        public static BigInteger FromBigEndian(byte[] bigEndian)
        {
            var le = new byte[bigEndian.Length + 1]; // extra zero byte keeps the value positive
            for (int i = 0; i < bigEndian.Length; i++) le[i] = bigEndian[bigEndian.Length - 1 - i];
            return new BigInteger(le);
        }

        public static byte[] ToLittleEndian(BigInteger value, int size)
        {
            byte[] raw = value.ToByteArray();
            var result = new byte[size];
            Buffer.BlockCopy(raw, 0, result, 0, Math.Min(size, raw.Length));
            return result;
        }

        public static BigInteger ModInverse(BigInteger a, BigInteger m)
        {
            BigInteger oldR = a, r = m, oldS = 1, s = 0;
            while (r != 0)
            {
                BigInteger q = oldR / r;
                BigInteger t = r; r = oldR - q * r; oldR = t;
                t = s; s = oldS - q * s; oldS = t;
            }
            return ((oldS % m) + m) % m;
        }
    }
}
