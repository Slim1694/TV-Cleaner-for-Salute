using System;
using System.IO;
using System.Text;

namespace TVCleaner.Adb
{
    /// <summary>One ADB protocol message: 24-byte little-endian header + payload.</summary>
    public sealed class AdbMessage
    {
        public const uint CNXN = 0x4e584e43;
        public const uint AUTH = 0x48545541;
        public const uint OPEN = 0x4e45504f;
        public const uint OKAY = 0x59414b4f;
        public const uint CLSE = 0x45534c43;
        public const uint WRTE = 0x45545257;

        public const uint AuthToken = 1;
        public const uint AuthSignature = 2;
        public const uint AuthRsaPublicKey = 3;

        public const int HeaderSize = 24;

        public uint Command { get; private set; }
        public uint Arg0 { get; private set; }
        public uint Arg1 { get; private set; }
        public byte[] Payload { get; set; }

        public AdbMessage(uint command, uint arg0, uint arg1, byte[] payload)
        {
            Command = command;
            Arg0 = arg0;
            Arg1 = arg1;
            Payload = payload ?? new byte[0];
        }

        /// <summary>Payload is a zero-terminated UTF-8 string (CNXN banner, OPEN service name, public key).</summary>
        public static AdbMessage WithString(uint command, uint arg0, uint arg1, string text)
        {
            return new AdbMessage(command, arg0, arg1, Encoding.UTF8.GetBytes(text + "\0"));
        }

        public string PayloadText
        {
            get { return Encoding.UTF8.GetString(Payload).TrimEnd('\0'); }
        }

        public static uint Checksum(byte[] data)
        {
            uint sum = 0;
            foreach (byte b in data) sum += b;
            return sum;
        }

        public byte[] Encode()
        {
            var buf = new byte[HeaderSize + Payload.Length];
            WriteUInt(buf, 0, Command);
            WriteUInt(buf, 4, Arg0);
            WriteUInt(buf, 8, Arg1);
            WriteUInt(buf, 12, (uint)Payload.Length);
            WriteUInt(buf, 16, Checksum(Payload));
            WriteUInt(buf, 20, Command ^ 0xffffffff);
            Buffer.BlockCopy(Payload, 0, buf, HeaderSize, Payload.Length);
            return buf;
        }

        /// <summary>Parses a header; the caller reads payloadLength bytes of payload separately.</summary>
        public static AdbMessage DecodeHeader(byte[] header, out int payloadLength)
        {
            if (header == null || header.Length < HeaderSize)
                throw new InvalidDataException("ADB: заголовок короче 24 байт");
            uint command = ReadUInt(header, 0);
            if ((command ^ 0xffffffff) != ReadUInt(header, 20))
                throw new InvalidDataException("ADB: неверное поле magic в заголовке");
            payloadLength = (int)ReadUInt(header, 12);
            return new AdbMessage(command, ReadUInt(header, 4), ReadUInt(header, 8), null);
        }

        public static string CommandName(uint command)
        {
            var b = new byte[4];
            WriteUInt(b, 0, command);
            return Encoding.ASCII.GetString(b);
        }

        public static void WriteUInt(byte[] b, int offset, uint value)
        {
            b[offset] = (byte)value;
            b[offset + 1] = (byte)(value >> 8);
            b[offset + 2] = (byte)(value >> 16);
            b[offset + 3] = (byte)(value >> 24);
        }

        public static uint ReadUInt(byte[] b, int offset)
        {
            return (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));
        }
    }
}
