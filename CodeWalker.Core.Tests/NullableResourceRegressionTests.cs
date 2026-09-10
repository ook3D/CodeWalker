using System.Text;
using System.Xml;
using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class NullableResourceRegressionTests
{
    [Fact]
    public void ResourcePointersPreserveMissingSlotsAndReaderPosition()
    {
        using var system = new MemoryStream("hello\0"u8.ToArray());
        using var graphics = new MemoryStream();
        var reader = new ResourceDataReader(system, graphics) { Position = 0x50000000 };

        Assert.Null(reader.ReadBlockAt<string_r>(0));
        var blocks = Assert.IsType<string_r[]>(reader.ReadBlocks<string_r>([0, 0x50000000, 0]));
        Assert.Equal(3, blocks.Length);
        Assert.Null(blocks[0]);
        Assert.Equal("hello", blocks[1].Value);
        Assert.Null(blocks[2]);
        Assert.Equal(0x50000000, reader.Position);
        Assert.Same(blocks[1], reader.ReadRequiredBlock<string_r>());
        Assert.Equal(0x50000006, reader.Position);
    }

    [Fact]
    public void RequiredParticleBlockRejectsUnsupportedType()
    {
        var bytes = new byte[12];
        BitConverter.GetBytes(uint.MaxValue).CopyTo(bytes, 8);
        using var system = new MemoryStream(bytes);
        using var graphics = new MemoryStream();
        var reader = new ResourceDataReader(system, graphics) { Position = 0x50000000 };
        Assert.Throws<InvalidDataException>(() => reader.ReadRequiredBlock<ParticleBehaviour>());
    }

    [Fact]
    public void MetadataStringLookupUsesBlockCountRatherThanByteLength()
    {
        var meta = new Meta { DataBlocks = new ResourceSimpleArray<MetaDataBlock>() };
        meta.DataBlocks.Add(new MetaDataBlock
        {
            StructureNameHash = (MetaName)MetaTypeName.STRING,
            Data = "abc\0"u8.ToArray(),
            DataLength = 4
        });
        Assert.Equal("abc", MetaTypes.GetString(meta, new CharPointer(1, 3)));
        Assert.Equal(string.Empty, MetaTypes.GetString(meta, new CharPointer(2, 3)));
        Assert.Equal(string.Empty, MetaTypes.GetString(meta, default));
    }

    [Fact]
    public void MovingAPathNodeWithoutAGridLeavesItsPositionUnchanged()
    {
        var node = new YndNode();
        node.SetPosition(new Vector3(1, 2, 3));
        node.SetYndNodePosition(new Space(), new Vector3(4, 5, 6), out var affected);
        Assert.Equal(new Vector3(1, 2, 3), node.Position);
        Assert.Empty(affected);
    }

    [Fact]
    public void ParticleXmlPreservesMissingNamePointers()
    {
        var document = new XmlDocument();
        document.LoadXml("<Item />");
        var rule = new ParticleEffectRule();
        rule.ReadXml(Assert.IsType<XmlElement>(document.DocumentElement));
        Assert.Null(rule.Name);
        rule.WriteXml(new StringBuilder(), 0);
    }

    [Fact]
    public void RemovingAnEmitterPreservesTheFixedCapacity()
    {
        var removed = new ParticleEventEmitter();
        var remaining = new ParticleEventEmitter { Index = 1 };
        var slots = new ParticleEventEmitter[32];
        slots[0] = removed;
        slots[1] = remaining;
        var effect = new ParticleEffectRule
        {
            EventEmitters = new ResourcePointerArray64<ParticleEventEmitter> { data_items = slots },
            EventEmittersCount = 2
        };
        YptEditUtil.RemoveEmitter(effect, removed);
        Assert.Equal(1, effect.EventEmittersCount);
        Assert.Equal(32, effect.EventEmitters.data_items.Length);
        Assert.Same(remaining, effect.EventEmitters.data_items[0]);
        Assert.Equal(0u, remaining.Index);
        Assert.All(effect.EventEmitters.data_items.Skip(1), item => Assert.Null(item));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FbxRoundTripPreservesAnEmptyChildList(bool binary)
    {
        var document = new FbxDocument();
        var header = new FbxNode { Name = "FBXHeaderExtension" };
        var timestamp = new FbxNode { Name = "CreationTimeStamp" };
        foreach (var (name, value) in new[]
        {
            ("Year", 2026), ("Month", 1), ("Day", 2), ("Hour", 3),
            ("Minute", 4), ("Second", 5), ("Millisecond", 6)
        })
            timestamp.Nodes.Add(new FbxNode { Name = name, Value = value });
        header.Nodes.Add(timestamp);
        document.Nodes.Add(header);
        var empty = new FbxNode { Name = "Objects" };
        empty.Nodes.Add(null);
        document.Nodes.Add(empty);

        using var stream = new MemoryStream();
        if (binary) new FbxBinaryWriter(stream).Write(document);
        else new FbxAsciiWriter(stream).Write(document);
        stream.Position = 0;
        var result = binary ? new FbxBinaryReader(stream).Read() : new FbxAsciiReader(stream).Read();
        var objects = Assert.IsType<FbxNode>(result["Objects"]);
        Assert.Null(Assert.Single(objects.Nodes));
        Assert.Null(objects.Value);
    }
}
