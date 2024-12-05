using System;
using System.Collections.Generic;
using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace CodeWalker.Rendering;

public struct PathShaderVSSceneVars
{
    public Matrix ViewProj;
    public Vector4 CameraPos;
    public Vector4 LightColour;
}

public class PathShader : Shader, IDisposable
{
    private readonly PixelShader boxps;

    private readonly VertexShader boxvs;

    private readonly UnitCube cube;
    private bool disposed;
    private readonly VertexShader dynvs;

    private readonly InputLayout layout;
    private readonly PixelShader ps;

    private bool UseDynamicVerts;
    private readonly GpuCBuffer<VertexTypePC> vertices; //for selection polys/lines use
    private readonly VertexShader vs;

    private readonly GpuVarsBuffer<PathShaderVSSceneVars> VSSceneVars;


    public PathShader(Device device)
    {
        var boxvsbytes = PathUtil.ReadAllBytes("Shaders\\PathBoxVS.cso");
        var boxpsbytes = PathUtil.ReadAllBytes("Shaders\\PathBoxPS.cso");
        var dynvsbytes = PathUtil.ReadAllBytes("Shaders\\PathDynVS.cso");
        var vsbytes = PathUtil.ReadAllBytes("Shaders\\PathVS.cso");
        var psbytes = PathUtil.ReadAllBytes("Shaders\\PathPS.cso");


        boxvs = new VertexShader(device, boxvsbytes);
        boxps = new PixelShader(device, boxpsbytes);
        dynvs = new VertexShader(device, dynvsbytes);
        vs = new VertexShader(device, vsbytes);
        ps = new PixelShader(device, psbytes);

        VSSceneVars = new GpuVarsBuffer<PathShaderVSSceneVars>(device);

        layout = new InputLayout(device, vsbytes, VertexTypeGTAV.GetLayout(VertexType.PC));

        cube = new UnitCube(device, boxvsbytes, true, false, true);

        vertices = new GpuCBuffer<VertexTypePC>(device, 1000); //should be more than needed....
    }


    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        VSSceneVars.Dispose();

        vertices.Dispose();

        layout.Dispose();
        cube.Dispose();

