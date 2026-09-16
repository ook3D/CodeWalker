using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class ScalarReadTests
{
    [Theory]
    [InlineData(0, Endianess.LittleEndian)]
    [InlineData(0, Endianess.BigEndian)]
    [InlineData(1, Endianess.LittleEndian)]
    [InlineData(1, Endianess.BigEndian)]
    [InlineData(2, Endianess.LittleEndian)]
    [InlineData(2, Endianess.BigEndian)]
    public void ScalarsPreserveEndianAndResourceRoutingWithoutAllocations(int route, Endianess endian)
    {
        using var output = new MemoryStream();
        var writer = new DataWriter(output, endian);
        writer.Write((byte)123);
        writer.Write((short)-1234);
        writer.Write(-123456);
        writer.Write(-1234567890123L);
        writer.Write((ushort)54321);
        writer.Write(3456789012u);
        writer.Write(12345678901234567890ul);
        writer.Write(1.25f);
        writer.Write(-2.5d);

        using var input = new PartialReadStream(output.ToArray());
        using var unused = new MemoryStream();
        DataReader reader = route switch
        {
            1 => new ResourceDataReader(input, unused, endian),
            2 => new ResourceDataReader(unused, input, endian),
            _ => new DataReader(input, endian)
        };
        long address = route == 1 ? 0x50000000 : route == 2 ? 0x60000000 : 0;
        reader.Position = address;
        var expected = ((byte)123, (short)-1234, -123456, -1234567890123L,
            (ushort)54321, 3456789012u, 12345678901234567890ul, 1.25f, -2.5d);
        Assert.Equal(expected, ReadValues(reader));
        Assert.Equal(address + output.Length, reader.Position);
        Assert.Equal(0, unused.Position);

        for (int i = 0; i < 1000; i++) { reader.Position = address; _ = ReadValues(reader); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) { reader.Position = address; _ = ReadValues(reader); }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    private static (byte, short, int, long, ushort, uint, ulong, float, double) ReadValues(DataReader reader) =>
        (reader.ReadByte(), reader.ReadInt16(), reader.ReadInt32(), reader.ReadInt64(), reader.ReadUInt16(),
            reader.ReadUInt32(), reader.ReadUInt64(), reader.ReadSingle(), reader.ReadDouble());

    private sealed class PartialReadStream(byte[] data) : MemoryStream(data)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(1, buffer.Length)]);
    }
}
