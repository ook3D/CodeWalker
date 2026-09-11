using CodeWalker.GameFiles;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class WatermapFormatTests
{
    [Theory]
    [InlineData(Endianess.BigEndian)]
    [InlineData(Endianess.LittleEndian)]
    public void HeaderLayoutAndDebugColoursMatchSource(Endianess endianess)
    {
        using var stream = new MemoryStream();
        var writer = new DataWriter(stream, endianess);
        writer.Write(0x574D4150u);
        writer.Write(100u);
        writer.Write(32u);
        for (int i = 0; i < 4; i++) writer.Write(0.0f);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write(new byte[] { 52, 4, 2, 2, 16, 48, 16, 48, 32, 0 });
        writer.Write(new Vector3(1, 2, 3));
        writer.Write(0u);
        writer.Write(new Vector3(4, 5, 6));
        writer.Write(0u);
        writer.Write(0xFF112233u);

        var map = new WatermapFile();
        map.Load(stream.ToArray(), null);

        Assert.Equal(endianess, map.Endianess);
        Assert.Equal((byte)52, map.HeaderSize);
        Assert.Equal((byte)4, map.TileRowSize);
        Assert.Equal((byte)2, map.TileSize);
        Assert.Equal((byte)2, map.RefSize);
        Assert.Equal((byte)16, map.RiverPointSize);
        Assert.Equal((byte)48, map.RiverSize);
        Assert.Equal((byte)16, map.LakeBoxSize);
        Assert.Equal((byte)48, map.LakeSize);
        Assert.Equal((byte)32, map.PoolSize);
        Assert.Single(map.Pools);
        Assert.Single(map.Colours);
        Assert.Equal(new Vector3(1, 2, 3), map.Pools[0].Position);
        Assert.Equal(new Vector3(4, 5, 6), map.Pools[0].Size);
    }
}
