using System.Text;
using System.Xml;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class NullableDataFileTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(22)]
    [InlineData(54)]
    [InlineData(151)]
    public void EmptyRelRoundTrips(int type)
    {
        var original = XmlRel.GetRel($"<Dat{type}><Version value=\"1\" /></Dat{type}>");
        var loaded = new RelFile();
        loaded.Load(original.Save(), new RpfBinaryFileEntry { Name = "test.rel" });
        Assert.Equal((RelDatFileType)type, loaded.RelType);
        Assert.Empty(loaded.RelDatas);
        Assert.Empty(loaded.RelDatasSorted);
        Assert.Equal(original.Save(), loaded.Save());
    }

    [Fact]
    public void UnknownSoundTypeKeepsItsTypeId()
    {
        var rel = new RelFile();
        var sound = rel.CreateRelData(RelDatFileType.Dat54DataEntries, 255);
        Assert.IsType<Dat54Sound>(sound);
        Assert.Equal((byte)255, sound.TypeID);
    }

    [Fact]
    public void PedComponentLookupRejectsMissingAndOutOfRangeIndices()
    {
        var variation = new MCPedVariationInfo();
        Assert.Null(variation.GetComponentData(0));
        variation.ComponentIndices = [0];
        variation.ComponentData3 = [];
        Assert.Null(variation.GetComponentData(0));
        Assert.Null(variation.GetComponentData(1));
        Assert.Null(variation.GetComponentData(-1));
        Assert.Null(variation.GetComponentData(12));
    }

    [Fact]
    public void MissingSynthOperandsReportAnError()
    {
        var errors = new List<string>();
        Dat10Synth.Assemble("ADD => R0", [], (message, line) => errors.Add(message));
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void SparseVehicleConfigurationsHaveEmptyCollections()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<Item />");
        var node = doc.DocumentElement ?? throw new InvalidOperationException();
        Assert.Empty(new CVehicleModelInfoVarGlobal(node).Colors);
        Assert.Empty(new CVehicleModelInfoVariation(node).variationData);
        Assert.Empty(new CVehicleModColours(node).metallic);
        Assert.Empty(new CPedModelInfo__InitDataList(node).InitDatas);
        var vehicle = new VehicleInitData();
        vehicle.Load(node);
        Assert.Empty(vehicle.drivers);
        Assert.Empty(vehicle.lodDistances);
    }

    [Fact]
    public void SparseShaderGroupsRoundTrip()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<Fxc><Shaders /></Fxc>");
        var shader = new FxcFile();
        shader.ReadXml(doc.DocumentElement ?? throw new InvalidOperationException(), string.Empty);
        var bytes = shader.Save();
        var loaded = new FxcFile();
        loaded.Load(bytes, new RpfBinaryFileEntry { Name = "empty.fxc" });
        Assert.Equal(6, loaded.ShaderGroups.Length);
        Assert.Empty(loaded.Shaders);
        Assert.Equal(bytes, loaded.Save());
    }

    [Fact]
    public void EmptyAudioContainerRoundTrips()
    {
        var audio = new AwcFile();
        var bytes = audio.Save();
        var loaded = new AwcFile();
        loaded.Load(bytes, new RpfBinaryFileEntry { Name = "empty.awc" });
        Assert.Empty(loaded.Streams);
        Assert.Equal(bytes, loaded.Save());
    }

    [Fact]
    public void ClothReinitializationClearsPreviousGeometryAndOwner()
    {
        var cloth = new ClothInstance();
        cloth.Init(new CharacterCloth(), new crSkeletonData());
        cloth.Vertices = [SharpDX.Vector4.One];
        cloth.Bones = [new crBoneData()];
        var environment = new EnvironmentCloth();
        cloth.Init(environment, new crSkeletonData());
        Assert.Empty(cloth.Vertices);
        Assert.Empty(cloth.Bones);
        Assert.Null(cloth.CharCloth);
        Assert.Same(environment, cloth.EnvCloth);
    }

    [Fact]
    public void SparseMovementXmlProducesEmptyDebugGraphs()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<Movement />");
        var movement = new MrfFile();
        movement.ReadXml(doc.DocumentElement ?? throw new InvalidOperationException());
        Assert.Empty(movement.AllNodes);
        Assert.Null(movement.RootState);
        Assert.NotNull(movement.DebugTreeGraph);
        Assert.NotNull(movement.DebugStateGraph);
    }

    [Fact]
    public void EmptySkeletonCloneHasIndependentCollections()
    {
        var original = new crSkeletonData();
        var clone = original.Clone();
        Assert.Empty(clone.ParentIndices);
        Assert.Empty(clone.ChildParentIndices);
        Assert.Empty(clone.DefaultTransforms);
        Assert.Empty(clone.CumulativeInverseTransforms);
    }

    [Fact]
    public void EmptyFragmentResourceRoundTrips()
    {
        var original = new YftFile { Fragment = new FragType() };
        var bytes = original.Save();
        var loaded = new YftFile();
        loaded.Load(bytes);
        Assert.NotNull(loaded.Fragment);
        Assert.Empty(loaded.Fragment.Cloths.data_items);
        Assert.Equal(bytes, loaded.Save());
    }

    [Fact]
    public void ParticleEffectPreservesPointerCapacityOnRoundTrip()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<Item><Name>test</Name></Item>");
        var effect = new ParticleEffectRule();
        effect.ReadXml(doc.DocumentElement ?? throw new InvalidOperationException());
        var original = new YptFile
        {
            PtfxList = new ParticleEffectsList
            {
                EffectRuleDictionary = new ParticleEffectRuleDictionary
                {
                    EffectRuleNameHashes = new ResourceSimpleList64_s<MetaHash> { data_items = [123] },
                    EffectRules = new ResourcePointerList64<ParticleEffectRule> { data_items = [effect] }
                }
            }
        };
        var bytes = original.Save();
        var loaded = new YptFile();
        loaded.Load(bytes);
        var loadedEffect = Assert.Single(loaded.AllEffects);
        Assert.Equal(32, loadedEffect.EventEmitters?.data_items.Length);
        Assert.Equal(16, loadedEffect.KeyframeProps?.data_items.Length);
        Assert.Equal(bytes, loaded.Save());
    }
}
