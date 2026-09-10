using System.Numerics;
using System.Runtime.InteropServices;
using CodeWalker.Rendering;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using Xunit;
using Buffer = SharpDX.Direct3D11.Buffer;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace CodeWalker.WinForms.Tests;

public class GpuBufferTests
{
    [Fact]
    public void EmptyConstantBufferUpdatePreservesPreviousMatrices()
    {
        using var device = new Device(DriverType.Warp, DeviceCreationFlags.None);
        var buffer = new GpuABuffer<Vector4>(device, 3);
        try
        {
            Vector4[] matrixRows = [Vector4.UnitX, Vector4.UnitY, Vector4.UnitZ];
            buffer.Update(device.ImmediateContext, matrixRows);
            buffer.Update(device.ImmediateContext, []);

            Assert.Equal(matrixRows, ReadBuffer(device, buffer.Buffer!, 3));
        }
        finally
        {
            buffer.Dispose();
        }
    }

    [Fact]
    public void EmptyStructuredBufferUpdatesPreservePreviousData()
    {
        using var device = new Device(DriverType.Warp, DeviceCreationFlags.None);
        var buffer = new GpuCBuffer<Vector4>(device, 1);
        try
        {
            Vector4[] values = [new Vector4(1, 2, 3, 4)];
            buffer.Update(device.ImmediateContext, values);
            buffer.Update(device.ImmediateContext, []);
            buffer.Clear();
            buffer.Update(device.ImmediateContext);

            Assert.Equal(values, ReadBuffer(device, buffer.Buffer!, 1));
            Assert.Equal(0, buffer.CurrentCount);

            buffer.Add(Vector4.One);
            buffer.Update(device.ImmediateContext);
            Assert.Equal(new[] { Vector4.One }, ReadBuffer(device, buffer.Buffer!, 1));
        }
        finally
        {
            buffer.Dispose();
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void HairStencilBufferRetainsFloatDepthSampling(int samples)
    {
        using var device = new Device(DriverType.Warp, DeviceCreationFlags.None);
        var buffers = new GpuMultiTexture(device, 8, 8, 4, SharpDX.DXGI.Format.R8G8B8A8_UNorm,
            true, SharpDX.DXGI.Format.D32_Float_S8X24_UInt, samples);
        try
        {
            buffers.SetRenderTargets(device.ImmediateContext);
            buffers.Clear(device.ImmediateContext, new SharpDX.Color4(0));
            device.ImmediateContext.ClearDepthStencilView(buffers.DSV, DepthStencilClearFlags.Stencil, 0, 0);
            Assert.Equal(SharpDX.DXGI.Format.R32_Float_X8X24_Typeless, buffers.DepthSRV!.Description.Format);
            Assert.Equal(SharpDX.DXGI.Format.D32_Float_S8X24_UInt, buffers.DSV!.Description.Format);
        }
        finally { buffers.Dispose(); }
    }

    private static Vector4[] ReadBuffer(Device device, Buffer source, int count)
    {
        using var staging = new Buffer(device, new BufferDescription
        {
            SizeInBytes = source.Description.SizeInBytes,
            Usage = ResourceUsage.Staging,
            CpuAccessFlags = CpuAccessFlags.Read,
        });
        var context = device.ImmediateContext;
        context.CopyResource(source, staging);
        var mapped = context.MapSubresource(staging, 0, MapMode.Read, MapFlags.None);
        try
        {
            var result = new Vector4[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = Marshal.PtrToStructure<Vector4>(mapped.DataPointer + i * Marshal.SizeOf<Vector4>());
            }
            return result;
        }
        finally
        {
            context.UnmapSubresource(staging, 0);
        }
    }
}
