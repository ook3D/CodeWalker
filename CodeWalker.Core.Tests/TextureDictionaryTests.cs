using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class TextureDictionaryTests
{
    [Fact]
    public void TextureBaseUsesNativeGrcTextureLayout()
    {
        var texture = new TextureBase
        {
            VFT = 0x11223344,
            FirstNodePointer = 0x0102030405060708,
            GcmFormat = 0x10,
            GcmMipMapCount = 0x11,
            GcmDimension = 0x12,
            GcmImageType = 0x13,
            GcmRemap = 0x14151617,
            GcmWidth = 0x1819,
            GcmHeight = 0x1A1B,
            GcmDepth = 0x1C1D,
            GcmTileMode = 0x1E,
            GcmBindFlags = 0x1F,
            GcmPitch = 0x20212223,
            GcmOffset = 0x24252627,
            ReferenceCount = 0x3031,
            ResourceTypeAndConversionFlags = 0x32,
            LayerCount = 0x33,
            CachedTexturePointer = 0x38393A3B3C3D3E3F,
            PhysicalSizeAndTemplateType = 0x40414243,
            HandleIndex = 0x44454647,
            ExtraFlags = 0x48494A4B,
        };
        using var system = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(system, graphics) { Position = 0x50000000 };

        texture.Write(writer);

        var bytes = system.ToArray();
        Assert.Equal(0x50, bytes.Length);
        Assert.Equal(texture.VFT, BitConverter.ToUInt32(bytes, 0x00));
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, 0x04));
        Assert.Equal(texture.FirstNodePointer, BitConverter.ToUInt64(bytes, 0x08));
        Assert.Equal(texture.GcmFormat, bytes[0x10]);
        Assert.Equal(texture.GcmRemap, BitConverter.ToUInt32(bytes, 0x14));
        Assert.Equal(texture.GcmWidth, BitConverter.ToUInt16(bytes, 0x18));
        Assert.Equal(texture.GcmPitch, BitConverter.ToUInt32(bytes, 0x20));
        Assert.Equal(texture.GcmOffset, BitConverter.ToUInt32(bytes, 0x24));
        Assert.Equal(texture.ReferenceCount, BitConverter.ToUInt16(bytes, 0x30));
        Assert.Equal(texture.ResourceTypeAndConversionFlags, bytes[0x32]);
        Assert.Equal(texture.LayerCount, bytes[0x33]);
        Assert.All(bytes[0x34..0x38], value => Assert.Equal(0, value));
        Assert.Equal(texture.CachedTexturePointer, BitConverter.ToUInt64(bytes, 0x38));
        Assert.Equal(texture.PhysicalSizeAndTemplateType, BitConverter.ToUInt32(bytes, 0x40));
        Assert.Equal(texture.HandleIndex, BitConverter.ToUInt32(bytes, 0x44));
        Assert.Equal(texture.ExtraFlags, BitConverter.ToUInt32(bytes, 0x48));
        Assert.All(bytes[0x4C..0x50], value => Assert.Equal(0, value));

        using var input = new MemoryStream(bytes);
        using var unusedGraphics = new MemoryStream();
        var reader = new ResourceDataReader(input, unusedGraphics) { Position = 0x50000000 };
        var loaded = new TextureBase();
        loaded.Read(reader);

        Assert.Equal(texture.FirstNodePointer, loaded.FirstNodePointer);
        Assert.Equal(texture.GcmBindFlags, loaded.GcmBindFlags);
        Assert.Equal(texture.ResourceTypeAndConversionFlags, loaded.ResourceTypeAndConversionFlags);
        Assert.Equal(texture.LayerCount, loaded.LayerCount);
        Assert.Equal(texture.CachedTexturePointer, loaded.CachedTexturePointer);
        Assert.Equal(texture.PhysicalSizeAndTemplateType, loaded.PhysicalSizeAndTemplateType);
        Assert.Equal(texture.HandleIndex, loaded.HandleIndex);
    }

    [Fact]
    public void DictionaryUsesNativePgDictionaryLayout()
    {
        var dictionary = new TextureDictionary { ReferenceCount = 7 };
        using var system = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(system, graphics) { Position = 0x50000000 };

        dictionary.Write(writer);

        var bytes = system.ToArray();
        Assert.Equal(0x40, bytes.Length);
        Assert.All(bytes[0x10..0x18], value => Assert.Equal(0, value));
        Assert.Equal(7u, BitConverter.ToUInt32(bytes, 0x18));
        Assert.All(bytes[0x1C..0x20], value => Assert.Equal(0, value));
    }

    [Fact]
    public void TextureEntriesAreSortedAndRoundTripWithTheirCodes()
    {
        var first = new Texture { Name = "z_texture", NameHash = JenkHash.GenHash("z_texture") };
        var second = new Texture { Name = "a_texture", NameHash = JenkHash.GenHash("a_texture") };
        var dictionary = new TextureDictionary();
        dictionary.BuildFromTextureList([first, second]);

        Assert.True(dictionary.Codes.data_items[0] < dictionary.Codes.data_items[1]);
        Assert.Same(dictionary.Entries.data_items[0], dictionary.Lookup(dictionary.Codes.data_items[0]));

        var data = ResourceBuilder.Build(dictionary, 13);
        var file = new YtdFile();
        RpfFile.LoadResourceFile(file, data, 13);

        var loaded = file.TextureDict!;
        Assert.Equal(dictionary.Codes.data_items, loaded.Codes.data_items);
        Assert.Equal(dictionary.ReferenceCount, loaded.ReferenceCount);
        Assert.Equal(2, loaded.Entries.data_items.Length);
        Assert.All(loaded.Codes.data_items, hash => Assert.NotNull(loaded.Lookup(hash)));
    }

    [Fact]
    public void MismatchedCodesAndEntriesAreRejected()
    {
        var dictionary = new TextureDictionary
        {
            Codes = new ResourceSimpleList64_uint { data_items = [1] },
            Entries = new ResourcePointerList64<Texture>(),
        };

        using var system = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(system, graphics) { Position = 0x50000000 };
        Assert.Throws<InvalidDataException>(() => dictionary.Write(writer));
    }
}
