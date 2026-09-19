using System.Text;
using System.Xml;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class AwcEncryptionFlagTests
{
    [Theory]
    [InlineData("PCM", 1)]
    [InlineData("PCM", 3)]
    [InlineData("PCM", 16385)]
    [InlineData("ADPCM", 16385)]
    [InlineData("ADPCM", 1)]
    [InlineData("ADPCM", 4088)]
    [InlineData("ADPCM", 4089)]
    public void XmlImportAlignsEncryptedChunksAndPreservesFollowingStreams(string codec, int samples)
    {
        var previousKey = GTA5Keys.PC_AWC_KEY;
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(folder);
        try
        {
            GTA5Keys.PC_AWC_KEY = [1, 2, 3, 4];
            var pcm = new byte[samples * 2];
            new Random(123).NextBytes(pcm);
            var wave = new AwcStream(new AwcFile())
            {
                FormatChunk = new AwcFormatChunk(new AwcChunkInfo { Type = AwcChunkType.format })
                { Codec = AwcCodecType.PCM, SamplesPerSecond = 24000, Samples = (uint)samples },
                DataChunk = new AwcDataChunk(new AwcChunkInfo { Type = AwcChunkType.data }) { Data = pcm }
            };
            File.WriteAllBytes(Path.Combine(folder, "test.wav"), wave.GetWavFile());
            var streams = string.Concat(new[] { "first", "second" }.Select(name =>
                $"<Item><Name>{name}</Name><FileName>test.wav</FileName><Chunks><Item><Type>format</Type><Codec>{codec}</Codec><Samples value=\"{samples}\"/><SampleRate value=\"24000\"/></Item><Item><Type>data</Type></Item></Chunks></Item>"));
            var doc = new XmlDocument();
            doc.LoadXml("<AudioWaveContainer><Version value=\"1\"/><DataEncrypted value=\"true\"/><ContiguousPacking value=\"true\"/><Streams>"
                + streams + "</Streams></AudioWaveContainer>");
            var bank = XmlAwc.GetAwc(doc, folder);
            var expectedAudio = bank.Streams.Select(s => s.GetPcmData()).ToArray();
            var saved = bank.Save();
            Assert.Equal(saved, bank.Save());
            var loaded = Load(saved);
            Assert.Equal(2, loaded.Streams.Length);
            for (int i = 0; i < loaded.Streams.Length; i++)
            {
                var stream = loaded.Streams[i];
                Assert.Equal(samples, stream.SampleCount);
                Assert.Equal(0, stream.DataChunk!.ChunkInfo.Size % 4);
                Assert.Equal(expectedAudio[i], stream.GetPcmData());
                Assert.Equal(samples * 2, stream.GetWavFile().Length - 44);
                if (codec == "PCM") Assert.Equal(pcm, stream.GetPcmData());
            }
        }
        finally
        {
            GTA5Keys.PC_AWC_KEY = previousKey;
            Directory.Delete(folder, true);
        }
    }

    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(415659)]
    public void EncryptingPartialAdpcmChunksPadsStorageWithoutChangingAudio(int length)
    {
        var previousKey = GTA5Keys.PC_AWC_KEY;
        try
        {
            GTA5Keys.PC_AWC_KEY = [1, 2, 3, 4];
            var audio = new byte[length];
            var bank = Load(CreateBank(0xFF03, audio));
            var stream = Assert.Single(bank.Streams);
            var expectedPcm = stream.GetPcmData();
            int samples = stream.SampleCount;
            bank.DataEncryptedFlag = true;
            var saved = bank.Save();
            Assert.Equal((length + 3) & ~3, stream.DataChunk!.ChunkInfo.Size);
            Assert.Equal(audio, stream.GetRawData());
            Assert.Equal(saved, bank.Save());
            var reloaded = Assert.Single(Load(saved).Streams);
            Assert.Equal(samples, reloaded.SampleCount);
            Assert.Equal(expectedPcm, reloaded.GetPcmData());
            Assert.Equal(audio, reloaded.GetRawData()[..length]);
            Assert.All(reloaded.GetRawData()[length..], b => Assert.Equal((byte)0, b));
        }
        finally { GTA5Keys.PC_AWC_KEY = previousKey; }
    }

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
        Assert.Equal(ADPCMCodec.DecodeADPCM(audio, 10)[..20], stream.GetWavFile()[44..]);
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

    [Theory]
    [InlineData(false, -1)]
    [InlineData(true, -1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public void XmlImportEncryptsAudioAndRepeatedSavesPreservePlaintext(bool multichannel, int shorterChannel)
    {
        var previousKey = GTA5Keys.PC_AWC_KEY;
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(folder);
        try
        {
            GTA5Keys.PC_AWC_KEY = [1, 2, 3, 4];
            var pcm = new byte[32768];
            new Random(123).NextBytes(pcm);
            var wave = new AwcStream(new AwcFile())
            {
                FormatChunk = new AwcFormatChunk(new AwcChunkInfo { Type = AwcChunkType.format })
                { Codec = AwcCodecType.PCM, SamplesPerSecond = 24000, Samples = 16384 },
                DataChunk = new AwcDataChunk(new AwcChunkInfo { Type = AwcChunkType.data }) { Data = pcm }
            };
            File.WriteAllBytes(Path.Combine(folder, "test.wav"), wave.GetWavFile());
            var shortPcm = pcm[..8176]; // One ADPCM block versus five in the long channel.
            wave.FormatChunk.Samples = 4088;
            wave.DataChunk.Data = shortPcm;
            File.WriteAllBytes(Path.Combine(folder, "short.wav"), wave.GetWavFile());
            const string format = "<Codec>ADPCM</Codec><Samples value=\"16384\"/><SampleRate value=\"24000\"/>";
            var streams = multichannel
                ? "<Item><Name/><Chunks><Item><Type>streamformat</Type><BlockSize value=\"8192\"/></Item><Item><Type>data</Type></Item><Item><Type>seektable</Type></Item></Chunks></Item>"
                    + "<Item><Name>left</Name><FileName>" + (shorterChannel == 0 ? "short.wav" : "test.wav") + "</FileName><StreamFormat>" + (shorterChannel == 0 ? format.Replace("16384", "4088") : format) + "</StreamFormat></Item>"
                    + "<Item><Name>right</Name><FileName>" + (shorterChannel == 1 ? "short.wav" : "test.wav") + "</FileName><StreamFormat>" + (shorterChannel == 1 ? format.Replace("16384", "4088") : format) + "</StreamFormat></Item>"
                : "<Item><Name>test</Name><FileName>test.wav</FileName><Chunks><Item><Type>format</Type>" + format + "</Item><Item><Type>data</Type></Item></Chunks></Item>";
            var doc = new XmlDocument();
            doc.LoadXml("<AudioWaveContainer><Version value=\"1\"/><DataEncrypted value=\"true\"/>"
                + (multichannel ? "<MultiChannel value=\"true\"/>" : "<ContiguousPacking value=\"true\"/>")
                + "<Streams>" + streams + "</Streams></AudioWaveContainer>");
            var bank = XmlAwc.GetAwc(doc, folder);
            var source = multichannel ? bank.MultiChannelSource! : Assert.Single(bank.Streams);
            var plaintext = (byte[])source.DataChunk!.Data.Clone();
            var chunk = source.DataChunk.ChunkInfo;
            var saved = XmlMeta.GetAwcData(doc, folder)!;
            Assert.Equal(8, BitConverter.ToUInt16(saved, 6) & 8);
            var encrypted = saved[chunk.Offset..(chunk.Offset + chunk.Size)];
            int blockSize = multichannel ? 8192 : plaintext.Length;
            if (multichannel) Assert.True(plaintext.Length > blockSize);
            for (int offset = 0; offset < plaintext.Length; offset += blockSize)
            {
                int count = Math.Min(blockSize, plaintext.Length - offset);
                var block = encrypted[offset..(offset + count)];
                Assert.False(block.SequenceEqual(plaintext[offset..(offset + count)]));
                AwcFile.Decrypt_RSXXTEA(block);
                Assert.Equal(plaintext[offset..(offset + count)], block);
            }
            var loaded = Load(saved);
            if (multichannel)
            {
                var channels = loaded.Streams.Where(s => s.StreamFormat != null).ToArray();
                for (int i = 0; i < channels.Length; i++)
                {
                    var stream = channels[i];
                    var channelPcm = i == shorterChannel ? shortPcm : pcm;
                    var encoded = ADPCMCodec.EncodeADPCM(channelPcm, channelPcm.Length / 2);
                    Assert.Equal(encoded, stream.GetRawData()[..encoded.Length]);
                    Assert.All(stream.GetRawData()[encoded.Length..], b => Assert.Equal((byte)0, b));
                    Assert.Equal(channelPcm.Length / 2, stream.SampleCount);
                    Assert.Equal(ADPCMCodec.DecodeADPCM(encoded, channelPcm.Length / 2)[..channelPcm.Length], stream.GetPcmData());
                    Assert.Equal(stream.SampleCount, loaded.MultiChannelSource!.StreamBlocks.Sum(b => b.Channels[i].SampleCount));
                }
                foreach (var block in loaded.MultiChannelSource!.StreamBlocks)
                {
                    int startBlock = 0;
                    foreach (var channel in block.Channels)
                    {
                        Assert.Equal(startBlock, channel.StartBlock);
                        startBlock += channel.BlockCount;
                    }
                }
            }
            else Assert.Equal(plaintext, Assert.Single(loaded.Streams).GetRawData());
            Assert.Equal(saved, bank.Save());
            Assert.Equal(plaintext, source.DataChunk.Data);
            Assert.Equal(saved, bank.Save());
        }
        finally
        {
            GTA5Keys.PC_AWC_KEY = previousKey;
            Directory.Delete(folder, true);
        }
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
        writer.Write((uint)((audio.Length - 4 * ((audio.Length + 2047) / 2048)) * 2));
        writer.Write(-1); // loop point
        writer.Write((ushort)24000);
        writer.Write(new byte[9]);
        writer.Write((byte)AwcCodecType.ADPCM);
        writer.Write(new byte[6]); // align the data chunk to 16 bytes
        writer.Write(audio);
        return buffer.ToArray();
    }
}
