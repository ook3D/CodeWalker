using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class ResourceTypeTests
{
    [Fact]
    public void AnimBoneTagsMatchNativeParserEnum()
    {
        Assert.Equal(131, Enum.GetValues<eAnimBoneTag>().Length);
        Assert.Equal(-1, (int)eAnimBoneTag.BONETAG_INVALID);
        Assert.Equal(0, (int)eAnimBoneTag.BONETAG_ROOT);
        Assert.Equal(64729, (int)eAnimBoneTag.BONETAG_L_CLAVICLE);
        Assert.Equal(56194, (int)eAnimBoneTag.BONETAG_FIRSTPERSONCAM);
        Assert.Equal(4126, (int)eAnimBoneTag.BONETAG_CH_R_HAND);
    }

    [Fact]
    public void AtStringMatchesNativeFieldLayout()
    {
        var original = (atString)"types";
        IResourceBlock dataBlock = Assert.Single(original.GetReferences());
        dataBlock.FilePosition = 0x50000010;

        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };
        original.Write(writer);
        dataBlock.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(22, bytes.Length);
        Assert.Equal(0x50000010ul, BitConverter.ToUInt64(bytes, 0));
        Assert.Equal(5, BitConverter.ToUInt16(bytes, 8));
        Assert.Equal(6, BitConverter.ToUInt16(bytes, 10));
        Assert.All(bytes[12..16], value => Assert.Equal(0, value));

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = new atString();
        loaded.Read(reader);
        Assert.Equal("types", loaded.Value);
        Assert.Equal(5, loaded.Length);
        Assert.Equal(6, loaded.Allocated);
    }

    [Fact]
    public void AtArrayUsesNativeHeaderAndBackingAllocation()
    {
        var original = new atArray<VehicleRecordEntry>
        {
            Items = [new VehicleRecordEntry { Time = 123 }]
        };
        IResourceBlock dataBlock = Assert.Single(original.GetReferences());
        dataBlock.FilePosition = 0x50000010;

        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };
        original.Write(writer);
        dataBlock.Write(writer);

        byte[] bytes = data.ToArray();
        Assert.Equal(48, bytes.Length);
        Assert.Equal(0x50000010ul, BitConverter.ToUInt64(bytes, 0));
        Assert.Equal(1, BitConverter.ToUInt16(bytes, 8));
        Assert.Equal(1, BitConverter.ToUInt16(bytes, 10));
        Assert.All(bytes[12..16], value => Assert.Equal(0, value));

        var reader = new ResourceDataReader(new MemoryStream(bytes), graphics) { Position = 0x50000000 };
        var loaded = new atArray<VehicleRecordEntry>();
        loaded.Read(reader);
        Assert.Single(loaded.Items);
        Assert.Equal(123u, loaded.Items[0].Time);
    }
}
