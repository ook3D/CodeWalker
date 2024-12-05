using System;
using System.Collections.Generic;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace CodeWalker.Rendering;

public class UnitDisc
{
    private readonly VertexBufferBinding vbbinding;

    public UnitDisc(Device device, int segmentCount, bool invert = false)
    {
        SegmentCount = segmentCount;
        var verts = new List<Vector3>();
        var inds = new List<uint>();
        verts.Add(Vector3.Zero);
        var incr = (float)Math.PI * 2.0f / segmentCount;
        for (var i = 0; i < segmentCount; i++)
        {
            var a = incr * i;
            var px = (float)Math.Sin(a);
            var py = (float)Math.Cos(a);
            verts.Add(new Vector3(px, py, 0));
        }

        for (var i = 0; i < segmentCount; i++)
        {
            var ci = (uint)(i == 0 ? segmentCount : i);
            var ni = (uint)i + 1;
            inds.Add(0);
            inds.Add(invert ? ni : ci);
            inds.Add(invert ? ci : ni);
        }

        IndexCount = inds.Count;

        VertexBuffer = Buffer.Create(device, BindFlags.VertexBuffer, verts.ToArray());
        IndexBuffer = Buffer.Create(device, BindFlags.IndexBuffer, inds.ToArray());
        vbbinding = new VertexBufferBinding(VertexBuffer, 12, 0);
    }

    public int SegmentCount { get; set; }
    public int IndexCount { get; set; }
    public Buffer VertexBuffer { get; set; }
    public Buffer IndexBuffer { get; set; }


    public void Draw(DeviceContext context)
    {
        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        context.InputAssembler.SetVertexBuffers(0, vbbinding);
        context.InputAssembler.SetIndexBuffer(IndexBuffer, Format.R32_UInt, 0);
        context.DrawIndexed(IndexCount, 0, 0);
    }

    public void DrawInstanced(DeviceContext context, int instcount)
    {
        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        context.InputAssembler.SetVertexBuffers(0, vbbinding);
        context.InputAssembler.SetIndexBuffer(IndexBuffer, Format.R32_UInt, 0);
        context.DrawIndexedInstanced(IndexCount, instcount, 0, 0, 0);
    }

    public void Dispose()
    {
        if (VertexBuffer != null)
        {
            VertexBuffer.Dispose();
            VertexBuffer = null;
        }

        if (IndexBuffer != null)
        {
            IndexBuffer.Dispose();
            IndexBuffer = null;
        }
    }

    public InputElement[] GetLayout()
    {
        return new[]
        {
            new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0)
        };
    }
}