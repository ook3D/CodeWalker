using System.Reflection;
using CodeWalker.Rendering;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Xunit;
using Device = SharpDX.Direct3D11.Device;

namespace CodeWalker.WinForms.Tests;

public class WaterOverlapTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void OverlappingWaterBlendsNearestSurfaceOnce(int samples)
    {
        using var device = new Device(DriverType.Warp, DeviceCreationFlags.None);
        var context = device.ImmediateContext;
        var shaders = new ShaderManager(device, new DXManager());
        T State<T>(string name) => (T)typeof(ShaderManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shaders)!;
        using var vertexCode = ShaderBytecode.Compile("""
            cbuffer Scene : register(b0) { float4 Depth; };
            float4 main(uint id : SV_VertexID) : SV_POSITION {
                uint corner = id % 3;
                return float4(corner == 1 ? 3 : -1, corner == 2 ? 3 : -1, Depth.x, 1);
            }
            """, "main", "vs_4_0");
        using var pixelCode = ShaderBytecode.Compile("""
            float4 main(float4 position : SV_POSITION) : SV_TARGET {
                return position.z < 0.6 ? float4(1,0,0,0.5) : float4(0,1,0,0.5);
            }
            """, "main", "ps_4_0");
        using var vs = new VertexShader(device, vertexCode);
        using var ps = new PixelShader(device, pixelCode);
        using var constants = new SharpDX.Direct3D11.Buffer(device, 16, ResourceUsage.Default,
            BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        void Draw(int start)
        {
            var value = new Vector4(start == 0 ? 0.4f : 0.8f);
            context.UpdateSubresource(ref value, constants);
            context.Draw(3, 0);
        }
        var description = new Texture2DDescription { Width = 1, Height = 1, ArraySize = 1, MipLevels = 1,
            Format = Format.R8G8B8A8_UNorm, SampleDescription = new SampleDescription(samples, 0), BindFlags = BindFlags.RenderTarget };
        using var target = new Texture2D(device, description);
        using var view = new RenderTargetView(device, target);
        description.Format = Format.D32_Float_S8X24_UInt;
        description.BindFlags = BindFlags.DepthStencil;
        using var depth = new Texture2D(device, description);
        using var depthView = new DepthStencilView(device, depth);
        description.Format = Format.R8G8B8A8_UNorm;
        description.SampleDescription = new SampleDescription(1, 0);
        description.BindFlags = BindFlags.None;
        using var resolved = new Texture2D(device, description);
        description.Usage = ResourceUsage.Staging;
        description.CpuAccessFlags = CpuAccessFlags.Read;
        using var staging = new Texture2D(device, description);
        using var rasterizer = new RasterizerState(device, new RasterizerStateDescription {
            FillMode = FillMode.Solid, CullMode = CullMode.None, IsMultisampleEnabled = true });
        try
        {
            context.Rasterizer.State = rasterizer;
            context.Rasterizer.SetViewport(0, 0, 1, 1);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.VertexShader.Set(vs);
            context.VertexShader.SetConstantBuffer(0, constants);
            context.PixelShader.Set(ps);
            context.OutputMerger.SetRenderTargets(depthView, view);
            // Duplicates, both height orders, and opaque geometry in front of water.
            foreach (var order in new[] { new[] { 0, 0 }, new[] { 0, 3 }, new[] { 3, 0 } })
            foreach (var sceneDepth in new[] { 0f, 0.9f })
            {
                context.ClearRenderTargetView(view, new Color4(0, 0, 1, 1));
                context.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, sceneDepth, 0);
                context.OutputMerger.DepthStencilState = State<DepthStencilState>("dsEnabled");
                context.OutputMerger.BlendState = State<BlendState>("bsWaterDepth");
                foreach (var start in order) Draw(start);
                context.OutputMerger.SetDepthStencilState(State<DepthStencilState>("dsWaterColour"), 1);
                context.OutputMerger.BlendState = State<BlendState>("bsDefault");
                foreach (var start in order) Draw(start);
                if (samples > 1) context.ResolveSubresource(target, 0, resolved, 0, Format.R8G8B8A8_UNorm);
                else context.CopyResource(target, resolved);
                context.CopyResource(resolved, staging);
                var mapped = context.MapSubresource(staging, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
                var pixel = Utilities.Read<SharpDX.Color>(mapped.DataPointer);
                context.UnmapSubresource(staging, 0);
                bool hidden = sceneDepth > 0;
                bool green = order.Contains(3);
                Assert.True(pixel.R >= (hidden || green ? 0 : 127) && pixel.R <= (hidden || green ? 0 : 128), $"order={string.Join(',', order)}, sceneDepth={sceneDepth}, pixel={pixel}");
                Assert.InRange((int)pixel.G, hidden || !green ? 0 : 127, hidden || !green ? 0 : 128);
                Assert.InRange((int)pixel.B, hidden ? 255 : 127, hidden ? 255 : 128);
            }
        }
        finally
        {
            context.ClearState();
            shaders.Dispose();
        }
    }
}
