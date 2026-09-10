using CodeWalker.GameFiles;
using CodeWalker.Properties;
using CodeWalker.Rendering;
using CodeWalker.World;
using SharpDX;
using SharpDX.Direct3D11;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodeWalker.Forms
{
    // Render-only WinForms host for the particle preview. Designed to be embedded in the WPF particle editor
    // via WindowsFormsHost (TopLevel=false). Owns the Renderer + GameFileCache and runs the live preview of a
    // selected ParticleEffectRule; the editor UI lives in WPF and just calls SetPreviewEffect / the transport.
    public class ParticleViewportHost : Form, DXForm
    {
        public Form Form { get { return this; } }
        public Lock RenderSyncRoot { get { return Renderer.RenderSyncRoot; } }

        public readonly Renderer Renderer;
        public GameFileCache GameFileCache { get; } = GameFileCacheFactory.Create();

        public event EventHandler? CacheReady;
        public bool IsCacheReady { get; private set; }

        //raised when the user presses space while the viewport has focus (toggles play/pause in the editor)
        public event EventHandler? PlayPauseRequested;

        volatile bool formopen = false;
        volatile bool running = false;

        Stopwatch frametimer = new();
        Camera camera;
        Timecycle timecycle;
        Weather weather;
        Clouds clouds;
        Entity camEntity = new();
        InputManager Input = new();
        bool initedOk = false;

        bool MouseLButtonDown = false;
        int MouseX, MouseY;
        System.Drawing.Point MouseLastPoint;

        //preview state
        YptFile? ypt;
        ParticleEffectInst? particleEffect;
        volatile ParticleEffectRule? selectedEffectRule;
        volatile bool particleEffectDirty = false;
        volatile bool particleRestart = false;
        volatile bool reframeCamera = false;
        volatile bool seekRequested = false;
        volatile float seekRatio = 0f;
        readonly System.Collections.Generic.HashSet<ParticleEventEmitter> hiddenEmitters = new();
        public bool Playing { get; set; } = true;
        public float TimeScale { get; set; } = 1.0f;

        //transport readouts (updated on the render thread)
        public float CurrentTime { get; private set; }
        public float Duration { get; private set; }
        public int ParticleCount { get; private set; }
        public string lastRenderError = string.Empty;
        public string RenderStats => Renderer.ParticleRenderStats;


        public ParticleViewportHost()
        {
            //minimal form setup - this is hosted, not shown standalone
            FormBorderStyle = FormBorderStyle.None;
            Text = "ParticleViewport";
            BackColor = System.Drawing.Color.Black;
            ClientSize = new System.Drawing.Size(640, 480);

            Renderer = new Renderer(this, GameFileCache);
            camera = Renderer.camera;
            timecycle = Renderer.timecycle;
            weather = Renderer.weather;
            clouds = Renderer.clouds;

            initedOk = Renderer.Init();

            Renderer.controllightdir = !Settings.Default.Skydome;
            Renderer.rendercollisionmeshes = false;
            Renderer.renderclouds = false;
            Renderer.rendermoon = false;
            Renderer.renderskeletons = false;
            Renderer.renderfragwindows = false;
            Renderer.SelectionFlagsTestAll = true;

            Load += ParticleViewportHost_Load;
            MouseDown += ParticleViewportHost_MouseDown;
            MouseUp += ParticleViewportHost_MouseUp;
            MouseMove += ParticleViewportHost_MouseMove;
            MouseWheel += ParticleViewportHost_MouseWheel;
            KeyDown += ParticleViewportHost_KeyDown;
        }


        #region public API (called from the WPF editor)

        public void SetPreviewEffect(ParticleEffectRule rule, YptFile owner)
        {
            ypt = owner;
            selectedEffectRule = rule;
            particleRestart = true;
            particleEffectDirty = true;
            reframeCamera = true;
        }

        public void MarkDirty() { particleEffectDirty = true; }

        public void Restart() { particleRestart = true; Playing = true; }

        //scrub the playback to a normalized time (0..1). Pauses so the caret holds where the user dragged it.
        public void SeekToRatio(float r)
        {
            seekRatio = Math.Clamp(r, 0f, 1f);
            seekRequested = true;
            Playing = false;
        }

        //show/hide a single emitter (by its ParticleEventEmitter) in the preview. Keeps simulating; just not drawn.
        public void SetEmitterVisible(ParticleEventEmitter? ee, bool visible)
        {
            if (ee == null) return;
            lock (Renderer.RenderSyncRoot)
            {
                if (visible) hiddenEmitters.Remove(ee); else hiddenEmitters.Add(ee);
                if (particleEffect != null)
                    foreach (var em in particleEffect.Emitters)
                        if (em.Event == ee) em.Visible = visible;
            }
        }

        #endregion


        #region DXForm

        public void InitScene(Device device)
        {
            try { Renderer.DeviceCreated(device, ClientSize.Width, ClientSize.Height); }
            catch (Exception ex) { MessageBox.Show("Error loading shaders!\n" + ex.ToString()); return; }

            camera.FollowEntity = camEntity;
            camera.FollowEntity.Position = Vector3.Zero;
            camera.FollowEntity.Orientation = Quaternion.LookAtLH(Vector3.Zero, Vector3.Up, Vector3.ForwardLH);
            camera.TargetDistance = 2.0f;
            camera.CurrentDistance = 2.0f;
            camera.TargetRotation.Y = 0.2f;
            camera.CurrentRotation.Y = 0.2f;
            camera.TargetRotation.X = 1.0f * (float)Math.PI;
            camera.CurrentRotation.X = 1.0f * (float)Math.PI;

            Renderer.shaders.deferred = false;
            // HDR on: additive particles accumulate in the float scene buffer and are bloomed + tonemapped by
            // the PostProcessor (the game's pipeline). This keeps a bright, saturated fire core that compresses
            // toward white instead of clipping to a hard white disc - without the no-HDR screen-blend hacks.
            Renderer.shaders.hdr = true;
            // Near-black backdrop (and we skip the sky render below): a bright sky makes HDR auto-exposure crush
            // the fire to a soft, low-contrast wash. Against dark, exposure keys off the fire so flames read
            // sharp and high-contrast like in-game (this is what amStudio/most VFX editors do).
            Renderer.shaders.HdrClearColour = new Color4(0.015f, 0.016f, 0.02f, 0.0f);
            // Bloom OFF for the preview: on a single bright fire over black, the full-scene bloom spreads a huge
            // frame-filling orange halo. The fire's own additive falloff gives enough natural glow.
            Renderer.shaders.hdrBloom = false;
            // Pin HDR exposure to ~1x. Auto-exposure keys off the near-black backdrop and cranks exposure ~3.6x,
            // blowing the flames + the effect's big additive "glow" sprite into a frame-filling disc. Pinning it
            // keeps the fire at its true brightness (and makes EmissiveScale an actual knob). 0.72 = 1x exposure.
            Renderer.shaders.HdrFixedLuminance = 0.72f;
            // The frame-filling orange disc is the effect's big additive "_glow" light sprite (e.g.
            // ent_amb_barrel_fire_race_glow), NOT post-processing - it's meant to glow over a lit scene, but on
            // our black backdrop additive-over-black shows it as a bright disc. Hide it in the preview (set >0
            // for a subtle glow if wanted).
            Renderer.ParticleGlowScale = 0.0f;

            formopen = true;
            new Thread(new ThreadStart(ContentThread)).Start();
            frametimer.Start();
        }

        public void CleanupScene()
        {
            formopen = false;
            Renderer.DeviceDestroyed();
            int count = 0;
            while (running && (count < 5000)) { Thread.Sleep(1); count++; }
        }

        public void RenderScene(DeviceContext context)
        {
            float elapsed = (float)frametimer.Elapsed.TotalSeconds;
            frametimer.Restart();

            GameFileCache.BeginFrame();
            var renderLock = Renderer.RenderSyncRoot;
            if (!renderLock.TryEnter(50)) return;
            try
            {
                UpdateControlInputs(elapsed);
                Renderer.Update(elapsed, MouseLastPoint.X, MouseLastPoint.Y);

                UpdateParticles(elapsed);

                Renderer.BeginRender(context);
                // Skip the sky: the dark HdrClearColour backdrop gives the fire the high-contrast, sharp in-game look.
                Renderer.SelectedDrawable = null;

                //guard the particle render so a transient issue can't kill the render thread (would freeze the view)
                try
                {
                    if (particleEffect != null) Renderer.RenderParticleModels(particleEffect);
                    Renderer.RenderQueued();
                    if (particleEffect != null) Renderer.RenderParticleEffect(particleEffect);
                }
                catch (Exception ex)
                {
                    lastRenderError = ex.Message;
                }

                Renderer.RenderFinalPass();
                Renderer.EndRender();
            }
            finally
            {
                renderLock.Exit();
            }
        }

        public void BuffersResized(int w, int h) { Renderer.BuffersResized(w, h); }
        public bool ConfirmQuit() { return true; }

        #endregion


        #region init / content

        private void ParticleViewportHost_Load(object? sender, EventArgs e)
        {
            if (!initedOk) return;
            if (!GTAFolder.UpdateGTAFolder(true)) return;
            Input.Init();
            Renderer.Start();
        }

        private void ContentThread()
        {
            running = true;
            try { GTA5Keys.LoadFromPath(GTAFolder.CurrentGTAFolder, GTAFolder.IsGen9, Settings.Default.Key); }
            catch { running = false; return; }

            GameFileCache.EnableDlc = true;
            GameFileCache.EnableMods = true;
            GameFileCache.LoadPeds = false;
            GameFileCache.LoadVehicles = false;
            GameFileCache.LoadArchetypes = false;
            GameFileCache.Init(s => { }, s => { });

            // Start the renderable-load loop FIRST so particle textures/renderables load even if the optional
            // world init below throws. (A throw here previously killed the thread before this loop started, so
            // textures never loaded and particles were invisible while the procedural sky still rendered.)
            Task.Run(() => {
                while (formopen && !IsDisposed)
                {
                    bool pending = Renderer.ContentThreadProc();
                    if (!pending) Thread.Sleep(1);
                }
            });

            try
            {
                timecycle.Init(GameFileCache, s => { });
                timecycle.SetTime(Renderer.timeofday);
                BoundsMaterialTypes.Init(GameFileCache);
                weather.Init(GameFileCache, s => { }, timecycle);
                clouds.Init(GameFileCache, s => { }, weather);
            }
            catch { }

            IsCacheReady = true;
            try { BeginInvoke(new Action(() => CacheReady?.Invoke(this, EventArgs.Empty))); } catch { }

            while (formopen && !IsDisposed)
            {
                bool pending = GameFileCache.ContentThreadProc();
                if (!pending) Thread.Sleep(1);
            }

            GameFileCache.Clear();
            running = false;
        }

        #endregion


        #region particle update

        private void UpdateParticles(float elapsed)
        {
            if (particleEffectDirty)
            {
                particleEffectDirty = false;
                var rule = selectedEffectRule;
                if ((rule != null) && (ypt != null))
                {
                    ParticleClipRegions.EnsureLoaded(GameFileCache);
                    particleEffect = new ParticleEffectInst(rule, ypt, GameFileCache);
                    //reapply per-emitter visibility toggles to the freshly built effect
                    if (hiddenEmitters.Count > 0)
                        foreach (var em in particleEffect.Emitters)
                            if (hiddenEmitters.Contains(em.Event)) em.Visible = false;
                }
                else particleEffect = null;
            }

            if (particleEffect != null)
            {
                if (particleRestart) { particleRestart = false; particleEffect.Reset(); }
                if (seekRequested) { seekRequested = false; particleEffect.SeekTo(seekRatio * particleEffect.Duration); }
                particleEffect.Playing = Playing;
                particleEffect.TimeScale = TimeScale;
                particleEffect.Update(elapsed);

                CurrentTime = particleEffect.CurrentTime;
                Duration = particleEffect.Duration;
                ParticleCount = particleEffect.TotalParticleCount();

                //Frame the effect ONCE when a new effect is selected, then leave the camera fixed. Continuously
                //chasing the particle centroid makes the camera oscillate, because the centroid jitters every
                //frame as particles spawn and die. Wait for a few particles for a stable bound, and clamp so a
                //stray/odd position can't fling the camera away.
                if (reframeCamera && (ParticleCount >= 3) && particleEffect.TryGetBounds(out var center, out var radius)
                    && IsFinite(center) && float.IsFinite(radius))
                {
                    reframeCamera = false;
                    camEntity.Position = center;
                    float r = Math.Clamp(radius, 0.3f, 12f);
                    camera.TargetDistance = r * 2.2f;
                    camera.CurrentDistance = r * 2.2f;
                    camera.UpdateProj = true;
                }
            }
            else
            {
                CurrentTime = 0; Duration = 0; ParticleCount = 0;
            }
        }

        #endregion


        private static bool IsFinite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);


        #region camera/input

        private void UpdateControlInputs(float elapsed)
        {
            if (elapsed > 0.1f) elapsed = 0.1f;
            Input.Update();
        }

        private void ParticleViewportHost_MouseDown(object? sender, MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Left: MouseLButtonDown = true; break;
            }
            MouseLastPoint = e.Location;
            MouseX = e.X; MouseY = e.Y;
            Focus();
        }
        private void ParticleViewportHost_MouseUp(object? sender, MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Left: MouseLButtonDown = false; break;
            }
        }
        private void ParticleViewportHost_MouseMove(object? sender, MouseEventArgs e)
        {
            int dx = e.X - MouseX;
            int dy = e.Y - MouseY;
            if (MouseLButtonDown) camera.MouseRotate(dx, dy);
            MouseX = e.X; MouseY = e.Y;
            MouseLastPoint = e.Location;
        }
        private void ParticleViewportHost_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (e.Delta != 0) camera.MouseZoom(e.Delta);
        }
        private void ParticleViewportHost_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                PlayPauseRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true; //stop the space from doing anything else
            }
        }

        #endregion
    }
}
