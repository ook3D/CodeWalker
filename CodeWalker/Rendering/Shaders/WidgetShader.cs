using System;
using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace CodeWalker.Rendering;

public struct WidgetShaderSceneVars
{
    public Matrix ViewProj;
    public uint Mode; //0=Vertices, 1=Arc
    public float Size; //world units
    public float SegScale; //arc angle / number of segments
    public float SegOffset; //angle offset of arc
    public Vector3 CamRel; //center position
    public uint CullBack; //culls pixels behind 0,0,0
    public Color4 Colour; //colour for arc
    public Vector3 Axis1; //axis 1 of arc
    public float WidgetPad2;
    public Vector3 Axis2; //axis 2 of arc
    public float WidgetPad3;
}

public class WidgetShader : Shader
{
    private readonly PixelShader ps;

    private readonly GpuVarsBuffer<WidgetShaderSceneVars> SceneVars;
    private readonly GpuCBuffer<WidgetShaderVertex> Vertices;
    private readonly VertexShader vs;


    public WidgetShader(Device device)
    {
        var vsbytes = PathUtil.ReadAllBytes("Shaders\\WidgetVS.cso");
        var psbytes = PathUtil.ReadAllBytes("Shaders\\WidgetPS.cso");

        vs = new VertexShader(device, vsbytes);
        ps = new PixelShader(device, psbytes);

        SceneVars = new GpuVarsBuffer<WidgetShaderSceneVars>(device);

        Vertices = new GpuCBuffer<WidgetShaderVertex>(device, 150); //should be more than needed....
    }

    public void Dispose()
    {
        Vertices.Dispose();
        SceneVars.Dispose();
        ps.Dispose();
        vs.Dispose();
    }

    public override void SetShader(DeviceContext context)
    {
        context.VertexShader.Set(vs);
        context.PixelShader.Set(ps);
    }

    public override bool SetInputLayout(DeviceContext context, VertexType type)
    {
        context.InputAssembler.InputLayout = null;
        context.InputAssembler.SetIndexBuffer(null, Format.Unknown, 0);
        return true;
    }

    public override void SetSceneVars(DeviceContext context, Camera camera, Shadowmap shadowmap,
        ShaderGlobalLights lights)
    {
        SceneVars.Vars.ViewProj = Matrix.Transpose(camera.ViewProjMatrix);
        SceneVars.Update(context);
        SceneVars.SetVSCBuffer(context, 0);
    }

    public override void SetEntityVars(DeviceContext context, ref RenderableInst rend)
    {
    }

    public override void SetModelVars(DeviceContext context, RenderableModel model)
    {
    }

    public override void SetGeomVars(DeviceContext context, RenderableGeometry geom)
    {
    }

    public override void UnbindResources(DeviceContext context)
    {
        context.VertexShader.SetConstantBuffer(0, null);
        context.VertexShader.SetShaderResource(0, null);
        context.VertexShader.Set(null);
        context.PixelShader.Set(null);
    }


    public void DrawDefaultWidget(DeviceContext context, Camera cam, Vector3 camrel, Quaternion ori, float size)
    {
        SetShader(context);
        SetInputLayout(context, VertexType.Default);

        SceneVars.Vars.Mode = 0; //vertices mode
        SceneVars.Vars.CamRel = camrel;
        SetSceneVars(context, cam, null, null);

        var xdir = ori.Multiply(Vector3.UnitX);
        var ydir = ori.Multiply(Vector3.UnitY);
        var zdir = ori.Multiply(Vector3.UnitZ);
        var xcolour = new Color4(0.8f, 0.0f, 0.0f, 1.0f);
        var ycolour = new Color4(0.8f, 0.0f, 0.0f, 1.0f);
        var zcolour = new Color4(0.5f, 0.5f, 0.5f, 1.0f);

        Vector3[] axes = { xdir, ydir, zdir };
        Vector3[] sides = { ydir, xdir, xdir };
        Color4[] colours = { xcolour, ycolour, zcolour };

        var linestart = 0.0f * size;
        var lineend = 1.0f * size;
        var arrowstart = 0.9f * size;
        var arrowend = 1.0f * size;
        var arrowrad = 0.05f * size;

        //draw lines...
        Vertices.Clear();

        for (var i = 0; i < 3; i++)
        {
            var sa = (WidgetAxis)(1 << i);
            var axcol = colours[i];

            //main axis lines
            Vertices.Add(new WidgetShaderVertex(axes[i] * linestart, axcol));
            Vertices.Add(new WidgetShaderVertex(axes[i] * lineend, axcol));

            //arrow heads
            var astart = axes[i] * arrowstart;
            var aend = axes[i] * arrowend;
            var aside = sides[i] * arrowrad;
            Vertices.Add(new WidgetShaderVertex(aend, axcol));
            Vertices.Add(new WidgetShaderVertex(astart + aside, axcol));
            Vertices.Add(new WidgetShaderVertex(aend, axcol));
            Vertices.Add(new WidgetShaderVertex(astart - aside, axcol));
        }

        Vertices.Update(context);
        Vertices.SetVSResource(context, 0);

        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.LineList;
        context.Draw(Vertices.CurrentCount, 0);

        UnbindResources(context);
    }

