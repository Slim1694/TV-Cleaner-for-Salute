using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using TVCleaner.Adb;

namespace TVCleaner.Core
{
    /// <summary>Little-endian readers for Android binary formats.</summary>
    internal static class Bin
    {
        public static int U16(byte[] b, int offset)
        {
            return b[offset] | (b[offset + 1] << 8);
        }

        public static uint U32(byte[] b, int offset)
        {
            return (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));
        }
    }

    /// <summary>Random access to part of a file.</summary>
    public interface IRangeReader
    {
        long Size { get; }
        byte[] Read(long offset, int count);
    }

    /// <summary>Reads parts of a file on the TV with "tail -c +N | head -c M", without downloading it whole.</summary>
    public sealed class RemoteFileReader : IRangeReader
    {
        private readonly IAdbShell shell;
        private readonly string path;

        public RemoteFileReader(IAdbShell shell, string path)
        {
            this.shell = shell;
            this.path = path;
            long size;
            if (!long.TryParse(shell.Exec("stat -c %s '" + path + "'").Trim(), out size))
                throw new InvalidDataException("не удалось узнать размер " + path);
            Size = size;
        }

        public long Size { get; private set; }

        public byte[] Read(long offset, int count)
        {
            return shell.ReadBytes("tail -c +" + (offset + 1) + " '" + path + "' | head -c " + count);
        }
    }

    public sealed class LocalFileReader : IRangeReader
    {
        private readonly string path;

        public LocalFileReader(string path)
        {
            this.path = path;
            Size = new FileInfo(path).Length;
        }

        public long Size { get; private set; }

        public byte[] Read(long offset, int count)
        {
            using (var f = File.OpenRead(path))
            {
                f.Seek(offset, SeekOrigin.Begin);
                var buf = new byte[count];
                int total = 0, read;
                while (total < count && (read = f.Read(buf, total, count - total)) > 0) total += read;
                if (total == count) return buf;
                var part = new byte[total];
                Buffer.BlockCopy(buf, 0, part, 0, total);
                return part;
            }
        }
    }

    /// <summary>Central directory of a ZIP (APK); extracts single entries by reading only the needed ranges.</summary>
    public sealed class ZipDirectory
    {
        private readonly IRangeReader file;
        private readonly byte[] central;

        public ZipDirectory(IRangeReader file)
        {
            this.file = file;
            int tailLength = (int)Math.Min(file.Size, 22 + 65535);
            byte[] tail = Exact(file.Read(file.Size - tailLength, tailLength), tailLength);
            int eocd = -1;
            for (int i = tail.Length - 22; i >= 0; i--)
            {
                if (Bin.U32(tail, i) == 0x06054b50)
                {
                    eocd = i;
                    break;
                }
            }
            if (eocd < 0) throw new InvalidDataException("файл не является ZIP-архивом");
            int size = (int)Bin.U32(tail, eocd + 12);
            long offset = Bin.U32(tail, eocd + 16);
            central = Exact(file.Read(offset, size), size);
        }

        /// <summary>Returns the uncompressed entry, or null if the archive has no such entry.</summary>
        public byte[] Read(string name)
        {
            int p = 0;
            while (p + 46 <= central.Length && Bin.U32(central, p) == 0x02014b50)
            {
                int method = Bin.U16(central, p + 10);
                int compressed = (int)Bin.U32(central, p + 20);
                int nameLength = Bin.U16(central, p + 28);
                int extraLength = Bin.U16(central, p + 30);
                int commentLength = Bin.U16(central, p + 32);
                long localHeader = Bin.U32(central, p + 42);
                if (Encoding.UTF8.GetString(central, p + 46, nameLength) == name)
                {
                    byte[] lh = Exact(file.Read(localHeader, 30), 30);
                    if (Bin.U32(lh, 0) != 0x04034b50) throw new InvalidDataException("повреждён локальный заголовок ZIP");
                    long dataStart = localHeader + 30 + Bin.U16(lh, 26) + Bin.U16(lh, 28);
                    byte[] data = Exact(file.Read(dataStart, compressed), compressed);
                    if (method == 0) return data;
                    if (method == 8) return Inflate(data);
                    throw new InvalidDataException("неподдерживаемый метод сжатия " + method);
                }
                p += 46 + nameLength + extraLength + commentLength;
            }
            return null;
        }

        private static byte[] Inflate(byte[] data)
        {
            using (var input = new DeflateStream(new MemoryStream(data), CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                input.CopyTo(output);
                return output.ToArray();
            }
        }

        private static byte[] Exact(byte[] data, int expected)
        {
            if (data == null || data.Length != expected)
                throw new InvalidDataException("прочитано меньше данных, чем ожидалось");
            return data;
        }
    }
}
