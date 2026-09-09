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

    [Fact]
    public void PedFlagPreservesPixelConstantBufferLayout()
    {
        Assert.Equal(128, Marshal.SizeOf<BasicShaderPSGeomVars>());
        Assert.Equal(124, Marshal.OffsetOf<BasicShaderPSGeomVars>(nameof(BasicShaderPSGeomVars.UsePedSpecular)).ToInt32());
    }
}


