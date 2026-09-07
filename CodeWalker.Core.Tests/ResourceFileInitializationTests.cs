using System.Xml;
using CodeWalker.GameFiles;
using CodeWalker.World;
using Xunit;

namespace CodeWalker.Core.Tests;

public class ResourceFileInitializationTests
{
    [Fact]
    public void ParticleLookupsAreClearedWhenTheEffectsListIsRemoved()
    {
        var drawable = new DrawablePtfx();
        var effect = new ParticleEffectRule();
        var file = new YptFile
        {
            PtfxList = new ParticleEffectsList
            {
                DrawableDictionary = new DrawablePtfxDictionary
                {
                    Hashes = [123],
                    Drawables = new ResourcePointerArray64<DrawablePtfx> { data_items = [drawable] }
                },
                EffectRuleDictionary = new ParticleEffectRuleDictionary
                {
                    EffectRules = new ResourcePointerList64<ParticleEffectRule> { data_items = [effect] }
                }
            }
        };
        file.RebuildDicts();
        Assert.Same(drawable, file.DrawableDict[123]);
        Assert.Same(file, drawable.Owner);
        Assert.Same(effect, Assert.Single(file.AllEffects));
        Assert.Single(file.EffectDict);

        file.PtfxList = null;
        file.RebuildDicts();
        Assert.Empty(file.DrawableDict);
        Assert.Empty(file.EffectDict);
        Assert.Empty(file.AllEffects);
        Assert.Throws<InvalidOperationException>(() => file.Save());
    }

    [Fact]
    public void AudioSectorsRequireAnXmlRoot()
    {
        Assert.Throws<XmlException>(() => XmlAud.GetAudWorldSectors(new XmlDocument()));
    }

    [Fact]
    public void HeightmapRebuildClearsGeometryWhenFilesAreRemoved()
    {
        var maps = new Heightmaps();
        maps.HeightmapFiles.Add(new HeightmapFile
        {
            Width = 2, Height = 2,
            MaxHeights = [20, 20, 20, 20],
            MinHeights = [10, 10, 10, 10]
        });
        maps.BuildVertices();
        Assert.Equal(12, maps.GetTriangleVertices().Length);
        Assert.Equal(8, maps.GetNodePositions().Length);

        maps.HeightmapFiles.Clear();
        maps.BuildVertices();
        Assert.Empty(maps.GetTriangleVertices());
        Assert.Empty(maps.GetNodePositions());
        Assert.Empty(maps.GetPathVertices());
    }

    [Fact]
    public void EmptyWorldOverlaysProvideEmptyGeometry()
    {
        var water = new Watermaps();
        water.BuildVertices();
        Assert.Empty(water.GetTriangleVertices());
        Assert.Empty(water.GetNodePositions());
        Assert.Empty(water.GetPathVertices());

        var zones = new PopZones();
        zones.Groups.Add("test", new PopZone { NameLabel = "test", Boxes = [new PopZoneBox()] });
        zones.BuildVertices();
        Assert.Equal(6, zones.GetTriangleVertices().Length);
        zones.Groups.Clear();
        zones.BuildVertices();
        Assert.Empty(zones.GetTriangleVertices());
        Assert.Empty(zones.GetNodePositions());
        Assert.Empty(zones.GetPathVertices());
    }

    [Fact]
    public void CompressedHeightmapRoundTripsWithoutAnArchiveEntry()
    {
        var original = new HeightmapFile
        {
            Width = 3,
            Height = 2,
            MaxHeights = [0, 40, 60, 80, 100, 0],
            MinHeights = [0, 10, 20, 30, 40, 0]
        };
        var loaded = new HeightmapFile();
        loaded.Load(original.Save(), null);

        Assert.Equal(original.Width, loaded.Width);
        Assert.Equal(original.Height, loaded.Height);
        Assert.Equal(original.MaxHeights, loaded.MaxHeights);
        Assert.Equal(original.MinHeights, loaded.MinHeights);
        Assert.Null(loaded.RpfFileEntry);
    }

    [Fact]
    public void EmptyHeightmapCanBeSavedAndLoaded()
    {
        var loaded = new HeightmapFile();
        loaded.Load(new HeightmapFile().Save(), null);
        Assert.Empty(loaded.CompHeaders);
        Assert.Empty(loaded.MaxHeights);
        Assert.Empty(loaded.MinHeights);
    }

