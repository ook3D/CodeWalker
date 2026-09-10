using System.Diagnostics.CodeAnalysis;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Runtime.InteropServices;
using Device = SharpDX.Direct3D11.Device;
using CodeWalker.GameFiles;
using CodeWalker.World;

namespace CodeWalker.Rendering
{

    [StructLayout(LayoutKind.Sequential)]
    public struct OutlineMaskVSVars
    {
        public Matrix WorldViewProj;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct OutlineBlurPSVars
    {
        public Vector4 OutlineColour;
        public int StepDirectionX;
        public int StepDirectionY;
        public int Stage;
        public int Width;
    }

    public class OutlineShader : IDisposable
    {
        bool disposed = false;

        VertexShader maskVS;
        PixelShader maskPS;
        VertexShader blurVS;
        PixelShader blurPS;

        InputLayout maskInputLayout;
        InputLayout blurInputLayout;

        GpuVarsBuffer<OutlineMaskVSVars> VSVars;
        GpuVarsBuffer<OutlineBlurPSVars> PSBlurVars;

        GpuTexture? MaskTex;
        GpuTexture? BlurTex;
        UnitQuad FullscreenQuad;

        int BufferWidth;
        int BufferHeight;

        Device device;

        public OutlineShader(Device device)
        {
            this.device = device;

            byte[] maskvsbytes = PathUtil.ReadAllBytes("Shaders\\OutlineMaskVS.cso");
            byte[] maskpsbytes = PathUtil.ReadAllBytes("Shaders\\OutlineMaskPS.cso");
            byte[] blurvsbytes = PathUtil.ReadAllBytes("Shaders\\OutlineBlurVS.cso");
            byte[] blurpsbytes = PathUtil.ReadAllBytes("Shaders\\OutlineBlurPS.cso");

            maskVS = new VertexShader(device, maskvsbytes);
            maskPS = new PixelShader(device, maskpsbytes);
            blurVS = new VertexShader(device, blurvsbytes);
            blurPS = new PixelShader(device, blurpsbytes);

            // Mask input layout matches the entity vertex format - just needs POSITION
            maskInputLayout = new InputLayout(device, maskvsbytes, new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            });

            // Blur input layout for fullscreen quad - POSITION as float4
            blurInputLayout = new InputLayout(device, blurvsbytes, new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
            });

            VSVars = new GpuVarsBuffer<OutlineMaskVSVars>(device);
            PSBlurVars = new GpuVarsBuffer<OutlineBlurPSVars>(device);

            FullscreenQuad = new UnitQuad(device);
        }


        [MemberNotNull(nameof(MaskTex), nameof(BlurTex))]
        private void EnsureBuffers(int w, int h)
        {
            if (BufferWidth == w && BufferHeight == h && MaskTex != null && BlurTex != null)
                return;

            DisposeBuffers();

            MaskTex = new GpuTexture(device, w, h, Format.R32_Float);
            BlurTex = new GpuTexture(device, w, h, Format.R32_Float);
            BufferWidth = w;
            BufferHeight = h;
        }

        private void DisposeBuffers()
        {
            MaskTex?.Dispose();
            MaskTex = null;
            BlurTex?.Dispose();
            BlurTex = null;
            BufferWidth = 0;
            BufferHeight = 0;
        }

        public void OnWindowResize(int w, int h)
        {
            DisposeBuffers();
            // Buffers will be recreated on next render
        }

