using System.Buffers.Binary;
using System.Security.Cryptography;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class ResourceBuilderOutputTests
{
    public static IEnumerable<object[]> PageCases()
    {
        foreach (bool gen9 in new[] { false, true })
        foreach (int size in new[] { 0, 1, 4095, 4096, 4097, 65537 })
            yield return [gen9, size];
    }

    [Theory]
    [MemberData(nameof(PageCases))]
    public void StreamingCompressionPreservesHeaderPayloadAndPagePadding(bool gen9, int size)
    {
        var root = new TestRoot(size);
        byte[] raw = ResourceBuilder.Build(root, 37, false, gen9);
        byte[] compressed = ResourceBuilder.Build(root, 37, true, gen9);
        Assert.Equal(0x37435352u, BinaryPrimitives.ReadUInt32LittleEndian(raw));
        Assert.Equal(37, BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(4)));
        Assert.Equal(raw[..16], compressed[..16]);
        Assert.Equal(raw[16..], ResourceBuilder.Decompress(compressed[16..]));
        // Compare against the previous single-buffer compression path on this runtime.
        Assert.Equal(ResourceBuilder.Compress(raw[16..]), compressed[16..]);

        var systemFlags = new RpfResourcePageFlags(BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(8)));
        var graphicsFlags = new RpfResourcePageFlags(BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(12)));
        int systemSize = checked((int)systemFlags.Size);
        int graphicsSize = checked((int)graphicsFlags.Size);
        Assert.Equal(16 + systemSize + graphicsSize, raw.Length);
        Assert.Equal(2u, systemFlags.Value >> 28);
        Assert.Equal(5u, graphicsFlags.Value >> 28);

        if (root.Graphics is { } graphics)
        {
            int offset = 16 + systemSize + checked((int)(graphics.FilePosition - 0x60000000));
            Assert.Equal(graphics.Bytes, raw[offset..(offset + size)]);
            Assert.All(raw[(offset + size)..], value => Assert.Equal((byte)0, value));
        }
        else Assert.Equal(0, graphicsSize);

        var pages = Assert.IsType<ResourcePagesInfo>(root.FilePagesInfo);
        int systemEnd = 16 + checked((int)(pages.FilePosition - 0x50000000 + pages.BlockLength));
        Assert.All(raw[systemEnd..(16 + systemSize)], value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void LargeUncompressedResourceMatchesPreOptimizationOutput()
    {
        byte[] raw = ResourceBuilder.Build(new TestRoot(2 * 1024 * 1024 + 17), 37, false);
        Assert.Equal(4325392, raw.Length);
        Assert.Equal("7B08DF066F429748A82D438290358CD584558D9912A70BBD696E8C415B8F220E",
            Convert.ToHexString(SHA256.HashData(raw)));
    }

    [Fact]
    public void AddingAHeaderPreservesFlagsAndInputBytes()
    {
        var entry = new RpfResourceFileEntry { SystemFlags = 0xa1234567, GraphicsFlags = 0xb7654321 };
        byte[] payload = [1, 2, 3, 255];
        byte[] result = Assert.IsType<byte[]>(ResourceBuilder.AddResourceHeader(entry, payload));
        Assert.Equal(0x37435352u, BinaryPrimitives.ReadUInt32LittleEndian(result));
        Assert.Equal(entry.Version, BinaryPrimitives.ReadInt32LittleEndian(result.AsSpan(4)));
        Assert.Equal(0xa1234567u, BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(8)));
        Assert.Equal(0xb7654321u, BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(12)));
        Assert.Equal(new byte[] { 1, 2, 3, 255 }, payload);
        Assert.Equal(payload, result[16..]);
        Assert.Null(ResourceBuilder.AddResourceHeader(entry, null));
        Assert.Equal(16, Assert.IsType<byte[]>(ResourceBuilder.AddResourceHeader(entry, [])).Length);
    }

    private sealed class TestRoot(int size) : ResourceFileBase
    {
        public TestGraphics? Graphics { get; } = size == 0 ? null : new TestGraphics(size);
        public override IResourceBlock[] GetReferences() =>
            Graphics is { } graphics ? [.. base.GetReferences(), graphics] : base.GetReferences();
    }

    private sealed class TestGraphics : ResourceGraphicsBlock
    {
        public byte[] Bytes { get; }
        public TestGraphics(int size)
        {
            Bytes = new byte[size];
            new Random(42).NextBytes(Bytes);
        }
        public override long BlockLength => Bytes.Length;
        public override void Read(ResourceDataReader reader, params object[] parameters) => throw new NotSupportedException();
        public override void Write(ResourceDataWriter writer, params object[] parameters) => writer.Write(Bytes);
    }
}
