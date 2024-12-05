using CodeWalker.GameFiles;
using CodeWalker.World;
using SharpDX.Direct3D11;

namespace CodeWalker.Rendering;

public abstract class Shader
{
    public abstract void SetShader(DeviceContext context);
    public abstract bool SetInputLayout(DeviceContext context, VertexType type);

    public abstract void SetSceneVars(DeviceContext context, Camera camera, Shadowmap shadowmap,
        ShaderGlobalLights lights);

    public abstract void SetEntityVars(DeviceContext context, ref RenderableInst rend);
    public abstract void SetModelVars(DeviceContext context, RenderableModel model);
    public abstract void SetGeomVars(DeviceContext context, RenderableGeometry geom);
    public abstract void UnbindResources(DeviceContext context);

    //public abstract void Dispose();
}