    public void DrawPositionWidget(DeviceContext context, Camera cam, Vector3 camrel, Quaternion ori, float size,
        WidgetAxis selax)
    {
        SetShader(context);
        SetInputLayout(context, VertexType.Default);

        SceneVars.Vars.Mode = 0; //vertices mode
        SceneVars.Vars.CamRel = camrel;
        SetSceneVars(context, cam, null, null);

        var xdir = ori.Multiply(Vector3.UnitX);
        var ydir = ori.Multiply(Vector3.UnitY);
        var zdir = ori.Multiply(Vector3.UnitZ);
        var xcolour = new Color4(1.0f, 0.0f, 0.0f, 1.0f);
        var ycolour = new Color4(0.0f, 1.0f, 0.0f, 1.0f);
        var zcolour = new Color4(0.0f, 0.0f, 1.0f, 1.0f);
        var selaxcol = new Color4(1.0f, 1.0f, 0.0f, 1.0f);
        var selplcol = new Color4(1.0f, 1.0f, 0.0f, 0.5f);

        Vector3[] axes = { xdir, ydir, zdir };
        Vector3[] sides1 = { ydir, zdir, xdir };
        Vector3[] sides2 = { zdir, xdir, ydir };
        WidgetAxis[] sideax1 = { WidgetAxis.Y, WidgetAxis.Z, WidgetAxis.X };
        WidgetAxis[] sideax2 = { WidgetAxis.Z, WidgetAxis.X, WidgetAxis.Y };
        Color4[] colours = { xcolour, ycolour, zcolour };
        Color4[] coloursdark = { xcolour * 0.5f, ycolour * 0.5f, zcolour * 0.5f };
        for (var i = 0; i < 3; i++) coloursdark[i].Alpha = 1.0f;

        var linestart = 0.2f * size;
        var lineend = 1.0f * size;
        var sideval = 0.4f * size;
        var arrowstart = 1.0f * size;
        var arrowend = 1.33f * size;
        var arrowrad = 0.06f * size;

        var hexx = 0.5f;
        var hexy = 0.86602540378443864676372317075294f; //sqrt(0.75)
        Vector2[] arrowv =
        {
            new Vector2(-1, 0) * arrowrad,
            new Vector2(-hexx, hexy) * arrowrad,
            new Vector2(hexx, hexy) * arrowrad,
            new Vector2(1, 0) * arrowrad,
            new Vector2(hexx, -hexy) * arrowrad,
            new Vector2(-hexx, -hexy) * arrowrad,
            new Vector2(-1, 0) * arrowrad
        };


        //draw lines...
        Vertices.Clear();

        for (var i = 0; i < 3; i++)
        {
            var sa = (WidgetAxis)(1 << i);
            var axsel = (selax & sa) > 0;
            var axcol = axsel ? selaxcol : colours[i];

            //axis side square lines
            var ax = axes[i] * sideval;
            var s1 = sides1[i] * sideval;
            var s2 = sides2[i] * sideval;
            var sc1 = axsel && (selax & sideax1[i]) > 0 ? selaxcol : colours[i];
            var sc2 = axsel && (selax & sideax2[i]) > 0 ? selaxcol : colours[i];
            Vertices.Add(new WidgetShaderVertex(ax, sc1));
            Vertices.Add(new WidgetShaderVertex(ax + s1, sc1));
            Vertices.Add(new WidgetShaderVertex(ax, sc2));
            Vertices.Add(new WidgetShaderVertex(ax + s2, sc2));

            //main axis lines - draw after side lines to be on top
            Vertices.Add(new WidgetShaderVertex(axes[i] * linestart, axcol));
            Vertices.Add(new WidgetShaderVertex(axes[i] * lineend, axcol));
        }

        Vertices.Update(context);
        Vertices.SetVSResource(context, 0);

        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.LineList;
        context.Draw(Vertices.CurrentCount, 0);


        //draw triangles...
        Vertices.Clear();

        for (var i = 0; i < 3; i++)
        {
            //axis arrows - kind of inefficient, but meh
            var aend = axes[i] * arrowend;
            var astart = axes[i] * arrowstart;
            for (var n = 0; n < 6; n++)
            {
                var a1 = arrowv[n];
                var a2 = arrowv[n + 1];
                var ap1 = astart + sides1[i] * a1.Y + sides2[i] * a1.X;
                var ap2 = astart + sides1[i] * a2.Y + sides2[i] * a2.X;
                Vertices.Add(new WidgetShaderVertex(aend, colours[i]));
                Vertices.Add(new WidgetShaderVertex(ap1, colours[i]));
                Vertices.Add(new WidgetShaderVertex(ap2, colours[i]));
                Vertices.Add(new WidgetShaderVertex(astart, coloursdark[i]));
                Vertices.Add(new WidgetShaderVertex(ap2, coloursdark[i]));
                Vertices.Add(new WidgetShaderVertex(ap1, coloursdark[i]));
            }

            //selection planes
            var sa = (WidgetAxis)(1 << i);
            if ((selax & sa) > 0)
            {
                var ax = axes[i] * sideval;
                for (var n = i + 1; n < 3; n++)
                {
                    var tsa = (WidgetAxis)(1 << n);
                    if ((selax & tsa) > 0)
                    {
                        var tax = axes[n] * sideval;
                        Vertices.Add(new WidgetShaderVertex(Vector3.Zero, selplcol));
                        Vertices.Add(new WidgetShaderVertex(ax, selplcol));
                        Vertices.Add(new WidgetShaderVertex(tax, selplcol));
                        Vertices.Add(new WidgetShaderVertex(tax + ax, selplcol));
                        Vertices.Add(new WidgetShaderVertex(tax, selplcol));
                        Vertices.Add(new WidgetShaderVertex(ax, selplcol));
                    }
                }
            }
        }

        Vertices.Update(context);
        Vertices.SetVSResource(context, 0);

        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        context.Draw(Vertices.CurrentCount, 0);

        UnbindResources(context);
    }

