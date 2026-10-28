using System.IO;
using System.Text;
using TVCleaner.Adb;

namespace TVCleaner.Tests
{
    public static class AdbMessageTests
    {
        public static void TestChecksumIsByteSum()
        {
            Assert.Equal(6u, AdbMessage.Checksum(new byte[] { 1, 2, 3 }));
        }

        public static void TestEncodeCnxnHeader()
        {
            byte[] b = AdbMessage.WithString(AdbMessage.CNXN, 0x01000000, 4096, "host::").Encode();
            Assert.Equal(24 + 7, b.Length);
            Assert.Equal("CNXN", Encoding.ASCII.GetString(b, 0, 4));
            Assert.Equal(0x01000000u, AdbMessage.ReadUInt(b, 4));
            Assert.Equal(4096u, AdbMessage.ReadUInt(b, 8));
            Assert.Equal(7u, AdbMessage.ReadUInt(b, 12));
            Assert.Equal(AdbMessage.Checksum(Encoding.ASCII.GetBytes("host::\0")), AdbMessage.ReadUInt(b, 16));
            Assert.Equal(AdbMessage.CNXN ^ 0xffffffff, AdbMessage.ReadUInt(b, 20));
        }

        public static void TestDecodeRoundTrip()
        {
            byte[] b = new AdbMessage(AdbMessage.WRTE, 5, 9, new byte[] { 10, 20 }).Encode();
            int length;
            AdbMessage m = AdbMessage.DecodeHeader(b, out length);
            Assert.Equal(AdbMessage.WRTE, m.Command);
            Assert.Equal(5u, m.Arg0);
            Assert.Equal(9u, m.Arg1);
            Assert.Equal(2, length);
        }

        public static void TestDecodeRejectsBadMagic()
        {
            byte[] b = new AdbMessage(AdbMessage.OKAY, 1, 2, null).Encode();
            b[20] ^= 1;
            Assert.Throws<InvalidDataException>(() => { int n; AdbMessage.DecodeHeader(b, out n); });
        }

        public static void TestPayloadTextStripsTerminator()
        {
            Assert.Equal("device::x", AdbMessage.WithString(AdbMessage.CNXN, 0, 0, "device::x").PayloadText);
        }

        public static void TestCommandName()
        {
            Assert.Equal("OKAY", AdbMessage.CommandName(AdbMessage.OKAY));
        }
    }
}
