using System;
using System.ComponentModel;
using System.IO;

namespace CodeWalker.GameFiles
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class atBitSet : ResourceSystemBlock
    {
        public override long BlockLength => 16;

        public ulong BitsPointer { get; private set; }
        public ushort WordCount { get; private set; }
        public ushort BitCount { get; set; }
        public uint[] Words { get; set; } = [];

        private ResourceSystemStructBlock<uint>? wordsBlock;

        public atBitSet()
        {
        }

        public atBitSet(int bitCount)
        {
            if ((uint)bitCount > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(bitCount));
            BitCount = (ushort)bitCount;
            Words = new uint[(bitCount + 31) >> 5];
        }

        public bool this[int index]
        {
            get
            {
                if ((uint)index >= BitCount) throw new ArgumentOutOfRangeException(nameof(index));
                return (Words[index >> 5] & (1u << (index & 31))) != 0;
            }
            set
            {
                if ((uint)index >= BitCount) throw new ArgumentOutOfRangeException(nameof(index));
                if (value) Words[index >> 5] |= 1u << (index & 31);
                else Words[index >> 5] &= ~(1u << (index & 31));
            }
        }

        public override void Read(ResourceDataReader reader, params object[] parameters)
        {
            BitsPointer = reader.ReadUInt64();
            WordCount = reader.ReadUInt16();
            BitCount = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            Words = reader.ReadUintsAt(BitsPointer, WordCount) ?? [];
        }

        public override void Write(ResourceDataWriter writer, params object[] parameters)
        {
            var requiredWords = (BitCount + 31) >> 5;
            if (requiredWords != Words.Length)
                throw new InvalidDataException($"A {BitCount}-bit atBitSet requires {requiredWords} words, but has {Words.Length}.");

            BitsPointer = (ulong)(wordsBlock?.FilePosition ?? 0);
            WordCount = checked((ushort)Words.Length);
            writer.Write(BitsPointer);
            writer.Write(WordCount);
            writer.Write(BitCount);
            writer.Write(0u);
        }

        public override IResourceBlock[] GetReferences()
        {
            wordsBlock = Words.Length == 0 ? null : new ResourceSystemStructBlock<uint>(Words);
            return wordsBlock == null ? [] : [wordsBlock];
        }

        public override string ToString() => $"{BitCount} bits";
    }
}