    [Fact]
    public void ResourceXmlRequiresARootElement()
    {
        Assert.Throws<XmlException>(() => XmlHmap.GetHeightmap(new XmlDocument()));
        Assert.Throws<XmlException>(() => XmlYfd.GetYfd(new XmlDocument()));
    }

    [Fact]
    public void UnloadedDictionariesCannotBeSaved()
    {
        Assert.Throws<InvalidOperationException>(() => new YfdFile().Save());
        Assert.Throws<InvalidOperationException>(() => new YedFile().Save());
        Assert.Throws<InvalidOperationException>(() => new YldFile().Save());
        Assert.Throws<InvalidOperationException>(() => new YddFile().Save());
    }

    [Fact]
    public void UnloadedRecordsCannotBeSaved()
    {
        Assert.Throws<InvalidOperationException>(() => new YvrFile().Save());
        Assert.Throws<InvalidOperationException>(() => new YwrFile().Save());
    }

    [Fact]
    public void RbfXmlPreservesVectorsAndScalarValues()
    {
        var document = new XmlDocument();
        document.LoadXml("<root><!-- ignored --><position x='1' y='2' z='3'/><enabled value='True'/></root>");
        var root = Assert.IsType<RbfStructure>(XmlRbf.GetRbf(document).current);
        Assert.Equal(2, root.Children.Count);
        var position = Assert.IsType<RbfFloat3>(root.Children[0]);
        Assert.Equal(1f, position.X);
        Assert.Equal(2f, position.Y);
        Assert.Equal(3f, position.Z);
        Assert.True(Assert.IsType<RbfBoolean>(root.Children[1]).Value);

        document.LoadXml("<root value='True'/>");
        Assert.Throws<XmlException>(() => XmlRbf.GetRbf(document));
    }

    [Fact]
    public void TimecycleSampleAllowsMissingOptionalAttributes()
    {
        var document = new XmlDocument();
        document.LoadXml("<sample hour='6' duration='2'/>");
        var sample = new TimecycleSample();
        sample.Init(Assert.IsType<XmlElement>(document.DocumentElement));
        Assert.Equal(string.Empty, sample.name);
        Assert.Equal(string.Empty, sample.uw_tc_mod);
        Assert.Equal(6f, sample.hour);
        Assert.Equal(2f, sample.duration);
    }

    [Fact]
    public void EmptyPoseMatcherRoundTripsThroughBinaryAndXml()
    {
        var original = XmlYpdb.GetYpdb("<PoseMatcher />");
        var bytes = original.Save();
        var loaded = new YpdbFile();
        loaded.Load(bytes, null);
        Assert.Empty(loaded.Samples);
        Assert.Empty(loaded.BoneTags);
        Assert.Empty(Assert.IsType<PoseMatcherWeightSet>(loaded.WeightSet).Weights);
        Assert.Equal(bytes, XmlYpdb.GetYpdb(YpdbXml.GetXml(loaded)).Save());
    }

    [Fact]
    public void EmptyDistantLightCellsHaveNoPaths()
    {
        // Big-endian zero counts followed by the three 1024-entry cell tables.
        var file = new DistantLightsFile();
        file.Load(new byte[8 + 3 * 1024 * sizeof(uint)], null);
        Assert.Empty(file.Nodes);
        Assert.Empty(file.Paths);
        Assert.Equal(1024, file.Cells.Length);
        Assert.All(file.Cells, cell =>
        {
            Assert.Empty(cell.Paths1);
            Assert.Empty(cell.Paths2);
        });
    }

    [Fact]
    public void TimecycleModifierRequiresANameAndLoadsItsValues()
    {
        var document = new XmlDocument();
        document.LoadXml("<modifier><strength>1 2</strength></modifier>");
        var root = Assert.IsType<XmlElement>(document.DocumentElement);
        var modifier = new TimecycleMod();
        Assert.Throws<XmlException>(() => modifier.Init(root));

        root.SetAttribute("name", "TestModifier");
        modifier.Init(root);
        var value = Assert.Single(modifier.Values);
        Assert.Equal("strength", value.name);
        Assert.Equal(1f, value.value1);
        Assert.Equal(2f, value.value2);
        Assert.Same(value, modifier.Dict["strength"]);
    }
}