        ps.Dispose();
        vs.Dispose();
        dynvs.Dispose();
        boxvs.Dispose();
        boxps.Dispose();
    }


    public override void SetShader(DeviceContext context)
    {
        if (UseDynamicVerts)
        {
            context.VertexShader.Set(dynvs);
            //context.InputAssembler.SetVertexBuffers(0, null);
            context.InputAssembler.SetIndexBuffer(null, Format.Unknown, 0);
        }
        else
        {
            context.VertexShader.Set(vs);
        }

        context.PixelShader.Set(ps);
    }

    public override bool SetInputLayout(DeviceContext context, VertexType type)
    {
        if (UseDynamicVerts)
            context.InputAssembler.InputLayout = null;
        else
            context.InputAssembler.InputLayout = layout;
        return true;
    }

    public override void SetSceneVars(DeviceContext context, Camera camera, Shadowmap shadowmap,
        ShaderGlobalLights lights)
    {
        VSSceneVars.Vars.ViewProj = Matrix.Transpose(camera.ViewProjMatrix);
        VSSceneVars.Vars.CameraPos = new Vector4(camera.Position, 0.0f);
        VSSceneVars.Vars.LightColour = new Vector4(1.0f, 1.0f, 1.0f, lights.HdrIntensity * 2.0f);
        VSSceneVars.Update(context);
        VSSceneVars.SetVSCBuffer(context, 0);
    }

    public override void SetEntityVars(DeviceContext context, ref RenderableInst rend)
    {
        //don't use this one
    }

    public override void SetModelVars(DeviceContext context, RenderableModel model)
    {
        //don't use this
    }

    public override void SetGeomVars(DeviceContext context, RenderableGeometry geom)
    {
        //don't use this
    }


    public void RenderBatches(DeviceContext context, List<RenderablePathBatch> batches, Camera camera,
        ShaderGlobalLights lights)
    {
        UseDynamicVerts = false;
        SetShader(context);
        SetInputLayout(context, VertexType.PC);
        SetSceneVars(context, camera, null, lights);

        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        context.InputAssembler.SetIndexBuffer(null, Format.R16_UInt, 0);
        for (var i = 0; i < batches.Count; i++)
        {
            var pbatch = batches[i];

            if (pbatch.TriangleVertexBuffer == null) continue;
            if (pbatch.TriangleVertexCount == 0) continue;

            context.InputAssembler.SetVertexBuffers(0, pbatch.TriangleVBBinding);
            context.Draw(pbatch.TriangleVertexCount, 0);
        }

        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.LineList;
        context.InputAssembler.SetIndexBuffer(null, Format.R16_UInt, 0);
        for (var i = 0; i < batches.Count; i++)
        {
            var pbatch = batches[i];

            if (pbatch.PathVertexBuffer == null) continue;
            if (pbatch.PathVertexCount == 0) continue;

            context.InputAssembler.SetVertexBuffers(0, pbatch.PathVBBinding);
            context.Draw(pbatch.PathVertexCount, 0);
        }


        context.VertexShader.Set(boxvs);
        context.PixelShader.Set(boxps);

        VSSceneVars.SetVSCBuffer(context, 0);

        foreach (var batch in batches)
        {
            if (batch.NodeBuffer == null) continue;

            context.VertexShader.SetShaderResource(0, batch.NodeBuffer.SRV);

            cube.DrawInstanced(context, batch.Nodes.Length);
        }

        UnbindResources(context);
    }

    public void RenderTriangles(DeviceContext context, List<VertexTypePC> verts, Camera camera,
        ShaderGlobalLights lights)
    {
        UseDynamicVerts = true;
        SetShader(context);
        SetInputLayout(context, VertexType.PC);
        SetSceneVars(context, camera, null, lights);

        var drawn = 0;
        var tricount = verts.Count / 3;
        var maxcount = vertices.StructCount / 3;
        while (drawn < tricount)
        {
            vertices.Clear();

            var offset = drawn * 3;
            var bcount = Math.Min(tricount - drawn, maxcount);
            for (var i = 0; i < bcount; i++)
            {
                var t = offset + i * 3;
                vertices.Add(verts[t + 0]);
                vertices.Add(verts[t + 1]);
                vertices.Add(verts[t + 2]);
            }

            drawn += bcount;

            vertices.Update(context);
            vertices.SetVSResource(context, 0);

            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.Draw(vertices.CurrentCount, 0);
        }
    }

    public void RenderLines(DeviceContext context, List<VertexTypePC> verts, Camera camera, ShaderGlobalLights lights)
    {
        UseDynamicVerts = true;
        SetShader(context);
        SetInputLayout(context, VertexType.PC);
        SetSceneVars(context, camera, null, lights);

        var drawn = 0;
        var linecount = verts.Count / 2;
        var maxcount = vertices.StructCount / 2;
        while (drawn < linecount)
        {
            vertices.Clear();

            var offset = drawn * 2;
            var bcount = Math.Min(linecount - drawn, maxcount);
            for (var i = 0; i < bcount; i++)
            {
                var t = offset + i * 2;
                vertices.Add(verts[t + 0]);
                vertices.Add(verts[t + 1]);
            }

            drawn += bcount;

            vertices.Update(context);
            vertices.SetVSResource(context, 0);

            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.LineList;
            context.Draw(vertices.CurrentCount, 0);
        }
    }


    public override void UnbindResources(DeviceContext context)
    {
        context.VertexShader.SetConstantBuffer(0, null);
        context.VertexShader.SetShaderResource(0, null);
        context.VertexShader.Set(null);
        context.PixelShader.Set(null);
    }
}