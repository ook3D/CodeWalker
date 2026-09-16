using System.Text;
using System.Xml;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class AwcEncryptionFlagTests
{
    [Theory]
    [InlineData(0xFF01)]
    [InlineData(0xFF03)]
    public void PackingDoesNotDecryptOrEncryptUnencryptedAudio(ushort flags)
    {
        // A partial ADPCM block, deliberately not a multiple of four bytes.
        byte[] audio = [0, 0, 0, 0, 0x11, 0x22, 0x33, 0x44, 0x55];
        var bank = Load(CreateBank(flags, audio));
        var stream = Assert.Single(bank.Streams);
        Assert.Equal(audio, stream.GetRawData());
        Assert.Equal(ADPCMCodec.DecodeADPCM(audio, 10), stream.GetWavFile()[44..]);
        Assert.Equal(audio, Assert.Single(Load(bank.Save()).Streams).GetRawData());
    }

    [Theory]
    [InlineData(0xFF09)]
    [InlineData(0xFF0B)]
    public void EncryptionBitControlsSingleChannelLoadAndSave(ushort flags)
    {
        var previousKey = GTA5Keys.PC_AWC_KEY;
        try
        {
            GTA5Keys.PC_AWC_KEY = [1, 2, 3, 4];
            byte[] audio = [0, 0, 0, 0, 0x11, 0x22, 0x33, 0x44];
            var encrypted = (byte[])audio.Clone();
            AwcFile.Encrypt_RSXXTEA(encrypted);
            Assert.False(audio.SequenceEqual(encrypted));
            var bank = Load(CreateBank(flags, encrypted));
            Assert.Equal(audio, Assert.Single(bank.Streams).GetRawData());
            var saved = bank.Save();
            var chunk = bank.Streams[0].DataChunk!.ChunkInfo;
            Assert.Equal(encrypted, saved[chunk.Offset..(chunk.Offset + chunk.Size)]);
            Assert.Equal(audio, Assert.Single(Load(saved).Streams).GetRawData());
        }
        finally
        {
            GTA5Keys.PC_AWC_KEY = previousKey;
        }
    }

    [Theory]
    [InlineData(0xFF01)]
    [InlineData(0xFF03)]
    [InlineData(0xFF09)]
    [InlineData(0xFF0B)]
    [InlineData(0xFF05)]
    [InlineData(0xFF0D)]
    public void XmlPreservesIndependentPackingAndEncryptionBits(ushort flags)
    {
        var bank = new AwcFile { Flags = flags };
        var xml = new StringBuilder();
        AwcFile.WriteXmlNode(bank, xml, 0, "");
        Assert.DoesNotContain("SingleChannelEncrypt", xml.ToString());
        Assert.DoesNotContain("MultiChannelEncrypt", xml.ToString());
        var doc = new XmlDocument();
        doc.LoadXml(xml.ToString());
        Assert.Equal(flags, AwcFile.ReadXmlNode(doc.DocumentElement, "")!.Flags);
    }

    [Theory]
    [InlineData("<SingleChannelEncrypt value=\"true\"/>", 0xFF02)]
    [InlineData("<MultiChannelEncrypt value=\"true\"/>", 0xFF08)]
    [InlineData("<SingleChannelEncrypt value=\"true\"/><ContiguousPacking value=\"false\"/>", 0xFF00)]
    [InlineData("<MultiChannelEncrypt value=\"true\"/><DataEncrypted value=\"false\"/>", 0xFF00)]
    public void LegacyXmlNamesPreserveTheirOriginalHeaderBits(string elements, ushort flags)
    {
        var doc = new XmlDocument();
        doc.LoadXml("<AudioWaveContainer><Version value=\"1\"/>" + elements + "</AudioWaveContainer>");
        Assert.Equal(flags, AwcFile.ReadXmlNode(doc.DocumentElement, "")!.Flags);
    }

    private static AwcFile Load(byte[] data)
    {
        var bank = new AwcFile();
        bank.Load(data, new RpfBinaryFileEntry { Name = "test.awc" });
        Assert.Null(bank.ErrorMessage);
        return bank;
    }

    private static byte[] CreateBank(ushort flags, byte[] audio)
    {
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer);
        writer.Write(0x54414441u);
        writer.Write((ushort)1);
        writer.Write(flags);
        writer.Write(1); // stream count
        writer.Write(38); // end of chunk table
        writer.Write((ushort)0); // first chunk index
        writer.Write(0x40000001u); // two chunks, stream ID 1
        writer.Write(((ulong)AwcChunkType.format << 56) | (20ul << 28) | 38ul);
        writer.Write(((ulong)AwcChunkType.data << 56) | ((ulong)audio.Length << 28) | 64ul);
        writer.Write((uint)((audio.Length - 4) * 2));
        writer.Write(-1); // loop point
        writer.Write((ushort)24000);
        writer.Write(new byte[9]);
        writer.Write((byte)AwcCodecType.ADPCM);
        writer.Write(new byte[6]); // align the data chunk to 16 bytes
        writer.Write(audio);
        return buffer.ToArray();
    }
}
