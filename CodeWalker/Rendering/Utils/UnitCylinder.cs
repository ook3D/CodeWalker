using System;
using System.Collections.Generic;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace CodeWalker.Rendering;

public class UnitCylinder
{
    private readonly int indexcount;
    private readonly VertexBufferBinding vbbinding;

    public UnitCylinder(Device device, byte[] vsbytes, int detail)
    {
        InputLayout = new InputLayout(device, vsbytes, new[]
        {
            new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
            new InputElement("NORMAL", 0, Format.R32G32B32A32_Float, 16, 0)
        });


        var verts = new List<Vector4>();
        var vdict = new Dictionary<Vector4, int>();
        var curtris = new List<SphTri>();
        //List<SphTri> nxttris = new List<SphTri>();

        verts.Add(new Vector4(0.0f, 0.0f, 0.0f, 0.0f)); //top end (translated by VS!)
        verts.Add(new Vector4(0.0f, -1.0f, 0.0f, 0.0f)); //top normal
        verts.Add(new Vector4(0.0f, 0.0f, 0.0f, 1.0f)); //bottom end
        verts.Add(new Vector4(0.0f, 1.0f, 0.0f, 1.0f)); //bottom normal

        var nlons = detail * 4;
        var lastlon = nlons - 1;
        var latrng = 1.0f / detail;
        var lonrng = 1.0f / nlons;
        var twopi = (float)(2.0 * Math.PI);

        for (var lon = 0; lon < nlons; lon++)
        {
            var tlon = lon * lonrng;
            var rlon = tlon * twopi;
            var lonx = (float)Math.Sin(rlon);
            var lonz = (float)Math.Cos(rlon);

            verts.Add(new Vector4(lonx, 0.0f, lonz, 0.0f)); //0
            verts.Add(new Vector4(0.0f, -1.0f, 0.0f, 0.0f)); //top normal
            verts.Add(new Vector4(lonx, 0.0f, lonz, 0.0f)); //1
            verts.Add(new Vector4(lonx, 0.0f, lonz, 0.0f)); //side normal
            verts.Add(new Vector4(lonx, 0.0f, lonz, 1.0f)); //2
            verts.Add(new Vector4(lonx, 0.0f, lonz, 0.0f)); //side normal
            verts.Add(new Vector4(lonx, 0.0f, lonz, 1.0f)); //3
            verts.Add(new Vector4(0.0f, 1.0f, 0.0f, 0.0f)); //bottom normal
        }

        for (var lon = 0; lon < nlons; lon++)
        {
            var i0 = 2 + lon * 4; // vertsperlon;//top row
            var i1 = i0 + 4; // vertsperlon;
            var i2 = i0 + 3; //bottom row
            var i3 = i2 + 4; // vertsperlon;
            var f1 = i0 + 1;
            var f2 = f1 + 4; // vertsperlon;

            if (lon == lastlon)
            {
                i1 = 2;
                i3 = 5; // 1 + vertsperlon;
                f2 = 3; // + offs;
            }


            curtris.Add(new SphTri(0, i1, i0)); //top cap triangles

            curtris.Add(new SphTri(f1, f2, f1 + 1));
            curtris.Add(new SphTri(f1 + 1, f2, f2 + 1)); //fill the rest

            curtris.Add(new SphTri(1, i2, i3)); //bottom cap triangles
        }


        var idata = new List<uint>();
        foreach (var tri in curtris)
        {
            idata.Add((uint)tri.v1);
            idata.Add((uint)tri.v2);
            idata.Add((uint)tri.v3);
        }


        VertexBuffer = Buffer.Create(device, BindFlags.VertexBuffer, verts.ToArray());
        vbbinding = new VertexBufferBinding(VertexBuffer, 32, 0);

        IndexBuffer = Buffer.Create(device, BindFlags.IndexBuffer, idata.ToArray());
        indexcount = idata.Count;
    }

    private Buffer VertexBuffer { get; set; }
    private Buffer IndexBuffer { get; set; }
    private InputLayout InputLayout { get; set; }


    public void Draw(DeviceContext context)
    {
        context.InputAssembler.InputLayout = InputLayout;
        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        context.InputAssembler.SetVertexBuffers(0, vbbinding);
        context.InputAssembler.SetIndexBuffer(IndexBuffer, Format.R32_UInt, 0);

        context.DrawIndexed(indexcount, 0, 0);
    }

    public void DrawInstanced(DeviceContext context, int count)
    {
        context.InputAssembler.InputLayout = InputLayout;
        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        context.InputAssembler.SetVertexBuffers(0, vbbinding);
        context.InputAssembler.SetIndexBuffer(IndexBuffer, Format.R32_UInt, 0);

        context.DrawIndexedInstanced(indexcount, count, 0, 0, 0);
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

        if (InputLayout != null)
        {
            InputLayout.Dispose();
            InputLayout = null;
        }
    }

    private struct SphTri
    {
        public readonly int v1;
        public readonly int v2;
        public readonly int v3;

        public SphTri(int i1, int i2, int i3)
        {
            v1 = i1;
            v2 = i2;
            v3 = i3;
        }
    }
}