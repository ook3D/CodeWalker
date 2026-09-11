using System.Xml;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class FilterFormatTests
{
    [Fact]
    public void MultiWeightDictionaryRoundTripsAsAResource()
    {
        var filter = new FrameFilterMultiWeight
        {
            NameHash = (MetaHash)JenkHash.GenHash("upper_body"),
            Entries = new ResourceSimpleList64_s<FrameFilterMultiWeight.TrackIdIndex>
            {
                data_items = [new() { Track = 1, BoneId = 0x1234, WeightIndex = 0 }],
            },
            Weights = new ResourceSimpleList64_float { data_items = [0.75f] },
        };
        var dictionary = new FrameFilterDictionary();
        dictionary.BuildFromFilterList([filter]);

        var data = ResourceBuilder.Build(dictionary, 4);
        var loaded = new YfdFile();
        RpfFile.LoadResourceFile(loaded, data, 4);

        var loadedFilter = Assert.IsType<FrameFilterMultiWeight>(Assert.Single(loaded.FrameFilterDictionary!.Filters.data_items));
        Assert.Equal(filter.NameHash, loadedFilter.NameHash);
        Assert.Equal(0.75f, Assert.Single(loadedFilter.Weights.data_items));
        Assert.Equal(data, loaded.Save());
    }

    [Fact]
    public void BuiltInFiltersMatchNativeLayoutsAndDispatchByType()
    {
        FrameFilterBase[] filters =
        [
            new FrameFilterBone(),
            new FrameFilterBoneBasic(),
            new FrameFilterBoneMultiWeight(),
            new FrameFilterMultiWeight(),
            new FrameFilterTrackMultiWeight(),
            new FrameFilterMover(),
        ];
        long[] sizes = [0x20, 0x30, 0x48, 0x40, 0x38, 0x28];

        for (var i = 0; i < filters.Length; i++)
        {
            using var data = new MemoryStream();
            using var graphics = new MemoryStream();
            var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };
            filters[i].Write(writer);

            Assert.Equal(sizes[i], data.Length);
            var reader = new ResourceDataReader(new MemoryStream(data.ToArray()), graphics) { Position = 0x50000000 };
            Assert.Equal(filters[i].GetType(), new FrameFilterBase().GetType(reader).GetType());
        }
    }

    [Fact]
    public void MoverFilterMatchesNativeFieldLayoutAndSignature()
    {
        var filter = new FrameFilterMover
        {
            VFT = 0x0102030405060708,
            RefCount = 7,
            TranslationWeight = 0.25f,
            RotationWeight = 0.75f,
            MoverId = 0xABCD,
            NonMoverDofsAllowed = false,
        };
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        var writer = new ResourceDataWriter(data, graphics) { Position = 0x50000000 };

        filter.Write(writer);

        var bytes = data.ToArray();
        Assert.Equal(filter.VFT, BitConverter.ToUInt64(bytes, 0x00));
        Assert.Equal(filter.RefCount, BitConverter.ToUInt32(bytes, 0x08));
        Assert.Equal(filter.CalculateSignature(), BitConverter.ToUInt32(bytes, 0x0C));
        Assert.Equal((uint)FrameFilterType.Mover, BitConverter.ToUInt32(bytes, 0x10));
        Assert.All(bytes[0x14..0x18], value => Assert.Equal(0, value));
        Assert.Equal(filter.TranslationWeight, BitConverter.ToSingle(bytes, 0x18));
        Assert.Equal(filter.RotationWeight, BitConverter.ToSingle(bytes, 0x1C));
        Assert.Equal(filter.MoverId, BitConverter.ToUInt16(bytes, 0x20));
        Assert.Equal(0, bytes[0x22]);
        Assert.All(bytes[0x23..0x28], value => Assert.Equal(0, value));
    }

    [Fact]
    public void FilterEntryStructsKeepIgnoredPaddingOutOfTheApi()
    {
        var dof = new FrameFilterMultiWeight.TrackIdIndex { Track = 3, BoneId = 0x1234, WeightIndex = 5 };
        var dofBytes = MetaTypes.ConvertArrayToBytes([dof]);
        Assert.Equal(8, dofBytes.Length);
        Assert.Equal(0, dofBytes[0]);
        Assert.Equal(dof.Track, dofBytes[1]);
        Assert.Equal(dof.BoneId, BitConverter.ToUInt16(dofBytes, 2));
        Assert.Equal(dof.WeightIndex, BitConverter.ToInt32(dofBytes, 4));

        var track = new FrameFilterTrackMultiWeight.TrackIndex { Track = 9, WeightIndex = 2 };
        var trackBytes = MetaTypes.ConvertArrayToBytes([track]);
        Assert.All(trackBytes[0..3], value => Assert.Equal(0, value));
        Assert.Equal(track.Track, trackBytes[3]);
        Assert.Equal(track.WeightIndex, BitConverter.ToInt32(trackBytes, 4));
    }

    [Fact]
    public void FilterDictionaryReadsPolymorphicXmlAndSortsHashes()
    {
        var document = new XmlDocument();
        document.LoadXml("""
            <FrameFilterDictionary>
              <Item><Name>z_filter</Name><Type>Mover</Type><MoverId value="2" /></Item>
              <Item><Name>a_filter</Name><Type>Bone</Type><NonBoneDofsAllowed value="false" /></Item>
            </FrameFilterDictionary>
            """);
        var dictionary = new FrameFilterDictionary();

        dictionary.ReadXml(document.DocumentElement!);

        Assert.Contains(dictionary.Filters.data_items, filter => filter is FrameFilterBone);
        Assert.Contains(dictionary.Filters.data_items, filter => filter is FrameFilterMover);
        Assert.True(dictionary.FilterNameHashes.data_items[0].Hash < dictionary.FilterNameHashes.data_items[1].Hash);
    }
}
