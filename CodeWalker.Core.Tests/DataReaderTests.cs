using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class DataReaderTests
{
    public static IEnumerable<object[]> PrimitiveCases()
    {
        for (int route = 0; route < 3; route++)
        foreach (var endian in new[] { Endianess.LittleEndian, Endianess.BigEndian })
        for (int kind = 0; kind < 7; kind++)
            yield return [route, endian, kind];
    }

    [Theory]
    [MemberData(nameof(PrimitiveCases))]
    public async Task AllReadPathsFillPartialReadsAndRespectRouting(int route, Endianess endian, int kind)
    {
        byte[] bytes = [0x12, 0x34, 0x56, 0x3f, 0x78, 0x9a, 0xbc, 0x3f];
        int size = kind == 0 ? 2 : kind is 1 or 2 or 5 ? 4 : 8;
        using var stream = new PartialReadStream(bytes);
        using var unused = new MemoryStream();
        DataReader reader = route switch
        {
            1 => new ResourceDataReader(stream, unused, endian),
            2 => new ResourceDataReader(unused, stream, endian),
            _ => new DataReader(stream, endian)
        };
        long start = route == 0 ? 0 : route == 1 ? 0x50000000 : 0x60000000;
        var expectedBytes = bytes[..size];
        if (endian == Endianess.BigEndian) Array.Reverse(expectedBytes);
        object expected = kind switch
        {
            0 => (object)BitConverter.ToInt16(expectedBytes),
            1 => BitConverter.ToInt32(expectedBytes),
            2 => BitConverter.ToUInt32(expectedBytes),
            3 => BitConverter.ToInt64(expectedBytes),
            4 => BitConverter.ToUInt64(expectedBytes),
            5 => BitConverter.ToSingle(expectedBytes),
            _ => BitConverter.ToDouble(expectedBytes)
        };
        reader.Position = start;
        Assert.Equal(expected, ReadScalar(reader, kind));
        Assert.Equal(start + size, reader.Position);
        reader.Position = start;
        Assert.Equal(expected, ReadSpan(reader, kind, size));
        Assert.Equal(start + size, reader.Position);
        reader.Position = start;
        Assert.Equal(expected, await ReadAsync(reader, kind));
        Assert.Equal(start + size, reader.Position);
        reader.Position = start;
        Assert.Equal(bytes, reader.ReadBytes(bytes.Length));
        reader.Position = start;
        Assert.Equal(bytes, await reader.ReadBytesAsync(bytes.Length));
    }

    [Theory]
    [MemberData(nameof(PrimitiveCases))]
    public async Task TruncatedPrimitivesThrowInsteadOfReturningPartialData(int route, Endianess endian, int kind)
    {
        using var stream = new PartialReadStream([0x12]);
        using var unused = new MemoryStream();
        DataReader reader = route switch
        {
            1 => new ResourceDataReader(stream, unused, endian),
            2 => new ResourceDataReader(unused, stream, endian),
            _ => new DataReader(stream, endian)
        };
        long start = route == 0 ? 0 : route == 1 ? 0x50000000 : 0x60000000;
        reader.Position = start;
        Assert.Throws<EndOfStreamException>(() => ReadScalar(reader, kind));
        Assert.Equal(start + 1, reader.Position);
        reader.Position = start;
        Assert.Throws<EndOfStreamException>(() => ReadSpan(reader, kind, 8));
        reader.Position = start;
        await Assert.ThrowsAsync<EndOfStreamException>(() => ReadAsync(reader, kind));
        Assert.Equal(start + 1, reader.Position);
    }

    [Fact]
    public async Task OneResourceReaderCanSwitchStreamsAtNonzeroOffsets()
    {
        using var system = new PartialReadStream([0, 0x12, 0x34]);
        using var graphics = new PartialReadStream([0, 0, 0x56, 0x78]);
        var reader = new ResourceDataReader(system, graphics, Endianess.BigEndian) { Position = 0x50000001 };
        Assert.Equal(0x1234, reader.ReadInt16());
        reader.Position = 0x60000002;
        Assert.Equal(0x5678, await reader.ReadInt16Async());
        Assert.Equal(0x60000004, reader.Position);
        reader.Position = 0x50000001;
        Assert.Equal(0x1234, reader.ReadInt16(new byte[2]));
        Assert.Equal(0x50000003, reader.Position);
    }

    [Fact]
    public async Task ByteReadsRejectTruncationAndHonorCancellation()
    {
        using var stream = new PartialReadStream([1, 2, 3]);
        var reader = new DataReader(stream);
        Assert.Throws<EndOfStreamException>(() => reader.ReadBytes(4));
        reader.Position = 0;
        await Assert.ThrowsAsync<EndOfStreamException>(() => reader.ReadBytesAsync(4));
        reader.Position = 0;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadBytesAsync(3, cancellation.Token));
        Assert.Equal(0, reader.Position);
    }

    [Fact]
    public async Task CancellationDoesNotAdvanceResourcePosition()
    {
        using var system = new PartialReadStream(new byte[8]);
        using var graphics = new MemoryStream();
        var reader = new ResourceDataReader(system, graphics) { Position = 0x50000000 };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadInt32Async(cancellation.Token));
        Assert.Equal(0x50000000, reader.Position);
        Assert.Equal(0, system.Position);
    }

    [Fact]
    public async Task EmptyReadsAndInvalidResourceAddressesAreHandled()
    {
        using var system = new MemoryStream();
        using var graphics = new MemoryStream();
        var reader = new ResourceDataReader(system, graphics) { Position = 0x50000000 };
        Assert.Empty(reader.ReadBytes(0));
        Assert.Empty(await reader.ReadBytesAsync(0));
        Assert.Throws<EndOfStreamException>(() => reader.ReadByte());
        Assert.Throws<EndOfStreamException>(() => reader.ReadString());
        reader.Position = 0;
        Assert.Throws<InvalidDataException>(() => reader.ReadInt32());
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadInt32Async());
    }

    private static object ReadScalar(DataReader reader, int kind) => kind switch
    {
        0 => (object)reader.ReadInt16(), 1 => reader.ReadInt32(), 2 => reader.ReadUInt32(),
        3 => reader.ReadInt64(), 4 => reader.ReadUInt64(), 5 => reader.ReadSingle(), _ => reader.ReadDouble()
    };

    private static object ReadSpan(DataReader reader, int kind, int size)
    {
        Span<byte> buffer = stackalloc byte[12];
        buffer.Fill(0xcc);
        object result = kind switch
        {
            0 => (object)reader.ReadInt16(buffer), 1 => reader.ReadInt32(buffer), 2 => reader.ReadUInt32(buffer),
            3 => reader.ReadInt64(buffer), 4 => reader.ReadUInt64(buffer),
            5 => reader.ReadSingle(buffer), _ => reader.ReadDouble(buffer)
        };
        foreach (byte value in buffer[size..]) Assert.Equal(0xcc, value);
        return result;
    }

    private static async Task<object> ReadAsync(DataReader reader, int kind) => kind switch
    {
        0 => (object)await reader.ReadInt16Async(), 1 => await reader.ReadInt32Async(),
        2 => await reader.ReadUInt32Async(), 3 => await reader.ReadInt64Async(),
        4 => await reader.ReadUInt64Async(), 5 => await reader.ReadSingleAsync(), _ => await reader.ReadDoubleAsync()
    };

    // A valid stream may return fewer bytes than requested even before EOF.
    private sealed class PartialReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 1));
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 1)]);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return Read(buffer.Span);
        }
    }
}
