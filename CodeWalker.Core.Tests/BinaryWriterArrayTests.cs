using System.Buffers.Binary;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class BinaryWriterArrayTests
{
    public static IEnumerable<object[]> WriterCases()
    {
        for (int route = 0; route < 3; route++)
        foreach (var endian in new[] { Endianess.LittleEndian, Endianess.BigEndian })
        for (int kind = 0; kind < 9; kind++)
            yield return [route, endian, kind];
    }

    [Theory]
    [MemberData(nameof(WriterCases))]
    public void NumericWritesPreserveBytesAndRoute(int route, Endianess endian, int kind)
    {
        using var output = new MemoryStream();
        using var unused = new MemoryStream();
        DataWriter writer = route switch
        {
            1 => new ResourceDataWriter(output, unused, endian),
            2 => new ResourceDataWriter(unused, output, endian),
            _ => new DataWriter(output, endian)
        };
        long start = route == 0 ? 0 : route == 1 ? 0x50000000 : 0x60000000;
        writer.Position = start + 3;
        byte[] expected;
        switch (kind)
        {
            case 0: writer.Write((byte)0xef); expected = [0xef]; break;
            case 1: writer.Write((short)-12345); expected = BitConverter.GetBytes((short)-12345); break;
            case 2: writer.Write(-123456789); expected = BitConverter.GetBytes(-123456789); break;
            case 3: writer.Write(long.MinValue + 12345); expected = BitConverter.GetBytes(long.MinValue + 12345); break;
            case 4: writer.Write((ushort)0xabcd); expected = BitConverter.GetBytes((ushort)0xabcd); break;
            case 5: writer.Write(0x89abcdefu); expected = BitConverter.GetBytes(0x89abcdefu); break;
            case 6: writer.Write(0x123456789abcdef0ul); expected = BitConverter.GetBytes(0x123456789abcdef0ul); break;
            case 7: writer.Write(BitConverter.Int32BitsToSingle(unchecked((int)0xffc12345))); expected = BitConverter.GetBytes(0xffc12345u); break;
            default: writer.Write(-0.0d); expected = BitConverter.GetBytes(-0.0d); break;
        }
        if (endian == Endianess.BigEndian) Array.Reverse(expected);
        Assert.Equal(expected, output.ToArray()[3..]);
        Assert.Equal(start + 3 + expected.Length, writer.Position);
        Assert.Equal(0, unused.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(1024)]
    public void ChunkedStringsPreserveLowBytesAndTerminator(int length)
    {
        string value = new(Enumerable.Range(0, length).Select(i => (char)(i % 3 == 0 ? 0x1234 : i)).ToArray());
        using var system = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(system, graphics, Endianess.BigEndian) { Position = 0x60000000 };
        writer.Write(value);
        Assert.Equal(value.Select(c => (byte)c).Append((byte)0), graphics.ToArray());
        Assert.Equal(0x60000000 + length + 1, writer.Position);
        Assert.Equal(0, system.Length);
    }

    [Fact]
    public void ByteArraysAndSpansRemainUnchangedInBigEndianMode()
    {
        byte[] bytes = [1, 2, 3, 4];
        using var output = new MemoryStream();
        var writer = new DataWriter(output, Endianess.BigEndian);
        writer.Write(bytes);
        writer.Write(bytes.AsSpan(1, 2));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 2, 3 }, output.ToArray());
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes);
    }

    public static IEnumerable<object[]> ArrayCases()
    {
        foreach (bool graphics in new[] { false, true })
        foreach (bool cache in new[] { false, true })
        foreach (var endian in new[] { Endianess.LittleEndian, Endianess.BigEndian })
        for (int kind = 0; kind < 5; kind++)
            yield return [graphics, cache, endian, kind];
    }

    [Theory]
    [MemberData(nameof(ArrayCases))]
    public void PrimitiveArraysPreserveRawLayoutAndCache(bool graphics, bool cache, Endianess endian, int kind)
    {
        byte[] bytes = Enumerable.Range(0, 35).Select(i => (byte)i).ToArray();
        using var input = new PartialReadStream(bytes);
        using var unused = new MemoryStream();
        var reader = graphics ? new ResourceDataReader(unused, input, endian) : new ResourceDataReader(input, unused, endian);
        ulong address = (graphics ? 0x60000000ul : 0x50000000ul) + 3;
        reader.Position = 0x50000001;
        Array result = kind switch
        {
            0 => Assert.IsType<ushort[]>(reader.ReadUshortsAt(address, 4, cache)),
            1 => Assert.IsType<short[]>(reader.ReadShortsAt(address, 4, cache)),
            2 => Assert.IsType<uint[]>(reader.ReadUintsAt(address, 4, cache)),
            3 => Assert.IsType<ulong[]>(reader.ReadUlongsAt(address, 4, cache)),
            _ => Assert.IsType<float[]>(reader.ReadFloatsAt(address, 4, cache))
        };
        var actual = new byte[Buffer.ByteLength(result)];
        Buffer.BlockCopy(result, 0, actual, 0, actual.Length);
        Assert.Equal(bytes[3..(3 + actual.Length)], actual);
        Assert.Equal(0x50000001, reader.Position);
        Assert.Equal(cache, reader.arrayPool.ContainsKey((long)address));
        if (cache) Assert.Same(result, reader.arrayPool[(long)address]);
    }

    [Fact]
    public void FailedArrayReadRestoresPositionAndDoesNotCachePartialData()
    {
        using var input = new PartialReadStream([1, 2, 3]);
        using var unused = new MemoryStream();
        var reader = new ResourceDataReader(input, unused) { Position = 0x60000000 };
        Assert.Throws<EndOfStreamException>(() => reader.ReadUintsAt(0x50000000, 1));
        Assert.Equal(0x60000000, reader.Position);
        Assert.Empty(reader.arrayPool);
        Assert.Throws<OverflowException>(() => reader.ReadUintsAt(0x50000000, uint.MaxValue));
        Assert.Null(reader.ReadUintsAt(0, 1));
        Assert.Null(reader.ReadUintsAt(0x50000000, 0));
    }

    [Fact]
    public void MovementSizingPassStillCountsEveryWrite()
    {
        var movement = new MrfFile { UnkBytes = [1, 2, 3], UnkBytesCount = 3 };
        byte[] bytes = movement.Save();
        Assert.Equal(43, bytes.Length);
        Assert.Equal(8u, movement.DataLength);
        Assert.Equal(8u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(20)));
        Assert.Equal(new byte[] { 1, 2, 3 }, bytes[^3..]);
    }

    private sealed class PartialReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(3, buffer.Length)]);
    }
}