    public void DrawRotationWidget(DeviceContext context, Camera cam, Vector3 camrel, Quaternion ori, float size,
        WidgetAxis selax, WidgetAxis drawax)
    {
        SetShader(context);
        SetInputLayout(context, VertexType.Default);


        SceneVars.Vars.Mode = 0; //vertices mode
        SceneVars.Vars.CamRel = camrel;
        SetSceneVars(context, cam, null, null);

        var xdir = ori.Multiply(Vector3.UnitX);
        var ydir = ori.Multiply(Vector3.UnitY);
        var zdir = ori.Multiply(Vector3.UnitZ);
        var xcolour = new Color4(1.0f, 0.0f, 0.0f, 1.0f);
        var ycolour = new Color4(0.0f, 1.0f, 0.0f, 1.0f);
        var zcolour = new Color4(0.0f, 0.0f, 1.0f, 1.0f);
        var icolour = new Color4(0.5f, 0.5f, 0.5f, 1.0f);
        var ocolour = new Color4(0.7f, 0.7f, 0.7f, 1.0f);
        var scolour = new Color4(1.0f, 1.0f, 0.0f, 1.0f);

        Vector3[] axes = { xdir, ydir, zdir };
        Vector3[] sides = { ydir, xdir, xdir };
        Color4[] colours = { xcolour, ycolour, zcolour };

        var linestart = 0.0f * size;
        var lineend = 0.3f * size;
        var ocircsize = 1.0f * size;
        var icircsize = 0.75f * size;

        //draw lines...
        Vertices.Clear();

        for (var i = 0; i < 3; i++)
        {
            var sa = (WidgetAxis)(1 << i);
            var axsel = (selax & sa) > 0;
            var axcol = axsel ? colours[i] : icolour;

            //main axis lines
            Vertices.Add(new WidgetShaderVertex(axes[i] * linestart, axcol));
            Vertices.Add(new WidgetShaderVertex(axes[i] * lineend, axcol));
        }

        Vertices.Update(context);
        Vertices.SetVSResource(context, 0);

        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.LineList;
        context.Draw(Vertices.CurrentCount, 0);


        //linestrip for arcs and circles
        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.LineStrip;

        var sdir = Vector3.Normalize(camrel);
        //if (cam.IsMapView || cam.IsOrthographic)
        //{
        //    sdir = cam.ViewDirection;
        //}
        var ad1 = Math.Abs(Vector3.Dot(sdir, Vector3.UnitY));
        var ad2 = Math.Abs(Vector3.Dot(sdir, Vector3.UnitZ));
        var ax1 = Vector3.Normalize(Vector3.Cross(sdir, ad1 > ad2 ? Vector3.UnitY : Vector3.UnitZ));
        var ax2 = Vector3.Normalize(Vector3.Cross(sdir, ax1));

        //drawing circles
        var segcount = 40;
        var vertcount = segcount + 1;
        SceneVars.Vars.Mode = 1; //arc mode
        SceneVars.Vars.SegScale = (float)Math.PI * 2.0f / segcount; //arc angle / number of segments
        SceneVars.Vars.SegOffset = 0.0f; //angle offset of arc
        SceneVars.Vars.Axis1 = ax1; //axis 1 of arc
        SceneVars.Vars.Axis2 = ax2; //axis 2 of arc
        SceneVars.Vars.CullBack = 0; //culls pixels behind 0,0,0

        //outer circle
        if (drawax == WidgetAxis.XYZ)
        {
            SceneVars.Vars.Size = ocircsize; //world units
            SceneVars.Vars.Colour = selax == WidgetAxis.XYZ ? scolour : ocolour; //colour for arc
            SetSceneVars(context, cam, null, null);
            context.Draw(vertcount, 0);
        }

        //inner circle
        SceneVars.Vars.Size = icircsize; //world units
        SceneVars.Vars.Colour = icolour; //colour for arc
        SetSceneVars(context, cam, null, null);
        context.Draw(vertcount, 0);


        //drawing arcs - culling done in PS
        SceneVars.Vars.Size = icircsize; //world units
        SceneVars.Vars.CullBack = 1; //culls pixels behind 0,0,0

        if ((drawax & WidgetAxis.X) != 0)
        {
            SceneVars.Vars.SegOffset = 0.0f; //angle offset of arc
            SceneVars.Vars.Axis1 = ydir; //axis 1 of arc
            SceneVars.Vars.Axis2 = zdir; //axis 2 of arc
            SceneVars.Vars.Colour = selax == WidgetAxis.X ? scolour : xcolour; //colour for arc
            SetSceneVars(context, cam, null, null);
            context.Draw(vertcount, 0);
        }

        if ((drawax & WidgetAxis.Y) != 0)
        {
            SceneVars.Vars.SegOffset = 0.0f; //angle offset of arc
            SceneVars.Vars.Axis1 = xdir; //axis 1 of arc
            SceneVars.Vars.Axis2 = zdir; //axis 2 of arc
            SceneVars.Vars.Colour = selax == WidgetAxis.Y ? scolour : ycolour; //colour for arc
            SetSceneVars(context, cam, null, null);
            context.Draw(vertcount, 0);
        }

        if ((drawax & WidgetAxis.Z) != 0)
        {
            SceneVars.Vars.SegOffset = 0.0f; //angle offset of arc
            SceneVars.Vars.Axis1 = xdir; //axis 1 of arc
            SceneVars.Vars.Axis2 = ydir; //axis 2 of arc
            SceneVars.Vars.Colour = selax == WidgetAxis.Z ? scolour : zcolour; //colour for arc
            SetSceneVars(context, cam, null, null);
            context.Draw(vertcount, 0);
        }


        UnbindResources(context);
    }

