using CodeWalker.Rendering;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Xunit;
using Device = SharpDX.Direct3D11.Device;

namespace CodeWalker.WinForms.Tests;

public class DisplayColourTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void DirectSceneOutputEncodesColourOnceAndLeavesAlphaLinear(int samples)
    {
        using var device = new Device(DriverType.Warp, DeviceCreationFlags.None);
        var context = device.ImmediateContext;
        var dx = new DXManager();
        typeof(DXManager).GetProperty("device")!.SetValue(dx, device);
        var description = new Texture2DDescription { Width = 1, Height = 1, ArraySize = 1, MipLevels = 1,
            Format = Format.R8G8B8A8_Typeless, SampleDescription = new SampleDescription(samples, 0),
            BindFlags = BindFlags.RenderTarget };
        using var target = new Texture2D(device, description);
        using var view = new RenderTargetView(device, target, new RenderTargetViewDescription {
            Format = Format.R8G8B8A8_UNorm,
            Dimension = samples > 1 ? RenderTargetViewDimension.Texture2DMultisampled : RenderTargetViewDimension.Texture2D });
        description.SampleDescription = new SampleDescription(1, 0);
        description.Format = Format.R8G8B8A8_UNorm;
        using var resolved = new Texture2D(device, description);
        description.Usage = ResourceUsage.Staging;
        description.BindFlags = BindFlags.None;
        description.CpuAccessFlags = CpuAccessFlags.Read;
        using var staging = new Texture2D(device, description);
        using var vertexCode = ShaderBytecode.Compile("""
            float4 main(uint id : SV_VertexID) : SV_POSITION {
                return float4(id == 1 ? 3 : -1, id == 2 ? 3 : -1, 0, 1);
            }
            """, "main", "vs_4_0");
        using var pixelCode = ShaderBytecode.Compile(
            "float4 main() : SV_TARGET { return float4(0.04, 0.18, 0.5, 0.25); }", "main", "ps_4_0");
        using var vs = new VertexShader(device, vertexCode);
        using var ps = new PixelShader(device, pixelCode);
        using var rasterizer = new RasterizerState(device, new RasterizerStateDescription {
            FillMode = FillMode.Solid, CullMode = CullMode.None, IsMultisampleEnabled = true });
        context.Rasterizer.State = rasterizer;
        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        context.VertexShader.Set(vs);
        context.PixelShader.Set(ps);
        dx.PushExportTargets(target, view, null!, null!, 1, 1);
        try
        {
            foreach (bool encode in new[] { false, true, false, true })
            {
                dx.SetDefaultRenderTarget(context, encode);
                context.Draw(3, 0);
                if (samples > 1) context.ResolveSubresource(target, 0, resolved, 0, Format.R8G8B8A8_UNorm);
                else context.CopyResource(target, resolved);
                context.CopyResource(resolved, staging);
                var mapped = context.MapSubresource(staging, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
                var pixel = Utilities.Read<SharpDX.Color>(mapped.DataPointer);
                context.UnmapSubresource(staging, 0);
                Assert.InRange((int)pixel.R, encode ? 55 : 9, encode ? 57 : 11);
                Assert.InRange((int)pixel.G, encode ? 117 : 45, encode ? 119 : 47);
                Assert.InRange((int)pixel.B, encode ? 187 : 127, encode ? 189 : 129);
                Assert.InRange((int)pixel.A, 63, 65);
            }
        }
        finally
        {
            context.ClearState();
            dx.PopExportTargets();
        }
    }
}