        public void RenderOutline(
            DeviceContext context,
            Camera camera,
            ShaderManager shaders,
            Renderable? renderable,
            Vector3 camrel,
            Quaternion orientation,
            Vector3 scale,
            Vector4 outlineColour,
            int outlineWidth = 3)
        {
            if (renderable == null) return;
            var models = renderable.HDModels ?? renderable.AllModels;
            if (models == null || models.Length == 0) return;

            // Get viewport dimensions from current rasterizer state
            var viewports = context.Rasterizer.GetViewports<SharpDX.Mathematics.Interop.RawViewportF>();
            if (viewports == null || viewports.Length == 0) return;
            int w = (int)viewports[0].Width;
            int h = (int)viewports[0].Height;
            if (w <= 0 || h <= 0) return;

            EnsureBuffers(w, h);

            // Save current render target state
            var origRTVs = context.OutputMerger.GetRenderTargets(1, out var origDSV);
            var origViewports = context.Rasterizer.GetViewports<SharpDX.Mathematics.Interop.RawViewportF>();

            var viewport = new Viewport(0, 0, w, h, 0, 1);

            MaskTex.Clear(context, new Color4(0, 0, 0, 0));
            MaskTex.SetRenderTarget(context);
            context.Rasterizer.SetViewport(viewport);

            // Set depth stencil to read-only so the outline respects depth
            shaders.SetDepthStencilMode(context, DepthStencilMode.DisableWrite);
            shaders.SetRasterizerMode(context, RasterizerMode.SolidDblSided);

            context.VertexShader.Set(maskVS);
            context.PixelShader.Set(maskPS);
            context.InputAssembler.InputLayout = maskInputLayout;

            // Compose the entity world matrix: Scale * Orientation * Translation(CamRel)
            Matrix entityWorld = Matrix.Scaling(scale)
                * Matrix.RotationQuaternion(orientation)
                * Matrix.Translation(camrel);

            // Draw each model/geometry in the renderable
            for (int mi = 0; mi < models.Length; mi++)
            {
                var model = models[mi];
                if (model?.Geometries == null) continue;

                // Compose WorldViewProj per-model, including fragment/bone transform
                Matrix world;
                if (model.UseTransform)
                {
                    world = model.Transform * entityWorld;
                }
                else
                {
                    world = entityWorld;
                }

                VSVars.Vars.WorldViewProj = Matrix.Transpose(world * camera.ViewProjMatrix);
                VSVars.Update(context);
                VSVars.SetVSCBuffer(context, 0);

                for (int gi = 0; gi < model.Geometries.Length; gi++)
                {
                    var geom = model.Geometries[gi];
                    if (geom?.VertexBuffer == null || geom?.IndexBuffer == null) continue;
                    if (geom.VertexCount == 0 || geom.IndexCount == 0) continue;

                    context.InputAssembler.PrimitiveTopology = geom.Topology;
                    context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(geom.VertexBuffer, geom.VertexStride, 0));
                    context.InputAssembler.SetIndexBuffer(geom.IndexBuffer, Format.R16_UInt, 0);
                    context.DrawIndexed(geom.IndexCount, 0, 0);
                }
            }

            BlurTex.Clear(context, new Color4(0, 0, 0, 0));
            BlurTex.SetRenderTarget(context);
            context.Rasterizer.SetViewport(viewport);

            shaders.SetDepthStencilMode(context, DepthStencilMode.DisableAll);
            // Use default blend (no blending for intermediate pass)
            context.OutputMerger.BlendState = null;

            context.VertexShader.Set(blurVS);
            context.PixelShader.Set(blurPS);
            context.InputAssembler.InputLayout = blurInputLayout;

            PSBlurVars.Vars.OutlineColour = outlineColour;
            PSBlurVars.Vars.Width = outlineWidth;
            PSBlurVars.Vars.StepDirectionX = 1;
            PSBlurVars.Vars.StepDirectionY = 0;
            PSBlurVars.Vars.Stage = 0;
            PSBlurVars.Update(context);
            PSBlurVars.SetPSCBuffer(context, 0);

            context.PixelShader.SetShaderResource(0, MaskTex.SRV);
            FullscreenQuad.Draw(context);

            // Restore original render target (back buffer)
            context.OutputMerger.SetRenderTargets(origDSV, origRTVs);
            if (origViewports != null && origViewports.Length > 0)
            {
                context.Rasterizer.SetViewports(origViewports);
            }

            shaders.SetDepthStencilMode(context, DepthStencilMode.DisableAll);
            shaders.SetDefaultBlendState(context); // Alpha blend onto the scene

            context.VertexShader.Set(blurVS);
            context.PixelShader.Set(blurPS);
            context.InputAssembler.InputLayout = blurInputLayout;

            PSBlurVars.Vars.StepDirectionX = 0;
            PSBlurVars.Vars.StepDirectionY = 1;
            PSBlurVars.Vars.Stage = 1;
            PSBlurVars.Update(context);
            PSBlurVars.SetPSCBuffer(context, 0);

            context.PixelShader.SetShaderResource(0, MaskTex.SRV);
            context.PixelShader.SetShaderResource(1, BlurTex.SRV);
            FullscreenQuad.Draw(context);

            // ====== Cleanup ======
            context.PixelShader.SetShaderResource(0, null);
            context.PixelShader.SetShaderResource(1, null);
            context.VertexShader.Set(null);
            context.PixelShader.Set(null);
            context.InputAssembler.InputLayout = null;

            // Dispose the saved RT references to avoid leaks
            origDSV?.Dispose();
            if (origRTVs != null)
            {
                foreach (var rtv in origRTVs) rtv?.Dispose();
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            DisposeBuffers();

            FullscreenQuad?.Dispose();

            PSBlurVars?.Dispose();
            VSVars?.Dispose();

            blurInputLayout?.Dispose();
            maskInputLayout?.Dispose();

            blurPS?.Dispose();
            blurVS?.Dispose();
            maskPS?.Dispose();
            maskVS?.Dispose();
        }
    }
}
