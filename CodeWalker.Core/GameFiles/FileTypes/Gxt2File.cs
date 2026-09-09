using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeWalker.GameFiles
{
    [TypeConverter(typeof(ExpandableObjectConverter))] public class Gxt2File : PackedFile
    {
        public string Name { get; set; } = string.Empty;
        public RpfFileEntry? FileEntry { get; set; }
        public uint EntryCount { get; set; }
        public Gxt2Entry[] TextEntries { get; set; } = [];
        //public Dictionary<uint, string> Dict { get; set; }


        public void Load(byte[] data, RpfFileEntry? entry)
        {
            Name = entry?.Name ?? "";
            FileEntry = entry;
            //Dict = new Dictionary<uint, string>();

            using (BinaryReader br = new(new MemoryStream(data)))
            {
                uint gxt2 = br.ReadUInt32(); //"GXT2" - 1196971058
                if (gxt2 != 1196971058)
                { return; }

                EntryCount = br.ReadUInt32();
                TextEntries = new Gxt2Entry[EntryCount];
                for (uint i = 0; i < EntryCount; i++)
                {
                    var e = new Gxt2Entry();
                    e.Hash = br.ReadUInt32();
                    e.Offset = br.ReadUInt32();
                    TextEntries[i] = e;
                }

                gxt2 = br.ReadUInt32(); //another "GXT2"
                if (gxt2 != 1196971058)
                { return; }

                uint endpos = br.ReadUInt32();

                List<byte> buf = new();

                for (uint i = 0; i < EntryCount; i++)
                {
                    var e = TextEntries[i];
                    br.BaseStream.Position = e.Offset;

                    buf.Clear();
                    byte b = br.ReadByte();
                    while ((b != 0) && (br.BaseStream.Position<endpos))
                    {
                        buf.Add(b);
                        b = br.ReadByte();
                    }
                    e.Text = Encoding.UTF8.GetString(buf.ToArray());

                    //Dict[e.Hash] = e.Text;
                }

            }
        }
        public byte[] Save()
        {
            TextEntries ??= [];
            EntryCount = (uint)TextEntries.Length;
            int dataOffset = checked(16 + TextEntries.Length * 8);
            int size = dataOffset;
            foreach (var entry in TextEntries)
            {
                size = checked(size + Encoding.UTF8.GetByteCount(entry.Text.AsSpan()) + 1);
            }

            var data = new byte[size];
            BinaryPrimitives.WriteUInt32LittleEndian(data, 1196971058); // GXT2
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), EntryCount);
            int tableOffset = 8;
            foreach (var entry in TextEntries)
            {
                entry.Offset = (uint)dataOffset;
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(tableOffset), entry.Hash);
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(tableOffset + 4), entry.Offset);
                tableOffset += 8;
                dataOffset += Encoding.UTF8.GetBytes(entry.Text.AsSpan(), data.AsSpan(dataOffset));
                dataOffset++; // The output buffer already contains the null terminator.
            }
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(tableOffset), 1196971058);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(tableOffset + 4), (uint)size);
            return data;
        }


        public string ToText()
        {
            StringBuilder sb = new();
            if (TextEntries != null)
            {
                foreach (var entry in TextEntries)
                {
                    sb.Append("0x");
                    sb.Append(entry.Hash.ToString("X8"));
                    sb.Append(" = ");
                    sb.Append(entry.Text);
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }
        public static Gxt2File FromText(string? text)
        {
            var gxt = new Gxt2File();
            var span = text.AsSpan();
            var entries = new List<Gxt2Entry>();
            foreach (var range in span.Split('\n'))
            {
                var tline = span[range].Trim();
                if (tline.Length < 13) continue;
                if (uint.TryParse(tline.Slice(2, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hash))
                {
                    var entry = new Gxt2Entry();
                    entry.Hash = hash;
                    entry.Text = (tline.Length > 13) ? tline[13..].ToString() : "";
                    entries.Add(entry);
                }
                else
                {
                    //error parsing hash, probably should tell the user about this somehow
                }
            }
            entries.Sort((a, b) => a.Hash.CompareTo(b.Hash));
            gxt.TextEntries = entries.ToArray();
            gxt.EntryCount = (uint)entries.Count;
            return gxt;
        }
    
    }


    [TypeConverter(typeof(ExpandableObjectConverter))] public class Gxt2Entry
    {
        public uint Hash { get; set; }
        public uint Offset { get; set; }
        public string Text { get; set; } = string.Empty;

        public override string ToString()
        {
            return Convert.ToString(Hash, 16).ToUpper().PadLeft(8, '0') + ": " + Text;
        }
    }








    public static class GlobalText
    {
        public static Dictionary<uint, string> Index = new();
        private static readonly System.Threading.Lock syncRoot = new();

        public static volatile bool FullIndexBuilt = false;

        public static void Clear()
        {
            lock (syncRoot)
            {
                Index.Clear();
            }
        }

        public static bool Ensure(string str)
        {
            uint hash = JenkHash.GenHash(str);
            if (hash == 0) return true;
            lock (syncRoot)
            {
                return !Index.TryAdd(hash, str);
            }
        }

        public static bool Ensure(string str, uint hash)
        {
            if (hash == 0) return true;
            lock (syncRoot)
            {
                return !Index.TryAdd(hash, str);
            }
        }

        public static string GetString(uint hash)
        {
            string? res;
            lock (syncRoot)
            {
                if (!Index.TryGetValue(hash, out res))
                {
                    res = hash.ToString();
                }
            }
            return res;
        }
        public static string TryGetString(uint hash)
        {
            string? res;
            lock (syncRoot)
            {
                if (!Index.TryGetValue(hash, out res))
                {
                    res = string.Empty;
                }
            }
            return res;
        }

        public static uint TryFindHash(string text)
        {
            lock (syncRoot)
            {
                foreach (var kvp in Index)
                {
                    if (kvp.Value == text)
                    {
                        return kvp.Key;
                    }
                }
            }
            return 0;
        }

    }



}
