using CodeWalker.GameFiles;
using CodeWalker.Rendering;
using System.Runtime.InteropServices;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class PedMaterialTests
{
    [Theory]
    [InlineData("ped.sps", true)]
    [InlineData("ped_wrinkle_cs.sps", true)]
    [InlineData("ped_hair_spiked.sps", true)]
    [InlineData("ped_default.sps", false)]
    [InlineData("normal_spec.sps", false)]
    [InlineData("vehicle_mesh.sps", false)]
    public void PackedSpecularIsRestrictedToDetailedPedMaterials(string name, bool expected)
    {
        Assert.Equal(expected, PedMaterial.UsesPackedSpecular(JenkHash.GenHash(name)));
    }

    [Theory]
    [InlineData("ped_hair_spiked.sps", true)]
    [InlineData("ped_hair_cutout_alpha.sps", true)]
    [InlineData("ped_hair_cutout_alpha_cloth.sps", true)]
    [InlineData("ped_hair_spiked_mask.sps", true)]
    [InlineData("ped.sps", false)]
    [InlineData("ped_wrinkle_cs.sps", false)]
    [InlineData("normal_spec.sps", false)]
    public void StrandLightingIsRestrictedToHair(string name, bool expected)
    {
        Assert.Equal(expected, PedMaterial.UsesAnisotropicHair(JenkHash.GenHash(name)));
    }

    [Fact]
    public void PedFlagPreservesPixelConstantBufferLayout()
    {
        Assert.Equal(208, Marshal.SizeOf<BasicShaderPSGeomVars>());
        Assert.Equal(124, Marshal.OffsetOf<BasicShaderPSGeomVars>(nameof(BasicShaderPSGeomVars.UsePedSpecular)).ToInt32());
    }
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(8, false)]
    [InlineData(2, true)]
    public void HairNormalCapIsRetainedWithoutDrawingShadowProxyAsColour(int order, bool hidden)
    {
        var geometry = new RenderableGeometry();
        geometry.Init(new DrawableGeometry { Shader = new ShaderFX {
            FileName = JenkHash.GenHash("ped_hair_spiked.sps"), RenderBucket = 3,
            ParametersList = new ShaderParametersBlock {
                Hashes = [(MetaName)ShaderParamNames.orderNumber, (MetaName)ShaderParamNames.anisotropicSpecularIntensity],
                Parameters = [new ShaderParameter { Data = new SharpDX.Vector4(order, 0, 0, 0) },
                    new ShaderParameter { Data = new SharpDX.Vector4(0.05f, 0.15f, 0, 0) }]
            }
        }});
        Assert.Equal(order, geometry.HairOrder);
        Assert.Equal(hidden, geometry.disableRendering);
        Assert.Equal(0.05f, geometry.HairSpecular.Z);
        Assert.Equal(0.15f, geometry.HairSpecular.W);
        var key = new ShaderKey { ShaderFile = JenkHash.GenHash("ped_hair_spiked.sps") };
        var batch = new ShaderBatch(key);
        batch.Geometries.Add(new RenderableGeometryInst { Geom = geometry });
        var bucket = new ShaderRenderBucket(3);
        bucket.Batches.Add(key, batch);
        bucket.GroupBatches();
        Assert.Single(bucket.HairBatches);
        Assert.Empty(bucket.CutoutBatches);
        Assert.Empty(bucket.BasicBatches);
    }
    [Theory]
    [InlineData(0, false, true)]
    [InlineData(0, true, false)]
    [InlineData(1, false, false)]
    [InlineData(1, true, false)]
    [InlineData(8, true, true)]
    public void HairShadowSelectionUsesProxyInsteadOfStrands(int order, bool proxy, bool casts)
    {
        Assert.Equal(casts, PedMaterial.CastsHairShadow(order, proxy));
    }
}
