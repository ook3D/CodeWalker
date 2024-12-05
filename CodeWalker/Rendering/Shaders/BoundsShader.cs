using System;
using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace CodeWalker.Rendering;

public struct BoundingSphereVSSceneVars
{
    public Matrix ViewProj;
    public Matrix ViewInv;
    public float SegmentCount;
    public float VertexCount;
    public float Pad1;
    public float Pad2;
}

public struct BoundingSphereVSSphereVars
{
    public Vector3 Center;
    public float Radius;
}

public struct BoundingBoxVSSceneVars
{
    public Matrix ViewProj;
}

public struct BoundingBoxVSBoxVars
{
    public Quaternion Orientation;
    public Vector4 BBMin;
    public Vector4 BBRng; //max-min
    public Vector3 CamRel;
    public float Pad1;
    public Vector3 Scale;
    public float Pad2;
}

public struct BoundsPSColourVars
{
    public Vector4 Colour;
}

public class BoundsShader : Shader, IDisposable
{
    private readonly PixelShader boundsps;
    private readonly VertexShader boxvs;
    private readonly UnitCube cube;
    private bool disposed;

    private BoundsShaderMode mode = BoundsShaderMode.Sphere;
    private readonly GpuVarsBuffer<BoundsPSColourVars> PSColourVars;

    private readonly int SegmentCount = 64;
    private readonly VertexShader spherevs;
    private readonly int VertexCount = 65;
    private readonly GpuVarsBuffer<BoundingBoxVSSceneVars> VSBoxSceneVars;
    private readonly GpuVarsBuffer<BoundingBoxVSBoxVars> VSBoxVars;

    private readonly GpuVarsBuffer<BoundingSphereVSSceneVars> VSSphereSceneVars;
    private readonly GpuVarsBuffer<BoundingSphereVSSphereVars> VSSphereVars;

    public BoundsShader(Device device)
    {
        var spherevsbytes = PathUtil.ReadAllBytes("Shaders\\BoundingSphereVS.cso");
        var boxvsbytes = PathUtil.ReadAllBytes("Shaders\\BoundingBoxVS.cso");
        var psbytes = PathUtil.ReadAllBytes("Shaders\\BoundsPS.cso");

        spherevs = new VertexShader(device, spherevsbytes);
        boxvs = new VertexShader(device, boxvsbytes);
        boundsps = new PixelShader(device, psbytes);

        VSSphereSceneVars = new GpuVarsBuffer<BoundingSphereVSSceneVars>(device);
        VSSphereVars = new GpuVarsBuffer<BoundingSphereVSSphereVars>(device);
        VSBoxSceneVars = new GpuVarsBuffer<BoundingBoxVSSceneVars>(device);
        VSBoxVars = new GpuVarsBuffer<BoundingBoxVSBoxVars>(device);
        PSColourVars = new GpuVarsBuffer<BoundsPSColourVars>(device);

        cube = new UnitCube(device, boxvsbytes, false, true, false);
    }


    public void Dispose()
    {
        if (disposed) return;

        VSSphereSceneVars.Dispose();
        VSSphereVars.Dispose();
        VSBoxSceneVars.Dispose();
        VSBoxVars.Dispose();
        PSColourVars.Dispose();

        cube.Dispose();

        boundsps.Dispose();
        boxvs.Dispose();
        spherevs.Dispose();

        disposed = true;
    }


    public void SetMode(BoundsShaderMode m)
    {
        mode = m;
    }

    public override void SetShader(DeviceContext context)
    {
        switch (mode)
        {
            default:
            case BoundsShaderMode.Sphere:
                context.VertexShader.Set(spherevs);
                break;
            case BoundsShaderMode.Box:
                context.VertexShader.Set(boxvs);
                break;
        }

        context.PixelShader.Set(boundsps);
    }

    public override bool SetInputLayout(DeviceContext context, VertexType type)
    {
        //switch (mode)
        //{
        //    default:
        //    case BoundsShaderMode.Sphere:
        //        context.InputAssembler.InputLayout = null;
        //        break;
        //    case BoundsShaderMode.Box:
        //        context.InputAssembler.InputLayout = cube.InputLayout;
        //        break;
        //}
        return true;
    }

    public override void SetSceneVars(DeviceContext context, Camera camera, Shadowmap shadowmap,
        ShaderGlobalLights lights)
    {
        switch (mode)
        {
            default:
            case BoundsShaderMode.Sphere:
                VSSphereSceneVars.Vars.ViewProj = Matrix.Transpose(camera.ViewProjMatrix);
                VSSphereSceneVars.Vars.ViewInv = Matrix.Transpose(camera.ViewInvMatrix);
                VSSphereSceneVars.Vars.SegmentCount = SegmentCount;
                VSSphereSceneVars.Vars.VertexCount = VertexCount;
                VSSphereSceneVars.Update(context);
                VSSphereSceneVars.SetVSCBuffer(context, 0);
                break;
            case BoundsShaderMode.Box:
                VSBoxSceneVars.Vars.ViewProj = Matrix.Transpose(camera.ViewProjMatrix);
                VSBoxSceneVars.Update(context);
                VSBoxSceneVars.SetVSCBuffer(context, 0);
                break;
        }
    }

    public void SetSphereVars(DeviceContext context, Vector3 center, float radius)
    {
        VSSphereVars.Vars.Center = center;
        VSSphereVars.Vars.Radius = radius;
        VSSphereVars.Update(context);
        VSSphereVars.SetVSCBuffer(context, 1);
    }

    public void SetBoxVars(DeviceContext context, Vector3 camrel, Vector3 bbmin, Vector3 bbmax, Quaternion orientation,
        Vector3 scale)
    {
        VSBoxVars.Vars.Orientation = orientation;
        VSBoxVars.Vars.BBMin = new Vector4(bbmin, 0.0f);
        VSBoxVars.Vars.BBRng = new Vector4(bbmax - bbmin, 0.0f);
        VSBoxVars.Vars.CamRel = camrel;
        VSBoxVars.Vars.Scale = scale;
        VSBoxVars.Update(context);
        VSBoxVars.SetVSCBuffer(context, 1);
    }

    public void SetColourVars(DeviceContext context, Vector4 colour)
    {
        PSColourVars.Vars.Colour = colour;
        PSColourVars.Update(context);
        PSColourVars.SetPSCBuffer(context, 0);
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

    public override void UnbindResources(DeviceContext context)
    {
        context.VertexShader.SetConstantBuffer(0, null);
        context.VertexShader.SetConstantBuffer(1, null);
        context.PixelShader.SetConstantBuffer(0, null);
        context.VertexShader.Set(null);
        context.PixelShader.Set(null);
    }


    public void DrawSphere(DeviceContext context)
    {
        context.InputAssembler.InputLayout = null;
        context.InputAssembler.SetIndexBuffer(null, Format.Unknown, 0);
        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.LineStrip;
        context.Draw(VertexCount, 0);
    }

    public void DrawBox(DeviceContext context)
    {
        cube.Draw(context);
    }
}

public enum BoundsShaderMode
{
    None = 0,
    Sphere = 1,
    Box = 2
}