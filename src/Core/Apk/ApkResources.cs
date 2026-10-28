using System.IO;
using System.Text;

namespace TVCleaner.Core
{
    /// <summary>ResStringPool chunk (UTF-8 or UTF-16 strings).</summary>
    internal sealed class StringPool
    {
        private readonly byte[] b;
        private readonly uint[] offsets;
        private readonly int stringsStart;
        private readonly bool utf8;

        public StringPool(byte[] b, int chunkStart)
        {
            this.b = b;
            int headerSize = Bin.U16(b, chunkStart + 2);
            int count = (int)Bin.U32(b, chunkStart + 8);
            uint flags = Bin.U32(b, chunkStart + 16);
            stringsStart = chunkStart + (int)Bin.U32(b, chunkStart + 20);
            utf8 = (flags & 0x100) != 0;
            offsets = new uint[count];
            for (int i = 0; i < count; i++) offsets[i] = Bin.U32(b, chunkStart + headerSize + i * 4);
        }

        public string Get(long index)
        {
            if (index < 0 || index >= offsets.Length) return null;
            int p = stringsStart + (int)offsets[index];
            if (utf8)
            {
                p += (b[p] & 0x80) != 0 ? 2 : 1; // length in UTF-16 units, not needed
                int length = b[p];
                if ((length & 0x80) != 0)
                {
                    length = ((length & 0x7f) << 8) | b[p + 1];
                    p += 2;
                }
                else
                {
                    p += 1;
                }
                return Encoding.UTF8.GetString(b, p, length);
            }
            int units = Bin.U16(b, p);
            p += 2;
            if ((units & 0x8000) != 0)
            {
                units = ((units & 0x7fff) << 16) | Bin.U16(b, p);
                p += 2;
            }
            return Encoding.Unicode.GetString(b, p, units * 2);
        }
    }

    /// <summary>Finds android:label of &lt;application&gt; in a compiled AndroidManifest.xml.</summary>
    public static class BinaryXml
    {
        private const uint AndroidLabelAttr = 0x01010001;
        private const int TypeReference = 0x01;
        private const int TypeString = 0x03;

        /// <summary>True if a label was found: either a literal string or a resource id to resolve in resources.arsc.</summary>
        public static bool FindApplicationLabel(byte[] xml, out string literal, out uint resourceId)
        {
            literal = null;
            resourceId = 0;
            if (xml.Length < 8 || Bin.U16(xml, 0) != 0x0003) throw new InvalidDataException("не бинарный XML");
            StringPool strings = null;
            uint[] resourceMap = null;
            int p = Bin.U16(xml, 2);
            while (p + 8 <= xml.Length)
            {
                int type = Bin.U16(xml, p);
                int headerSize = Bin.U16(xml, p + 2);
                int size = (int)Bin.U32(xml, p + 4);
                if (size <= 0) break;
                if (type == 0x0001)
                {
                    strings = new StringPool(xml, p);
                }
                else if (type == 0x0180)
                {
                    resourceMap = new uint[(size - headerSize) / 4];
                    for (int i = 0; i < resourceMap.Length; i++) resourceMap[i] = Bin.U32(xml, p + headerSize + i * 4);
                }
                else if (type == 0x0102 && strings != null)
                {
                    int ext = p + headerSize;
                    if (strings.Get(Bin.U32(xml, ext + 4)) == "application")
                        return ReadLabel(xml, ext, strings, resourceMap, out literal, out resourceId);
                }
                p += size;
            }
            return false;
        }

        private static bool ReadLabel(byte[] xml, int ext, StringPool strings, uint[] resourceMap,
            out string literal, out uint resourceId)
        {
            literal = null;
            resourceId = 0;
            int attrStart = Bin.U16(xml, ext + 8);
            int attrSize = Bin.U16(xml, ext + 10);
            int attrCount = Bin.U16(xml, ext + 12);
            for (int i = 0; i < attrCount; i++)
            {
                int a = ext + attrStart + i * attrSize;
                uint name = Bin.U32(xml, a + 4);
                bool isLabel = (resourceMap != null && name < resourceMap.Length && resourceMap[name] == AndroidLabelAttr)
                               || strings.Get(name) == "label";
                if (!isLabel) continue;
                uint raw = Bin.U32(xml, a + 8);
                int dataType = xml[a + 15];
                uint data = Bin.U32(xml, a + 16);
                if (dataType == TypeReference)
                {
                    resourceId = data;
                    return true;
                }
                literal = dataType == TypeString ? strings.Get(data) : raw != 0xFFFFFFFF ? strings.Get(raw) : null;
                return literal != null;
            }
            return false;
        }
    }

