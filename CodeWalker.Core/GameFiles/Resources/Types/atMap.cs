using System.ComponentModel;

namespace CodeWalker.GameFiles
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class atMapEntry : ResourceSystemBlock
    {
        public override long BlockLength => 16;

        public uint Key { get; set; }
        public int Data { get; set; }
        public ulong NextPointer { get; set; }

        public atMapEntry? Next { get; set; }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            Key = reader.ReadUInt32();
            Data = reader.ReadInt32();
            NextPointer = reader.ReadUInt64();
            Next = reader.ReadBlockAt<atMapEntry>(NextPointer);
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            NextPointer = (ulong)(Next?.FilePosition ?? 0);
            writer.Write(Key);
            writer.Write(Data);
            writer.Write(NextPointer);
        }

        public override IResourceBlock[] GetReferences() => Next is null ? [] : [Next];

        public override string ToString() => $"{Key}: {Data}";
    }
}
