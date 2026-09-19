using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class RelAmbientZoneTests
{
    [Theory]
    [InlineData(150u, 5750395u, 0, 0)]
    [InlineData(150u, 5750395u, 0, 1)]
    [InlineData(150u, 5750395u, 2, 2)]
    [InlineData(151u, 7126027u, 0, 0)]
    [InlineData(151u, 7126027u, 0, 1)]
    [InlineData(151u, 7126027u, 2, 2)]
    [InlineData(151u, 50141324u, 0, 0)]
    [InlineData(151u, 50141324u, 0, 1)]
    [InlineData(151u, 50141324u, 2, 2)]
    public void AmbientZoneCountsSurviveBinaryAndXmlRoundTrips(uint type, uint version, byte rules, byte ambiences)
    {
        // Build the on-disk layout independently of the REL writer.
        bool packedCount = version is 5750395 or 7126027;
        using var record = new MemoryStream();
        using var writer = new BinaryWriter(record);
        writer.Write((uint)Dat151RelType.AmbientZone);
        writer.Write(new byte[224]);
        writer.Write((byte)10);
        writer.Write((byte)1);
        writer.Write(rules);
        writer.Write(packedCount ? ambiences : (byte)0);
        for (int i = 0; i < rules; i++) writer.Write(0x12345678u + (uint)i);
        if (!packedCount)
        {
            writer.Write(ambiences);
            writer.Write(new byte[3]);
        }
        for (int i = 0; i < ambiences; i++)
        {
            writer.Write(0x9679A34Eu + (uint)i);
            writer.Write(0.8f);
        }
        while (record.Length % 16 != 0) writer.Write((byte)0);
        var recordBytes = record.ToArray();

        using var file = new MemoryStream();
        using var fileWriter = new BinaryWriter(file);
        fileWriter.Write(type);
        fileWriter.Write((uint)(16 + recordBytes.Length));
        fileWriter.Write(version);
        fileWriter.Write(new byte[12]);
        fileWriter.Write(recordBytes);
        fileWriter.Write(4u); // Empty name table.
        fileWriter.Write(0u);
        fileWriter.Write(1u); // One indexed record.
        fileWriter.Write(0x64160F3Du);
        fileWriter.Write(16u);
        fileWriter.Write((uint)recordBytes.Length);
        fileWriter.Write(0u); // Empty hash and pack tables.
        fileWriter.Write(0u);

        var rel = new RelFile();
        rel.Load(file.ToArray(), null);
        AssertZone(rel);
        var saved = new RelFile();
        saved.Load(rel.Save(), null);
        AssertZone(saved);
        Assert.Equal(recordBytes, Assert.Single(saved.RelDatas).Data);
        var fromXml = XmlRel.GetRel(RelXml.GetXml(rel));
        var reloadedXml = new RelFile();
        reloadedXml.Load(fromXml.Save(), null);
        AssertZone(reloadedXml);
        Assert.Equal(recordBytes, Assert.Single(reloadedXml.RelDatas).Data);

        void AssertZone(RelFile parsed)
        {
            Assert.Equal((RelDatFileType)type, parsed.RelType);
            Assert.Equal(version, parsed.DataUnkVal);
            var zone = Assert.IsType<Dat151AmbientZone>(Assert.Single(parsed.RelDatas));
            Assert.Equal(rules, zone.NumRules);
            Assert.Equal(ambiences, zone.NumDirAmbiences);
            Assert.Equal((int)rules, zone.Rules.Length);
            Assert.Equal((int)ambiences, zone.DirAmbiences.Length);
            for (int i = 0; i < rules; i++) Assert.Equal(0x12345678u + (uint)i, (uint)zone.Rules[i]);
            for (int i = 0; i < ambiences; i++)
            {
                Assert.Equal(0x9679A34Eu + (uint)i, (uint)zone.DirAmbiences[i].Name);
                Assert.Equal(0.8f, zone.DirAmbiences[i].Volume);
            }
        }
    }
}
