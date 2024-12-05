using System;
using System.Collections.Generic;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace CodeWalker.Rendering;

public class UnitCapsule
{
    private readonly int indexcount;
    private readonly VertexBufferBinding vbbinding;

    public UnitCapsule(Device device, byte[] vsbytes, int detail, bool invert = false)
    {
        InputLayout = new InputLayout(device, vsbytes, new[]
        {
            new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0)
            //new InputElement("NORMAL", 0, Format.R32G32B32A32_Float, 16, 0),
        });


        var verts = new List<Vector4>();
        var vdict = new Dictionary<Vector4, int>();
        var curtris = new List<SphTri>();
        //List<SphTri> nxttris = new List<SphTri>();

        verts.Add(new Vector4(0.0f, -1.0f, 0.0f, 0.0f)); //top end
        verts.Add(new Vector4(0.0f, 1.0f, 0.0f, 1.0f)); //bottom end

        //detail = nlats each hemisphere
        //nlons = detail*4

        var nlats = detail;
        var nlons = detail * 4;
        var firstlat = 1 - nlats;
        var lastlat = nlats - 1;
        var lastlon = nlons - 1;
        var vertsperlon = 2 + (nlats - 1) * 2;
        var latrng = 1.0f / detail;
        var lonrng = 1.0f / nlons;
        var halfpi = (float)(0.5 * Math.PI);
        var twopi = (float)(2.0 * Math.PI);

        for (var lon = 0; lon < nlons; lon++)
        {
            var tlon = lon * lonrng;
            var rlon = tlon * twopi;
            var lonx = (float)Math.Sin(rlon);
            var lonz = (float)Math.Cos(rlon);
            for (var lat = firstlat; lat < nlats; lat++)
            {
                var tlat = lat * latrng;
                var rlat = tlat * halfpi;
                var laty = (float)Math.Sin(rlat);
                var latxz = (float)Math.Cos(rlat);
                var hemi = lat > 0 ? 1.0f : 0.0f;

                verts.Add(new Vector4(lonx * latxz, laty, lonz * latxz, hemi));

                if (lat == 0) verts.Add(new Vector4(lonx * latxz, laty, lonz * latxz, 1.0f)); //split at the "equator"
            }
        }

        for (var lon = 0; lon < nlons; lon++)
        {
            var i0 = 2 + lon * vertsperlon; //top row
            var i1 = i0 + vertsperlon;
            var i2 = i1 - 1; //bottom row
            var i3 = i2 + vertsperlon;

            if (lon == lastlon)
            {
                i1 = 2;
                i3 = 1 + vertsperlon;
            }

            curtris.Add(new SphTri(0, i1, i0)); //top cap triangles

            for (var lat = firstlat; lat <= lastlat; lat++)
            {
                var offs = lat - firstlat;
                var f1 = i0 + offs;
                var f2 = f1 + vertsperlon;
                var f3 = f1 + 1;
                if (lon == lastlon) f2 = 2 + offs;
                var f4 = f2 + 1;
                curtris.Add(new SphTri(f1, f2, f3));
                curtris.Add(new SphTri(f3, f2, f4)); //fill the rest
            }

            curtris.Add(new SphTri(1, i2, i3)); //bottom cap triangles
        }


        #region cube version (unfinished)

        /* cube version

        verts.Add(new Vector4(-1.0f, 0.0f, 0.0f, 0.0f));
        verts.Add(new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
        verts.Add(new Vector4(0.0f, -1.0f, 0.0f, 0.0f));
        verts.Add(new Vector4(0.0f, 1.0f, 0.0f, 1.0f));//bottom end
        verts.Add(new Vector4(0.0f, 0.0f, -1.0f, 0.0f));
        verts.Add(new Vector4(0.0f, 0.0f, 1.0f, 0.0f));

        verts.Add(new Vector4(-1.0f, 0.0f, 0.0f, 1.0f));//0==6  - bottom equator split
        verts.Add(new Vector4(1.0f, 0.0f, 0.0f, 1.0f));//1==7
        verts.Add(new Vector4(0.0f, 0.0f, -1.0f, 1.0f));//4==8
        verts.Add(new Vector4(0.0f, 0.0f, 1.0f, 1.0f));//5==9

        curtris.Add(new SphTri(0, 4, 2));
        curtris.Add(new SphTri(4, 1, 2));
        curtris.Add(new SphTri(1, 5, 2));
        curtris.Add(new SphTri(5, 0, 2));

        curtris.Add(new SphTri(8, 6, 3));//split halves - Y axis
        curtris.Add(new SphTri(7, 8, 3));
        curtris.Add(new SphTri(9, 7, 3));
        curtris.Add(new SphTri(6, 9, 3));

        for (int i = 0; i < verts.Count; i++)
        {
            vdict[verts[i]] = i;
        }


        for (int i = 0; i < detail; i++)
        {
            nxttris.Clear();
            foreach (var tri in curtris)
            {
                Vector4 v1 = verts[tri.v1];
                Vector4 v2 = verts[tri.v2];
                Vector4 v3 = verts[tri.v3];
                Vector4 s1 = new Vector4(Vector3.Normalize((v1 + v2).XYZ()), v1.W);
                Vector4 s2 = new Vector4(Vector3.Normalize((v2 + v3).XYZ()), v1.W);
                Vector4 s3 = new Vector4(Vector3.Normalize((v3 + v1).XYZ()), v1.W);
                int i1, i2, i3;
                if (!vdict.TryGetValue(s1, out i1))
                {
                    i1 = verts.Count;
                    verts.Add(s1);
                    vdict[s1] = i1;
                }
                if (!vdict.TryGetValue(s2, out i2))
                {
                    i2 = verts.Count;
                    verts.Add(s2);
                    vdict[s2] = i2;
                }
                if (!vdict.TryGetValue(s3, out i3))
                {
                    i3 = verts.Count;
                    verts.Add(s3);
                    vdict[s3] = i3;
                }
                nxttris.Add(new SphTri(tri.v1, i1, i3));
                nxttris.Add(new SphTri(tri.v2, i2, i1));
                nxttris.Add(new SphTri(tri.v3, i3, i2));
                nxttris.Add(new SphTri(i1, i2, i3));
            }
            var cur = curtris;
            curtris = nxttris;
            nxttris = cur;
        }
        */

        #endregion


        var idata = new List<uint>();
        foreach (var tri in curtris)
        {
            idata.Add((uint)tri.v1);
            idata.Add((uint)(invert ? tri.v3 : tri.v2));
            idata.Add((uint)(invert ? tri.v2 : tri.v3));
        }


        VertexBuffer = Buffer.Create(device, BindFlags.VertexBuffer, verts.ToArray());
        vbbinding = new VertexBufferBinding(VertexBuffer, 16, 0);

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