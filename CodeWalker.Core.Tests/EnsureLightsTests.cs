using CodeWalker.GameFiles;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class EnsureLightsTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CullingBoundsPreserveLodGeneratorInputs(bool fragment, bool boneAttached)
    {
        var attributes = new[] {
            new CLightAttr { Position = new Vector3(1, 2, 3), Direction = Vector3.UnitZ,
                Type = LightType.Capsule, BoneTag = 7, Extents = new Vector3(5, 0, 0),
                Falloff = 100, FalloffExponent = 2, Intensity = 8, ColorR = 255, ColorG = 128,
                ColorB = 64, TimeFlags = 0x00FF0000, ConeInnerAngle = 20, ConeOuterAngle = 45,
                CoronaIntensity = 2, CoronaSize = 1 },
            new CLightAttr { Position = new Vector3(-4, 0, 0), Direction = Vector3.UnitX,
                Type = LightType.Point, Falloff = 20, Intensity = 4 }
        };
        var originalBytes = attributes.Select(Serialize).ToArray();
        rmcDrawable drawable = fragment
            ? new FragDrawable { OwnerFragment = new FragType {
                LightAttributes = new ResourceSimpleList64<CLightAttr> { data_items = attributes } } }
            : new gtaDrawable { Lights = new atArray<CLightAttr> { data_items = attributes } };
        drawable.BoundingBoxMin = new Vector3(-2);
        drawable.BoundingBoxMax = new Vector3(2);
        if (boneAttached)
            drawable.SkeletonData = new crSkeletonData { BonesMap = new() {
                [7] = new crBoneData { AbsTransform = Matrix.Translation(10, 0, 0) } } };
        var entity = new YmapEntityDef {
            Archetype = new Archetype { BBMin = new Vector3(-1), BBMax = new Vector3(1) },
            Position = new Vector3(100, 200, 300), Orientation = Quaternion.Identity, Scale = new Vector3(2),
            BBMin = new Vector3(98, 198, 298), BBMax = new Vector3(102, 202, 302),
            BBCenter = new Vector3(100, 200, 300), BBExtent = new Vector3(2)
        };
        var boundsBefore = GeneratorBounds(entity);
        entity.EnsureLights(drawable);
        var lights = entity.Lights!;
        Assert.Equal(2, lights.Length);
        Assert.Equal(new Vector3(boneAttached ? 111 : 101, 202, 303), lights[0].Position);
        Assert.Equal(Vector3.UnitZ, lights[0].Direction);
        Assert.Equal(new Vector3(96, 200, 300), lights[1].Position);
        Assert.Equal(Vector3.UnitX, lights[1].Direction);
        for (int i = 0; i < lights.Length; i++)
        {
            Assert.Same(attributes[i], lights[i].Attributes);
            Assert.Equal(originalBytes[i], Serialize(attributes[i]));
            // EnsureLights hashes drawable bounds; the generator independently uses archetype bounds.
            Assert.Equal(YmapEntityDef.ComputeLightHash([960, 1960, 2960, 1040, 2040, 3040, (uint)i]), lights[i].Hash);
        }
        Assert.Equal(boundsBefore, GeneratorBounds(entity));
        Assert.Equal(new Vector3(98, 198, 298), entity.BBMin);
        Assert.Equal(new Vector3(102, 202, 302), entity.BBMax);
        Assert.Equal(entity.Position, entity.BBCenter);
        Assert.Equal(new Vector3(2), entity.BBExtent);
        Assert.Equal(new Vector3(-2), drawable.BoundingBoxMin);
        Assert.Equal(new Vector3(2), drawable.BoundingBoxMax);
        Assert.True(entity.LightsBBExtent.X > entity.BBExtent.X);
        // Rendering before generation must leave the same cached lights available.
        entity.EnsureLights(drawable);
        Assert.Same(lights, entity.Lights);
    }

    private static BoundingBox GeneratorBounds(YmapEntityDef entity) =>
        new BoundingBox(entity.Archetype!.BBMin, entity.Archetype.BBMax)
            .Transform(entity.Position, entity.Orientation, entity.Scale);

    private static byte[] Serialize(CLightAttr light)
    {
        using var data = new MemoryStream();
        using var graphics = new MemoryStream();
        light.Write(new ResourceDataWriter(data, graphics) { Position = 0x50000000 });
        return data.ToArray();
    }
}
