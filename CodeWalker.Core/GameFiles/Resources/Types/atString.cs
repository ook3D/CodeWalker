using System.ComponentModel;

namespace CodeWalker.GameFiles
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class atString : ResourceSystemBlock
    {
        public override long BlockLength => 16;

        public ulong DataPointer { get; private set; }
        public ushort Length { get; private set; }
        public ushort Allocated { get; private set; }

        public string Value { get; set; } = string.Empty;

        private string_r? dataBlock;

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            DataPointer = reader.ReadUInt64();
            Length = reader.ReadUInt16();
            Allocated = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            Value = reader.ReadStringAt(DataPointer) ?? string.Empty;
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            DataPointer = (ulong)(dataBlock?.FilePosition ?? 0);
            Length = checked((ushort)Value.Length);
            Allocated = Length == 0 ? (ushort)0 : checked((ushort)(Length + 1));

            writer.Write(DataPointer);
            writer.Write(Length);
            writer.Write(Allocated);
            writer.Write(0u);
        }

        public override IResourceBlock[] GetReferences()
        {
            dataBlock = Value.Length == 0 ? null : (string_r)Value;
            return dataBlock is null ? [] : [dataBlock];
        }

        public static explicit operator string(atString value) => value.Value;
        public static explicit operator atString(string value) => new() { Value = value };

        public override string ToString() => Value;
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class string_r : ResourceSystemBlock
    {
        public override long BlockLength => Value.Length + 1;

        public string Value { get; set; } = string.Empty;

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            Value = reader.ReadString();
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            writer.Write(Value);
        }

        public static explicit operator string(string_r value) => value.Value;
        public static explicit operator string_r(string value) => new() { Value = value };

        public override string ToString() => Value;
    }
}