    /// <summary>Resolves a string resource id in resources.arsc, preferring the given language.</summary>
    public static class ResourceTable
    {
        private const int TypeReference = 0x01;
        private const int TypeString = 0x03;

        public static string ResolveString(byte[] arsc, uint resourceId, string language)
        {
            if (arsc.Length < 12 || Bin.U16(arsc, 0) != 0x0002) throw new InvalidDataException("не таблица ресурсов");
            for (int depth = 0; depth < 4; depth++) // a label may point to another string resource
            {
                int dataType;
                uint data;
                StringPool values;
                if (!Lookup(arsc, resourceId, language, out dataType, out data, out values)) return null;
                if (dataType == TypeString) return values != null ? values.Get(data) : null;
                if (dataType != TypeReference) return null;
                resourceId = data;
            }
            return null;
        }

        private static bool Lookup(byte[] t, uint resourceId, string language,
            out int dataType, out uint data, out StringPool values)
        {
            dataType = 0;
            data = 0;
            values = null;
            int packageId = (int)(resourceId >> 24);
            int typeId = (int)((resourceId >> 16) & 0xff);
            int entryIndex = (int)(resourceId & 0xffff);
            int bestRank = -1;

            int p = Bin.U16(t, 2);
            while (p + 8 <= t.Length)
            {
                int type = Bin.U16(t, p);
                int headerSize = Bin.U16(t, p + 2);
                int size = (int)Bin.U32(t, p + 4);
                if (size <= 0) break;
                if (type == 0x0001)
                {
                    values = new StringPool(t, p);
                }
                else if (type == 0x0200 && (int)Bin.U32(t, p + 8) == packageId)
                {
                    int end = p + size;
                    int q = p + headerSize;
                    while (q + 8 <= end)
                    {
                        int chunkType = Bin.U16(t, q);
                        int chunkHeader = Bin.U16(t, q + 2);
                        int chunkSize = (int)Bin.U32(t, q + 4);
                        if (chunkSize <= 0) break;
                        if (chunkType == 0x0201 && t[q + 8] == typeId)
                        {
                            int rank;
                            int dt;
                            uint d;
                            if (ReadEntry(t, q, chunkHeader, entryIndex, language, out rank, out dt, out d) && rank > bestRank)
                            {
                                bestRank = rank;
                                dataType = dt;
                                data = d;
                            }
                        }
                        q += chunkSize;
                    }
                }
                p += size;
            }
            return bestRank >= 0;
        }

        /// <summary>Reads one entry of a ResTable_type chunk (dense or sparse) and ranks its configuration.</summary>
        private static bool ReadEntry(byte[] t, int chunk, int headerSize, int entryIndex, string language,
            out int rank, out int dataType, out uint data)
        {
            rank = -1;
            dataType = 0;
            data = 0;
            int flags = t[chunk + 9];
            int entryCount = (int)Bin.U32(t, chunk + 12);
            int entriesStart = (int)Bin.U32(t, chunk + 16);
            int offset = -1;
            if ((flags & 0x01) != 0) // sparse: (u16 index, u16 offset/4) pairs
            {
                for (int i = 0; i < entryCount; i++)
                {
                    if (Bin.U16(t, chunk + headerSize + i * 4) == entryIndex)
                    {
                        offset = Bin.U16(t, chunk + headerSize + i * 4 + 2) * 4;
                        break;
                    }
                }
            }
            else if (entryIndex < entryCount)
            {
                uint o = Bin.U32(t, chunk + headerSize + entryIndex * 4);
                if (o != 0xFFFFFFFF) offset = (int)o;
            }
            if (offset < 0) return false;

            int entry = chunk + entriesStart + offset;
            if ((Bin.U16(t, entry + 2) & 0x0001) != 0) return false; // complex (bag) entry, not a string
            int value = entry + Bin.U16(t, entry);
            dataType = t[value + 3];
            data = Bin.U32(t, value + 4);

            int config = chunk + 20;
            char l0 = (char)t[config + 8], l1 = (char)t[config + 9];
            if (language != null && language.Length == 2 && l0 == language[0] && l1 == language[1]) rank = 3;
            else if (l0 == 0 && l1 == 0) rank = 2;
            else if (l0 == 'e' && l1 == 'n') rank = 1;
            else rank = 0;
            return true;
        }
    }
}