    public void DrawScaleWidget(DeviceContext context, Camera cam, Vector3 camrel, Quaternion ori, float size,
        WidgetAxis selax)
    {
        SetShader(context);
        SetInputLayout(context, VertexType.Default);

        SceneVars.Vars.Mode = 0; //vertices mode
        SceneVars.Vars.CamRel = camrel;
        SetSceneVars(context, cam, null, null);

        var xdir = ori.Multiply(Vector3.UnitX);
        var ydir = ori.Multiply(Vector3.UnitY);
        var zdir = ori.Multiply(Vector3.UnitZ);
        var xcolour = new Color4(1.0f, 0.0f, 0.0f, 1.0f);
        var ycolour = new Color4(0.0f, 1.0f, 0.0f, 1.0f);
        var zcolour = new Color4(0.0f, 0.0f, 1.0f, 1.0f);
        var selaxcol = new Color4(1.0f, 1.0f, 0.0f, 1.0f);
        var selplcol = new Color4(1.0f, 1.0f, 0.0f, 0.5f);

        Vector3[] axes = { xdir, ydir, zdir };
        Vector3[] sides1 = { ydir, zdir, xdir };
        Vector3[] sides2 = { zdir, xdir, ydir };
        WidgetAxis[] sideax1 = { WidgetAxis.Y, WidgetAxis.Z, WidgetAxis.X };
        WidgetAxis[] sideax2 = { WidgetAxis.Z, WidgetAxis.X, WidgetAxis.Y };
        Color4[] colours = { xcolour, ycolour, zcolour };
        Color4[] coloursn = { ycolour, zcolour, xcolour };
        Color4[] coloursdark = { xcolour * 0.5f, ycolour * 0.5f, zcolour * 0.5f };
        for (var i = 0; i < 3; i++) coloursdark[i].Alpha = 1.0f;

        var linestart = 0.0f * size;
        var lineend = 1.33f * size;
        var innertri = 0.7f * size;
        var outertri = 1.0f * size;
        var cubestart = 1.28f * size;
        var cubeend = 1.33f * size;
        var cubesize = 0.025f * size;

        //draw lines...
        Vertices.Clear();

        for (var i = 0; i < 3; i++)
        {
            var sa = (WidgetAxis)(1 << i);
            var axsel = (selax & sa) > 0;
            var axcol = axsel ? selaxcol : colours[i];

            var triaxn = sideax1[i];
            var trisel = axsel && (selax & triaxn) > 0;
            var tricol = trisel ? selaxcol : colours[i];
            var trincol = trisel ? selaxcol : coloursn[i];

            var inner1 = axes[i] * innertri;
            var inner2 = sides1[i] * innertri;
            var innera = (inner1 + inner2) * 0.5f;
            var outer1 = axes[i] * outertri;
            var outer2 = sides1[i] * outertri;
            var outera = (outer1 + outer2) * 0.5f;

            //triangle axis lines
            Vertices.Add(new WidgetShaderVertex(inner1, tricol));
            Vertices.Add(new WidgetShaderVertex(innera, tricol));
            Vertices.Add(new WidgetShaderVertex(innera, trincol));
            Vertices.Add(new WidgetShaderVertex(inner2, trincol));
            Vertices.Add(new WidgetShaderVertex(outer1, tricol));
            Vertices.Add(new WidgetShaderVertex(outera, tricol));
            Vertices.Add(new WidgetShaderVertex(outera, trincol));
            Vertices.Add(new WidgetShaderVertex(outer2, trincol));

            //main axis lines - draw after side lines to be on top
            Vertices.Add(new WidgetShaderVertex(axes[i] * linestart, axcol));
            Vertices.Add(new WidgetShaderVertex(axes[i] * lineend, axcol));
        }

        Vertices.Update(context);
        Vertices.SetVSResource(context, 0);


        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.LineList;
        context.Draw(Vertices.CurrentCount, 0);


        //draw triangles...
        Vertices.Clear();

        for (var i = 0; i < 3; i++)
        {
            //axis end cubes - kind of inefficient, but meh
            var cend = axes[i] * cubeend;
            var cstart = axes[i] * cubestart;
            var cside1 = sides1[i] * cubesize;
            var cside2 = sides2[i] * cubesize;
            var cv1 = cstart + cside1 - cside2;
            var cv2 = cstart - cside1 - cside2;
            var cv3 = cend + cside1 - cside2;
            var cv4 = cend - cside1 - cside2;
            var cv5 = cstart + cside1 + cside2;
            var cv6 = cstart - cside1 + cside2;
            var cv7 = cend + cside1 + cside2;
            var cv8 = cend - cside1 + cside2;
            var col = colours[i];
            var cold = coloursdark[i];
            Vertices.Add(new WidgetShaderVertex(cv1, cold));
            Vertices.Add(new WidgetShaderVertex(cv2, cold));
            Vertices.Add(new WidgetShaderVertex(cv5, cold));
            Vertices.Add(new WidgetShaderVertex(cv5, cold));
            Vertices.Add(new WidgetShaderVertex(cv2, cold));
            Vertices.Add(new WidgetShaderVertex(cv6, cold));
            Vertices.Add(new WidgetShaderVertex(cv3, col));
            Vertices.Add(new WidgetShaderVertex(cv4, col));
            Vertices.Add(new WidgetShaderVertex(cv7, col));
            Vertices.Add(new WidgetShaderVertex(cv7, col));
            Vertices.Add(new WidgetShaderVertex(cv4, col));
            Vertices.Add(new WidgetShaderVertex(cv8, col));
            Vertices.Add(new WidgetShaderVertex(cv1, col));
            Vertices.Add(new WidgetShaderVertex(cv2, col));
            Vertices.Add(new WidgetShaderVertex(cv3, col));
            Vertices.Add(new WidgetShaderVertex(cv3, col));
            Vertices.Add(new WidgetShaderVertex(cv2, col));
            Vertices.Add(new WidgetShaderVertex(cv4, col));
            Vertices.Add(new WidgetShaderVertex(cv5, col));
            Vertices.Add(new WidgetShaderVertex(cv6, col));
            Vertices.Add(new WidgetShaderVertex(cv7, col));
            Vertices.Add(new WidgetShaderVertex(cv7, col));
            Vertices.Add(new WidgetShaderVertex(cv6, col));
            Vertices.Add(new WidgetShaderVertex(cv8, col));
            Vertices.Add(new WidgetShaderVertex(cv1, col));
            Vertices.Add(new WidgetShaderVertex(cv5, col));
            Vertices.Add(new WidgetShaderVertex(cv3, col));
            Vertices.Add(new WidgetShaderVertex(cv3, col));
            Vertices.Add(new WidgetShaderVertex(cv5, col));
            Vertices.Add(new WidgetShaderVertex(cv7, col));
            Vertices.Add(new WidgetShaderVertex(cv2, col));
            Vertices.Add(new WidgetShaderVertex(cv6, col));
            Vertices.Add(new WidgetShaderVertex(cv4, col));
            Vertices.Add(new WidgetShaderVertex(cv4, col));
            Vertices.Add(new WidgetShaderVertex(cv6, col));
            Vertices.Add(new WidgetShaderVertex(cv8, col));


            //selection triangles
            if (selax == WidgetAxis.XYZ)
            {
                //all axes - just draw inner triangle
                Vertices.Add(new WidgetShaderVertex(Vector3.Zero, selplcol));
                Vertices.Add(new WidgetShaderVertex(axes[i] * innertri, selplcol));
                Vertices.Add(new WidgetShaderVertex(sides1[i] * innertri, selplcol));
            }
            else
            {
                var sa = (WidgetAxis)(1 << i);
                var na = sideax1[i];
                if ((selax & sa) > 0 && (selax & na) > 0)
                {
                    Vertices.Add(new WidgetShaderVertex(axes[i] * innertri, selplcol));
                    Vertices.Add(new WidgetShaderVertex(sides1[i] * innertri, selplcol));
                    Vertices.Add(new WidgetShaderVertex(axes[i] * outertri, selplcol));
                    Vertices.Add(new WidgetShaderVertex(axes[i] * outertri, selplcol));
                    Vertices.Add(new WidgetShaderVertex(sides1[i] * innertri, selplcol));
                    Vertices.Add(new WidgetShaderVertex(sides1[i] * outertri, selplcol));
                }
            }
        }

        Vertices.Update(context);
        Vertices.SetVSResource(context, 0);

        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        context.Draw(Vertices.CurrentCount, 0);


        UnbindResources(context);
    }
}

public struct WidgetShaderVertex
{
    public Vector4 Position;
    public Color4 Colour;

    public WidgetShaderVertex(Vector3 p, Color4 c)
    {
        Position = new Vector4(p, 0.0f);
        Colour = c;
    }
}