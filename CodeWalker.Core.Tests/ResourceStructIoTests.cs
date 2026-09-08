using System.Runtime.InteropServices;
using CodeWalker.GameFiles;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class ResourceStructIoTests
{
    [Theory]
    [InlineData(false, Endianess.LittleEndian)]
    [InlineData(false, Endianess.BigEndian)]
    [InlineData(true, Endianess.LittleEndian)]
    [InlineData(true, Endianess.BigEndian)]
    public void MatrixArraysMatchMarshaledBytesAndPreserveRouting(bool graphics, Endianess endian)
    {
        Matrix[] matrices = [Matrix.Identity, Matrix.Translation(1, -2, 3), Matrix.Scaling(4, 5, 6)];
        byte[] expected = matrices.SelectMany(MarshalBytes).ToArray();
        using var output = new MemoryStream();
        using var unused = new MemoryStream();
        var writer = graphics ? new ResourceDataWriter(unused, output, endian) : new ResourceDataWriter(output, unused, endian);
        long address = graphics ? 0x60000003 : 0x50000003;
        writer.Position = address;
        writer.WriteStructs(matrices);
        Assert.Equal(expected, output.ToArray()[3..]);
        Assert.Equal(address + expected.Length, writer.Position);
        Assert.Equal(0, unused.Length);

        using var input = new PartialReadStream(output.ToArray());
        var reader = graphics ? new ResourceDataReader(unused, input, endian) : new ResourceDataReader(input, unused, endian);
        reader.Position = 0x50000001;
        var loaded = reader.ReadStructsAt<Matrix>((ulong)address, 3);
        Assert.Equal(matrices, loaded);
        Assert.Equal(0x50000001, reader.Position);
        Assert.Same(loaded, reader.arrayPool[address]);
        Assert.Equal(matrices[1], reader.ReadStructAt<Matrix>(address + 64));
        Assert.Equal(0x50000001, reader.Position);
        reader.Position = address;
        Assert.Equal(matrices, reader.ReadStructs<Matrix>(3));
        Assert.Equal(address + expected.Length, reader.Position);
    }

    [Fact]
    public void PackedAndNestedRecordsMatchTheirNativeLayout()
    {
        PackedRecord[] records =
        [
            new() { Tag = 1, Count = -2, Position = new Vector3(1, 2, 3) },
            new() { Tag = 255, Count = short.MaxValue, Position = new Vector3(-4, 5, -6) }
        ];
        RoundTrip(records, (expected, actual) => Assert.Equal(expected, actual));
        RoundTrip(new[] { new PsoChar32("particle"), new PsoChar32("cloth") },
            (expected, actual) => Assert.Equal(expected, actual));
    }

    [Fact]
    public void BooleanFieldsUseNativeConversionEvenWhenSizesMatch()
    {
        BooleanRecord[] records = [new() { Enabled = true, Count = 12 }, new() { Enabled = false, Count = -1 }];
        RoundTrip(records, (expected, actual) => Assert.Equal(expected, actual));

        // A native BOOL accepts any nonzero 32-bit value, including one whose low byte is zero.
        using var input = new MemoryStream(new byte[] { 0, 1, 0, 0, 12, 0, 0, 0 });
        using var unused = new MemoryStream();
        var reader = new ResourceDataReader(input, unused) { Position = 0x50000000 };
        var value = Assert.Single(reader.ReadStructs<BooleanRecord>(1));
        Assert.True(value.Enabled);
        Assert.Equal(12, value.Count);
    }

    [Fact]
    public void InlineArraysAndMarshalAttributesKeepTheirWireRepresentation()
    {
        InlineRecord[] records =
        [
            new() { Enabled = true, Values = [1, 2, 65535] },
            new() { Enabled = false, Values = [3, 4, 5] }
        ];
        RoundTrip(records, (expected, actual) =>
        {
            Assert.Equal(expected.Enabled, actual.Enabled);
            Assert.Equal(expected.Values, actual.Values);
        });
    }

    [Fact]
    public void TruncatedAndOversizedReadsRestorePositionWithoutCaching()
    {
        using var input = new PartialReadStream(new byte[63]);
        using var unused = new MemoryStream();
        var reader = new ResourceDataReader(input, unused) { Position = 0x60000000 };
        Assert.Throws<EndOfStreamException>(() => reader.ReadStructsAt<Matrix>(0x50000000, 1));
        Assert.Equal(0x60000000, reader.Position);
        Assert.Throws<EndOfStreamException>(() => reader.ReadStructAt<Matrix>(0x50000000));
        Assert.Equal(0x60000000, reader.Position);
        Assert.Throws<OverflowException>(() => reader.ReadStructsAt<Matrix>(0x50000000, uint.MaxValue));
        Assert.Equal(0x60000000, reader.Position);
        Assert.Empty(reader.arrayPool);
        Assert.Null(reader.ReadStructsAt<Matrix>(0, 1));
        Assert.Null(reader.ReadStructsAt<Matrix>(0x50000000, 0));
        Assert.Empty(reader.ReadStructs<Matrix>(0));
    }

    private static void RoundTrip<T>(T[] values, Action<T, T> compare) where T : struct
    {
        byte[] expected = values.SelectMany(MarshalBytes).ToArray();
        using var output = new MemoryStream();
        using var unused = new MemoryStream();
        var writer = new ResourceDataWriter(output, unused) { Position = 0x50000000 };
        writer.WriteStructs(values);
        Assert.Equal(expected, output.ToArray());
        writer.Position = 0x50000000;
        foreach (var value in values) writer.WriteStruct(value);
        Assert.Equal(expected, output.ToArray());
        using var input = new PartialReadStream(expected);
        var reader = new ResourceDataReader(input, unused) { Position = 0x50000000 };
        var loaded = reader.ReadStructs<T>((uint)values.Length);
        for (int i = 0; i < values.Length; i++) compare(values[i], loaded[i]);
        Assert.Equal(0x50000000 + expected.Length, reader.Position);
    }

    // Independent wire-format oracle matching the previous marshaling implementation.
    private static byte[] MarshalBytes<T>(T value) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        IntPtr pointer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(value, pointer, false);
            try
            {
                var result = new byte[size];
                Marshal.Copy(pointer, result, 0, size);
                return result;
            }
            finally { Marshal.DestroyStructure<T>(pointer); }
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct PackedRecord
    {
        public byte Tag;
        public short Count;
        public Vector3 Position;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BooleanRecord
    {
        public bool Enabled;
        public int Count;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct InlineRecord
    {
        [MarshalAs(UnmanagedType.U1)] public bool Enabled;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public ushort[] Values;
    }

    private sealed class PartialReadStream(byte[] data) : MemoryStream(data)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(5, buffer.Length)]);
    }
}
