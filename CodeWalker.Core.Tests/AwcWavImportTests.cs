using System.Text;
using System.Xml;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class AwcWavImportTests
{
    [Theory]
    [InlineData(16, false)]
    [InlineData(18, false)]
    [InlineData(20, false)]
    [InlineData(18, true)]
    public void ReadsPcmWithFormatExtensionsAndMetadata(int formatSize, bool dataFirst)
    {
        byte[] pcm = [0, 0, 1, 0, 255, 255, 123, 0];
        var stream = CreateStream();
        stream.ParseWavFile(CreateWav(formatSize, pcm, dataFirst));
        Assert.Equal(pcm, stream.GetRawData());
        Assert.Equal(48000, stream.SamplesPerSecond);
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("chunk_length")]
    [InlineData("missing_data")]
    [InlineData("missing_fmt")]
    [InlineData("short_fmt")]
    [InlineData("float")]
    [InlineData("stereo")]
    [InlineData("8bit")]
    [InlineData("odd_data")]
    public void RejectsMalformedOrUnsupportedWav(string kind)
    {
        var wav = CreateWav(18, kind == "odd_data" ? [1, 2, 3] : [1, 2, 3, 4], false);
        // fmt begins at 12, followed by an odd-sized JUNK chunk, then data at 50.
        switch (kind)
        {
            case "truncated": wav = wav[..^1]; break;
            case "chunk_length": BitConverter.GetBytes(uint.MaxValue).CopyTo(wav, 54); break;
            case "missing_data": Encoding.ASCII.GetBytes("JUNK").CopyTo(wav, 50); break;
            case "missing_fmt": Encoding.ASCII.GetBytes("JUNK").CopyTo(wav, 12); break;
            case "short_fmt": BitConverter.GetBytes(14).CopyTo(wav, 16); break;
            case "float": wav[20] = 3; break;
            case "stereo": wav[22] = 2; break;
            case "8bit": wav[34] = 8; break;
        }
        Assert.Throws<InvalidDataException>(() => CreateStream().ParseWavFile(wav));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void XmlImportReportsMissingOrInvalidAudio(bool createInvalidFile)
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(folder);
        try
        {
            if (createInvalidFile) File.WriteAllBytes(Path.Combine(folder, "test.wav"), [1, 2, 3]);
            var doc = new XmlDocument();
            doc.LoadXml("<Item><Name>test</Name><FileName>test.wav</FileName></Item>");
            var error = Assert.Throws<InvalidDataException>(() => CreateStream().ReadXml(doc.DocumentElement!, folder));
            Assert.Contains("test.wav", error.Message);
            Assert.NotNull(error.InnerException);
        }
        finally { Directory.Delete(folder, true); }
    }

    private static AwcStream CreateStream() => new(new AwcFile())
    {
        FormatChunk = new AwcFormatChunk(new AwcChunkInfo { Type = AwcChunkType.format }) { Codec = AwcCodecType.PCM }
    };

    private static byte[] CreateWav(int formatSize, byte[] pcm, bool dataFirst)
    {
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(0);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        void WriteChunk(string tag, byte[] data)
        {
            writer.Write(Encoding.ASCII.GetBytes(tag));
            writer.Write(data.Length);
            writer.Write(data);
            if ((data.Length & 1) != 0) writer.Write((byte)0);
        }
        if (dataFirst) WriteChunk("data", pcm);
        var format = new byte[formatSize];
        BitConverter.GetBytes((ushort)1).CopyTo(format, 0);
        BitConverter.GetBytes((ushort)1).CopyTo(format, 2);
        BitConverter.GetBytes(48000).CopyTo(format, 4);
        BitConverter.GetBytes(96000).CopyTo(format, 8);
        BitConverter.GetBytes((ushort)2).CopyTo(format, 12);
        BitConverter.GetBytes((ushort)16).CopyTo(format, 14);
        WriteChunk("fmt ", format);
        WriteChunk("JUNK", [1, 2, 3]);
        if (!dataFirst) WriteChunk("data", pcm);
        writer.Seek(4, SeekOrigin.Begin);
        writer.Write((int)buffer.Length - 8);
        return buffer.ToArray();
    }
}
