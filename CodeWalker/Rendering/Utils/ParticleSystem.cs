using System;
using System.Collections.Generic;
using CodeWalker.GameFiles;
using SharpDX;

namespace CodeWalker.Rendering
{
    // CPU particle effect simulator for previewing .ypt files.
    // Practical (not bit-exact) reimplementation of rage's rmptfx (ptxEffectInst/ptxEmitterInst).
    // Covers sprite + model particles: spawn domains (with rotation/inner hollow), particle life,
    // velocity/acceleration/dampening, size/colour/alpha/emissive/rotation over-life curves,
    // animated-texture flipbooks, effect-level tint/zoom, and blend-set selection.
    // Trail particles and velocity-aligned sprite orientation are not yet simulated.

    public enum ParticleBlendMode { Normal = 0, Additive = 1, Composite = 2 }
    public enum ParticleDrawMode { Sprite = 0, Model = 1, Trail = 2 }

    // Per-instance billboard data uploaded to the GPU (must match ParticleVS.hlsl, 64 bytes).
    public struct ParticleInstance
    {
        public Vector3 Position;
        public float Rotation;
        public Vector2 Size;     //half-width, half-height (world units)
        public Vector2 Pad0;
        public Vector4 UVRect;   //xy = uv min, zw = uv max
        public Vector4 Colour;   //rgba 0..1
    }


    public static class ParticleKeyframeEval
    {
        // Evaluate a keyframe prop at normalized time t (linear interp on KeyframeTime.X, clamped both ends).
        public static Vector4 Query(ParticleKeyframeProp prop, float t, Vector4 def)
        {
            var vals = prop?.Values?.data_items;
            if ((vals == null) || (vals.Length == 0)) return def;
            if (vals.Length == 1) return vals[0].KeyframeValue;
            if (t <= vals[0].KeyframeTime.X) return vals[0].KeyframeValue;
            for (int i = 1; i < vals.Length; i++)
            {
                float tc = vals[i].KeyframeTime.X;
                if (t < tc)
                {
                    float tp = vals[i - 1].KeyframeTime.X;
                    float denom = tc - tp;
                    float r = (denom > 1e-8f) ? ((t - tp) / denom) : 0f;
                    return Vector4.Lerp(vals[i - 1].KeyframeValue, vals[i].KeyframeValue, r);
                }
            }
            return vals[vals.Length - 1].KeyframeValue;
        }

        // Emitter-rule KFPs pack (min,max) into value .X/.Y; pick a value in that band using rand01.
        public static float QueryRanged(ParticleKeyframeProp prop, float t, float defMin, float defMax, float rand01)
        {
            var v = Query(prop, t, new Vector4(defMin, defMax, 0f, 0f));
            return v.X + (v.Y - v.X) * rand01;
        }
    }


    public class ParticleEffectInst
    {
        public ParticleEffectRule Rule { get; private set; }
        public YptFile Owner { get; private set; }
        public List<ParticleEmitterInst> Emitters { get; } = new List<ParticleEmitterInst>();

        public float Duration { get; private set; }
        public float CurrentTime { get; private set; }
        public bool Playing = true;
        public float TimeScale = 1.0f;

        // ptxEffectRule m_playbackRateScalarMin/Max: a per-instance random scalar (rmptfx: GetRandPlaybackRateScalar)
        // that scales how fast each spawned particle moves and ages (NOT the emission timeline).
        public float PlaybackRateScalar { get; private set; } = 1.0f;

        // ptxEffectRule effect zoom (rmptfx m_finalZoom): (ZoomScalarKFP/100, lerped min..max by a per-instance
        // random) * (ZoomLevel/100). Multiplies every particle's size each frame. Recomputed in Update.
        public float CurrentZoom { get; private set; } = 1.0f;
        private float zoomRand;

        public bool HasModelParticles { get; private set; }

        // EffectSpawner support: child effects spawned by this effect's particles (EffectSpawnerAtRatio). The root
        // effect loops; spawned children play once. Origin is the world position a child was spawned at; particles
        // in that child spawn relative to it. One spawn level only (children don't spawn grandchildren), as the game.
        public Vector3 Origin;
        public List<ParticleEffectInst> ChildEffects { get; } = new List<ParticleEffectInst>();
        public bool IsSpawnedChild { get; private set; }
        bool loop = true;
        bool finished;
        readonly GameFileCache gfc;
        const int MaxChildEffects = 96;

        private readonly Random rnd = new Random(0x50544658); //"PTFX"

        public ParticleEffectInst(ParticleEffectRule rule, YptFile owner, GameFileCache? gfc = null)
        {
            Rule = rule;
            Owner = owner;
            this.gfc = gfc;

            float dmax = Math.Max(rule?.DurationMax ?? 0f, rule?.DurationMin ?? 0f);
            Duration = (dmax > 0.01f) ? dmax : 4.0f; //fall back to a sane loop length
            PlaybackRateScalar = RandPlaybackRate();
            zoomRand = (float)rnd.NextDouble();

            var emitters = rule?.EventEmitters?.data_items;
            int count = Math.Min(rule?.EventEmittersCount ?? 0, emitters?.Length ?? 0);
            for (int i = 0; i < count; i++)
            {
                var ee = emitters[i];
                if (ee == null) continue;
                var inst = ParticleEmitterInst.TryCreate(ee, rule, owner, gfc, rnd);
                if (inst != null)
                {
                    Emitters.Add(inst);
                    if (inst.DrawMode == ParticleDrawMode.Model) HasModelParticles = true;
                }
            }
        }

        public void Reset()
        {
            CurrentTime = 0f;
            finished = false;
            PlaybackRateScalar = RandPlaybackRate();
            zoomRand = (float)rnd.NextDouble();
            ChildEffects.Clear();
            foreach (var e in Emitters) e.Reset();
        }

        float RandPlaybackRate()
        {
            float pmin = Rule?.PlaybackRateScalarMin ?? 1f;
            float pmax = Rule?.PlaybackRateScalarMax ?? 1f;
            if (pmax <= 0.0001f) return 1f; //unset/zero -> normal rate (avoid frozen particles)
            if (pmax < pmin) pmax = pmin;
            return pmin + (pmax - pmin) * (float)rnd.NextDouble();
        }

        // rmptfx ptxEffectInst::UpdateSetup zoom: ZoomScalarKFP holds (min,max) percentages in X/Y; lerp by a
        // per-instance random, /100, then * (ZoomLevel/100). Falls back to 1x when unset.
        float ComputeZoom(float ratio)
        {
            var z = ParticleKeyframeEval.Query(Rule?.ZoomScalarKFP, ratio, new Vector4(100f, 100f, 0f, 0f));
            float zoomScalar = (z.X + (z.Y - z.X) * zoomRand) / 100f;
            float zoomLevel = Rule?.ZoomLevel ?? 100f;
            if (zoomLevel <= 0.0001f) zoomLevel = 100f; //unset -> 100%
            float zoom = zoomScalar * (zoomLevel / 100f) * spawnZoomScale;
            return (zoom > 0.0001f) ? zoom : 1f;
        }

        // Seek/scrub to an absolute time. A particle sim is stateful, so we reset and re-simulate forward in fixed
        // steps to rebuild the particle state at that time (rather than just jumping the clock).
        public void SeekTo(float time)
        {
            if (Duration <= 0f) { CurrentTime = 0f; return; }
            time = Math.Clamp(time, 0f, Duration - 1e-4f);

            Reset();
            bool wasPlaying = Playing; float wasScale = TimeScale;
            Playing = true; TimeScale = 1f;

            const float step = 1f / 60f;
            float acc = 0f; int guard = 0;
            while ((acc < time) && (guard++ < 4096))
            {
                float dt = Math.Min(step, time - acc);
                Update(dt);
                acc += dt;
            }

            Playing = wasPlaying; TimeScale = wasScale;
            CurrentTime = time;
        }

        public void Update(float dt)
        {
            if (dt < 0f) dt = 0f;
            if (dt > 0.1f) dt = 0.1f; //clamp big frame gaps

            float edt = dt * Math.Max(0f, TimeScale);
            if (Playing)
            {
                CurrentTime += edt;
                if (CurrentTime >= Duration)
                {
                    if (loop) CurrentTime -= Duration;        //root preview loops
                    else { CurrentTime = Duration; finished = true; } //spawned children play once
                }
            }

            // a finished child stops emitting (ratio past the end) but still ages its existing particles out
            float ratio = finished ? 1.0001f : ((Duration > 0f) ? (CurrentTime / Duration) : 0f);
            CurrentZoom = ComputeZoom(Math.Min(ratio, 1f));
            foreach (var e in Emitters)
            {
                e.Update(this, ratio, Playing ? edt : 0f, rnd);
            }

            // update spawned child effects; drop finished ones once their particles have died out
            if (ChildEffects.Count > 0)
            {
                for (int i = ChildEffects.Count - 1; i >= 0; i--)
                {
                    var ch = ChildEffects[i];
                    ch.Playing = Playing;
                    ch.TimeScale = TimeScale;
                    ch.Update(dt);
                    if (ch.finished && (ch.TotalParticleCount() == 0)) ChildEffects.RemoveAt(i);
                }
            }
        }

        // Spawn a child effect (EffectSpawnerAtRatio) at a world position. One level only: children can't spawn.
        public void SpawnChildEffect(ParticleEffectRule childRule, Vector3 worldPos, ParticleEffectSpawner spawner, float particleRemainingLife)
        {
            if (IsSpawnedChild || childRule == null) return;           //one spawn level (matches the game)
            if (ChildEffects.Count >= MaxChildEffects) return;          //cap to avoid runaway spawning

            var child = new ParticleEffectInst(childRule, Owner, gfc)
            {
                IsSpawnedChild = true,
                loop = false,
                Origin = worldPos,
            };

            // apply the spawner's random scalars (rmptfx m_spawnedEffectScalars)
            float durScale = RandSpawnScalar(spawner?.DurationScalarMin ?? 0f, spawner?.DurationScalarMax ?? 0f);
            float rateScale = RandSpawnScalar(spawner?.PlaybackRateScalarMin ?? 0f, spawner?.PlaybackRateScalarMax ?? 0f);
            float zoomScale = RandSpawnScalar(spawner?.ZoomScalarMin ?? 0f, spawner?.ZoomScalarMax ?? 0f);

            child.Duration = Math.Max(0.05f, child.Duration * durScale);
            // InheritsPointLife overrides the child duration with the particle's remaining life (rmptfx SetOOLife)
            if ((spawner?.InheritsPointLife ?? 0) != 0 && particleRemainingLife > 0.01f) child.Duration = particleRemainingLife;
            child.PlaybackRateScalar *= rateScale;
            child.spawnZoomScale = zoomScale;

            ChildEffects.Add(child);
        }

        float spawnZoomScale = 1f; //extra zoom applied to a spawned child (folds into its CurrentZoom)

        static float RandSpawnScalar(float min, float max)
        {
            if (max <= 0.0001f && min <= 0.0001f) return 1f; //unset -> 1x
            return min; //deterministic preview: use min (no per-spawn RNG plumbed); typically min==max
        }

        public int TotalParticleCount()
        {
            int c = 0;
            foreach (var e in Emitters) c += e.Particles.Count;
            foreach (var ch in ChildEffects) c += ch.TotalParticleCount();
            return c;
        }

        // Centroid + radius of all live particles, for framing the camera. False when there are no particles.
        public bool TryGetBounds(out Vector3 center, out float radius)
        {
            center = Vector3.Zero;
            radius = 0f;
            int n = 0;
            Vector3 sum = Vector3.Zero;
            foreach (var e in Emitters)
            {
                foreach (var p in e.Particles) { sum += p.Position; n++; }
            }
            if (n == 0) return false;
            center = sum / n;
            float maxd = 0f;
            foreach (var e in Emitters)
            {
                foreach (var p in e.Particles)
                {
                    float d = (p.Position - center).Length() + Math.Max(p.Size.X, p.Size.Y);
                    if (d > maxd) maxd = d;
                }
            }
            radius = Math.Max(0.1f, maxd);
            return true;
        }
    }


    public class ParticleEmitterInst
    {
        // emitter-rule keyframe-prop name hashes
        const uint H_SpawnRateOverTime = 0x61c50318;
        const uint H_ParticleLife = 0x9fc4652b;
        const uint H_SpeedScalar = 0xc9fe6abb;
        const uint H_SizeScalar = 0x4af0ffa1;
        const uint H_AccnScalar = 0xa83b53f0;
        const uint H_DampeningScalar = 0xdd18b4f2;

        public ParticleEventEmitter Event;
        public ParticleEffectRule EffectRule;
        public ParticleEmitterRule EmitterRule;
        public ParticleRule ParticleRule;
        public Texture SpriteTexture;
        public ParticleBlendMode BlendMode;
        public ParticleDrawMode DrawMode;
        public bool Visible = true; //editor toggle: hide this emitter in the preview (still simulated)
        public bool IsGlow;          //big additive "glow" light sprite - scaled by Renderer.ParticleGlowScale in the preview
        public List<Particle> Particles = new List<Particle>();
        public ParticleDrawable[] Drawables;

        ParticleBehaviourSize sizeBeh;
        ParticleBehaviourColour colourBeh;
        ParticleBehaviourRotation rotBeh;
        ParticleBehaviourAcceleration accelBeh;
        ParticleBehaviourDampening dampBeh;
        ParticleBehaviourVelocity velBeh;
        ParticleBehaviourAnimateTexture animBeh;

        ParticleKeyframeProp kfSpawnRate, kfParticleLife, kfSpeedScalar, kfSizeScalar, kfAccnScalar, kfDampScalar;

        Vector4 colourTintMin, colourTintMax;
        float emitterZoom = 1f;
        float effectPlaybackRate = 1f; //captured from the owning effect inst each Update, applied per spawned particle
        float effectZoom = 1f;         //effect-rule zoom (ZoomScalar/ZoomLevel), multiplies particle size each frame
        ParticleEffectInst ownerEffect; //owning effect inst, for EffectSpawner child spawning + world origin
        Vector3 effectOrigin;           //world position of the owning effect (children spawn relative to it)
        ParticleEffectSpawner atRatioSpawner;   //EffectSpawnerAtRatio (if it names a child effect)
        ParticleEffectRule atRatioChildRule;    //resolved child effect rule to spawn at the trigger ratio
        int atlasCols = 1, atlasRows = 1, atlasFrames = 1;
        int texFrameMin, texFrameMax;
        uint spriteTexHash;
        string spriteTexName;
        GameFileCache gameFileCache; //optional, for resolving textures not embedded in the ypt
        bool rotAccumulate;
        int maxParticles = DefaultMaxParticles;
        const float DegToRad = (float)(Math.PI / 180.0);
        const int DefaultMaxParticles = 4000;
        // Soft additive "glow" sprites are bloom elements meant to overlap into a halo; the game relies on
        // HDR bloom so it can spawn hundreds. Without a bloom pass that just saturates to a hard white disc,
        // so we cap glow emitters to a handful (enough to read as a glow, not a solid sphere).
        const int GlowMaxParticles = 24;
        // Emissive boost for additive particles. The game's m_emissiveIntensityKFP reaches ~5, but that's
        // calibrated for a full HDR scene; in the isolated dark preview (exposure keys off the fire) the raw
        // value over-blooms to a white core + huge halo. Apply only a FRACTION of the boost: emissive=1 at
        // raw<=1, ramping to ~1+(raw-1)*EmissiveScale. Tune this one number if the fire is too hot / too dim.
        const float EmissiveScale = 0.30f;

        bool oneShotDone;
        float spawnAccum;

        public static ParticleEmitterInst TryCreate(ParticleEventEmitter ee, ParticleEffectRule effect, YptFile owner, GameFileCache? gfc = null, Random? rnd = null)
        {
            var prule = ee?.ParticleRule;
            if (prule == null) return null;
            if (prule.DrawType == 2) return null; //trails not simulated yet

            var inst = new ParticleEmitterInst();
            inst.Event = ee;
            inst.EffectRule = effect;
            inst.EmitterRule = ee.EmitterRule;
            inst.ParticleRule = prule;
            inst.gameFileCache = gfc;
            inst.DrawMode = (ParticleDrawMode)prule.DrawType;

            // EventEmitter ZoomScalarMin/Max (rmptfx ptxEmitterInst m_zoomScalar): a per-emitter RAW multiplier
            // (1.0 = 1x, NOT a percentage) that multiplies the effect zoom and so scales this emitter's size.
            inst.emitterZoom = RandScalarRaw(ee.ZoomScalarMin, ee.ZoomScalarMax, rnd);

            inst.CollectBehaviours();
            inst.rotAccumulate = (inst.rotBeh?.AccumulateAngle ?? 0) != 0;
            inst.CollectEmitterKeyframes();
            inst.ResolveTexture(owner);
            inst.ResolveDrawables(owner);
            inst.ResolveAtlas();
            inst.colourTintMin = UnpackColour(ee.ColourTintMin);
            inst.colourTintMax = UnpackColour(ee.ColourTintMax);
            inst.BlendMode = MapBlend(prule.BlendSet);

            // EffectSpawnerAtRatio: resolve the child effect rule to spawn when a particle crosses the trigger ratio.
            inst.atRatioSpawner = prule.EffectSpawnerAtRatio;
            inst.atRatioChildRule = ResolveSpawnerRule(prule.EffectSpawnerAtRatio, owner);

            // Many GTA particle sprites store the image in RGB on black with no alpha channel (DXT1/X8/L8,
            // often named "*_rgb") and are meant to be drawn additively so the black background disappears.
            // A normal-alpha rule over such a texture would otherwise show an opaque black box, so force
            // additive when the rule is normal AND the texture has no usable alpha.
            if ((inst.BlendMode == ParticleBlendMode.Normal) && inst.TextureHasNoAlpha())
            {
                inst.BlendMode = ParticleBlendMode.Additive;
            }

            // additive radial "glow" sprites are big soft light-glow billboards. In-game they add subtly over a
            // LIT scene; on the preview's black backdrop, additive-over-black shows their full self as a bright
            // frame-filling disc. Flag them so the preview can scale/hide their contribution (Renderer.ParticleGlowScale).
            if ((inst.BlendMode == ParticleBlendMode.Additive) && (inst.spriteTexName != null) && inst.spriteTexName.Contains("glow"))
            {
                inst.maxParticles = GlowMaxParticles;
                inst.IsGlow = true;
            }

            return inst;
        }

        // Resolve the child effect rule an EffectSpawner points at: prefer the resolved pointer, else look it up
        // by name in the ypt's effect-rule dictionary (binary loads don't run AssignChildren).
        static ParticleEffectRule ResolveSpawnerRule(ParticleEffectSpawner? spawner, YptFile owner)
        {
            if (spawner == null) return null;
            if (spawner.EffectRule != null) return spawner.EffectRule;
            var name = spawner.EffectRuleName?.Value;
            if (string.IsNullOrEmpty(name)) return null;
            var rules = owner?.PtfxList?.EffectRuleDictionary?.EffectRules?.data_items;
            if (rules == null) return null;
            foreach (var efr in rules)
            {
                if ((efr != null) && string.Equals(efr.Name?.Value, name, StringComparison.OrdinalIgnoreCase)) return efr;
            }
            return null;
        }

        // a raw random multiplier (1.0 = 1x); 0/unset -> 1x so it never zero-scales the emitter
        static float RandScalarRaw(float min, float max, Random rnd)
        {
            if (max <= 0.0001f) return 1f;
            if (max < min) max = min;
            float r = (rnd != null) ? (float)rnd.NextDouble() : 0.5f;
            return min + (max - min) * r;
        }

        bool TextureHasNoAlpha()
        {
            var fmt = SpriteTexture?.Format;
            if (fmt != null)
            {
                switch (fmt.Value)
                {
                    case TextureFormat.D3DFMT_DXT1:      //BC1 - no (usable) alpha
                    case TextureFormat.D3DFMT_X8R8G8B8:  //no alpha
                    case TextureFormat.D3DFMT_L8:        //luminance only
                        return true;
                }
                return false; //DXT3/DXT5/BC7/A8R8G8B8/A8 etc. carry a real alpha mask
            }
            //no format available (e.g. external reference): fall back to the GTA "_rgb" naming convention
            return (spriteTexName != null) && spriteTexName.EndsWith("_rgb");
        }

        static ParticleBlendMode MapBlend(int blendSet)
        {
            switch (blendSet)
            {
                case 1:  //grcbsAdd
                case 4:  //grcbsMatte (additive cutout)
                case 7:  //grcbsAlphaAdd
                case 10: //grcbsMax
                    return ParticleBlendMode.Additive;
                case 13: //grcbsCompositeAlpha (premultiplied)
                case 14: //grcbsCompositeAlphaSubtract
                    return ParticleBlendMode.Composite;
                default: //grcbsNormal etc.
                    return ParticleBlendMode.Normal;
            }
        }

        void CollectBehaviours()
        {
            CollectFrom(ParticleRule.AllBehaviours);
            CollectFrom(ParticleRule.UpdateBehaviours);
            CollectFrom(ParticleRule.InitBehaviours);
            CollectFrom(ParticleRule.DrawBehaviours);
        }
        void CollectFrom(ResourcePointerList64<ParticleBehaviour> list)
        {
            var items = list?.data_items;
            if (items == null) return;
            foreach (var b in items)
            {
                switch (b)
                {
                    case ParticleBehaviourSize s: sizeBeh = sizeBeh ?? s; break;
                    case ParticleBehaviourColour c: colourBeh = colourBeh ?? c; break;
                    case ParticleBehaviourRotation r: rotBeh = rotBeh ?? r; break;
                    case ParticleBehaviourAcceleration a: accelBeh = accelBeh ?? a; break;
                    case ParticleBehaviourDampening d: dampBeh = dampBeh ?? d; break;
                    case ParticleBehaviourVelocity v: velBeh = velBeh ?? v; break;
                    case ParticleBehaviourAnimateTexture t: animBeh = animBeh ?? t; break;
                }
            }
        }

        void CollectEmitterKeyframes()
        {
            var kfs = EmitterRule?.KeyframeProps;
            if (kfs == null) return;
            foreach (var kf in kfs)
            {
                if (kf == null) continue;
                switch ((uint)kf.Name)
                {
                    case H_SpawnRateOverTime: kfSpawnRate = kf; break;
                    case H_ParticleLife: kfParticleLife = kf; break;
                    case H_SpeedScalar: kfSpeedScalar = kf; break;
                    case H_SizeScalar: kfSizeScalar = kf; break;
                    case H_AccnScalar: kfAccnScalar = kf; break;
                    case H_DampeningScalar: kfDampScalar = kf; break;
                }
            }
        }

        void ResolveTexture(YptFile owner)
        {
            var vars = ParticleRule?.ShaderVars?.data_items;
            if (vars != null)
            {
                var td = owner?.PtfxList?.TextureDictionary;
                foreach (var v in vars)
                {
                    if (v is not ParticleShaderVarTexture tex) continue;
                    //rules carry several texture slots and the first ones are usually empty placeholders
                    //(TextureNameHash == 0); the sprite texture is the first slot that actually names a texture.
                    if (tex.TextureNameHash == 0) continue;
                    spriteTexHash = tex.TextureNameHash;
                    spriteTexName = (tex.TextureName?.Value ?? tex.Texture?.Name)?.ToLowerInvariant();
                    if (tex.Texture != null) { SpriteTexture = tex.Texture; return; }
                    var t = td?.Lookup(tex.TextureNameHash);
                    if (t != null) { SpriteTexture = t; if (spriteTexName == null) spriteTexName = t.Name?.ToLowerInvariant(); return; }
                    ResolveTextureFromCache(); //fall back to game files (may resolve later once the ytd loads)
                    return;
                }
            }
        }

        void ResolveTextureFromCache()
        {
            if ((SpriteTexture != null) || (gameFileCache == null) || (spriteTexHash == 0)) return;
            var ytd = gameFileCache.TryGetTextureDictForTexture(spriteTexHash);
            var t = ytd?.TextureDict?.Lookup(spriteTexHash); //null until the ytd finishes loading; retried in Update
            if (t != null)
            {
                SpriteTexture = t;
                if (spriteTexName == null) spriteTexName = t.Name?.ToLowerInvariant();
            }
        }

        void ResolveDrawables(YptFile owner)
        {
            var items = ParticleRule?.Drawables?.data_items;
            if ((items == null) || (items.Length == 0)) return;
            Drawables = items;
            foreach (var pd in items)
            {
                if (pd?.Drawable != null) pd.Drawable.Owner = owner; //so embedded textures resolve when rendering
            }
        }

        void ResolveAtlas()
        {
            texFrameMin = (int)(ParticleRule?.TexFrameIDMin ?? 0);
            texFrameMax = (int)(ParticleRule?.TexFrameIDMax ?? 0);

            // ptxclipregions.dat gives the real (possibly non-square) cols x rows for the sheet. Modern atlases
            // are uniform grids, so we use cols x rows to lay out cells. (The dat also has explicit per-frame
            // rects, but those describe pan windows for a few stock continuous textures and don't match grid
            // atlases, so we don't use them here.)
            var cr = ParticleClipRegions.Get(spriteTexHash);
            if ((cr != null) && (cr.Cols * cr.Rows > 1))
            {
                atlasCols = Math.Max(1, cr.Cols);
                atlasRows = Math.Max(1, cr.Rows);
                atlasFrames = atlasCols * atlasRows;
                if (texFrameMax >= atlasFrames) texFrameMax = atlasFrames - 1;
                if (texFrameMin > texFrameMax) texFrameMin = texFrameMax;
                return;
            }

            // fallback: a sprite-sheet is in use whenever frame ids > 0 are referenced or an AnimateTexture
            // behaviour exists. The real grid is unknown without clip-region data, so approximate a square grid.
            int animFrames = (animBeh != null) ? (animBeh.LastFrameID + 1) : 0;
            int frames = Math.Max(animFrames, texFrameMax + 1);
            if (frames <= 1) { atlasCols = atlasRows = atlasFrames = 1; return; }

            int dim = (int)Math.Ceiling(Math.Sqrt(frames));
            atlasCols = atlasRows = Math.Max(1, dim);
            atlasFrames = frames;
        }

        public void Reset()
        {
            Particles.Clear();
            spawnAccum = 0f;
            oneShotDone = false;
        }

        public void Update(ParticleEffectInst effect, float effectRatio, float dt, Random rnd)
        {
            if ((SpriteTexture == null) && (gameFileCache != null)) ResolveTextureFromCache(); //retry async ytd load

            effectPlaybackRate = (effect?.PlaybackRateScalar ?? 1f);
            effectZoom = (effect?.CurrentZoom ?? 1f);
            ownerEffect = effect;
            effectOrigin = effect?.Origin ?? Vector3.Zero;
            UpdateParticles(effectRatio, dt);

            float start = Event?.StartRatio ?? 0f;
            float end = Event?.EndRatio ?? 1f;
            if (end <= start) end = 1f;
            bool active = (effectRatio >= start) && (effectRatio <= end);
            float emitRatio = (end > start) ? Clamp01((effectRatio - start) / (end - start)) : 0f;

            if (!active)
            {
                oneShotDone = false; //rearm one-shot for the next loop
                return;
            }

            bool oneShot = (EmitterRule?.IsOneShot ?? 0) != 0;
            float spawnRate = Math.Max(0f, ParticleKeyframeEval.QueryRanged(kfSpawnRate, emitRatio, 10f, 10f, (float)rnd.NextDouble()));

            // For capped (glow) emitters, rate-limit spawning to ~cap/life instead of bursting to the cap and
            // hard-blocking. Bursting makes all particles spawn (and then die) together, which pulses; spreading
            // the spawns over the lifetime keeps a steady, staggered population.
            if (!oneShot && (maxParticles < DefaultMaxParticles))
            {
                var lifev = ParticleKeyframeEval.Query(kfParticleLife, emitRatio, new Vector4(1f, 1f, 0f, 0f));
                float lifeEst = Math.Max(0.1f, (lifev.X + lifev.Y) * 0.5f);
                float maxRate = maxParticles / lifeEst;
                if (spawnRate > maxRate) spawnRate = maxRate;
            }

            int numToSpawn = 0;
            if (oneShot)
            {
                if (!oneShotDone) { numToSpawn = (int)spawnRate; oneShotDone = true; }
            }
            else
            {
                spawnAccum += spawnRate * dt;
                numToSpawn = (int)spawnAccum;
                spawnAccum -= numToSpawn;
            }

            Vector4 effectTint = ComputeEffectTint(effectRatio);
            for (int i = 0; i < numToSpawn; i++)
            {
                if (Particles.Count >= maxParticles) break;
                SpawnParticle(emitRatio, effectTint, rnd);
            }
        }

        Vector4 ComputeEffectTint(float effectRatio)
        {
            if (EffectRule == null) return Vector4.One;
            var etmin = ParticleKeyframeEval.Query(EffectRule.ColourTintMinKFP, effectRatio, Vector4.One);
            if (EffectRule.ColourTintMaxEnable != 0)
            {
                var etmax = ParticleKeyframeEval.Query(EffectRule.ColourTintMaxKFP, effectRatio, Vector4.One);
                return Vector4.Lerp(etmin, etmax, 0.5f);
            }
            return etmin;
        }

        // Evaluates a particle's appearance (size/colour/rotation/UV) at normalized life time nt. Shared by the
        // per-frame update AND the spawn path, so a just-spawned particle renders with its real nt=0 look
        // (e.g. alpha 0 fade-in, correct size) instead of flashing full-white BaseSize for one frame.
        void EvaluateVisuals(ref Particle p, float nt, float dt, Vector4 effectTint)
        {
            // size
            Vector4 whd = Vector4.One;
            if (sizeBeh != null)
            {
                var wmin = ParticleKeyframeEval.Query(sizeBeh.WhdMinKFP, nt, Vector4.One);
                var wmax = ParticleKeyframeEval.Query(sizeBeh.WhdMaxKFP, nt, Vector4.One);
                whd = LerpChannels(wmin, wmax, p.RandSize);
            }
            whd *= p.SizeScalar;  // sizeScalar = emitterSizeScalar/100 * emitterZoom
            whd *= effectZoom;    // effect-rule zoom (ZoomScalarKFP * ZoomLevel), time-varying over effect life
            if (DrawMode == ParticleDrawMode.Model)
            {
                p.ModelScale = new Vector3(
                    (whd.X > 1e-4f) ? whd.X : 1f,
                    (whd.Y > 1e-4f) ? whd.Y : 1f,
                    (whd.Z > 1e-4f) ? whd.Z : 1f);
            }
            else
            {
                p.Size = new Vector2(whd.X * 0.5f, whd.Y * 0.5f); // sprite half-extents (TBLR = WHD.YYXX * 0.5)
            }

            // colour + alpha
            Vector4 col = Vector4.One;
            if (colourBeh != null)
            {
                var cmin = ParticleKeyframeEval.Query(colourBeh.RGBAMinKFP, nt, Vector4.One);
                Vector4 rgba = cmin;
                if (colourBeh.RGBAMaxEnable != 0)
                {
                    var cmax = ParticleKeyframeEval.Query(colourBeh.RGBAMaxKFP, nt, Vector4.One);
                    rgba = LerpChannels(cmin, cmax, p.RandColour);
                }
                rgba *= p.Tint;
                rgba *= effectTint;
                col = rgba;
            }
            // Emissive intensity (ptxu_Colour:m_emissiveIntensityKFP): in-game this drives a SEPARATE additive
            // pass into the HDR/bloom buffer and reaches ~5. With HDR on we apply it as an RGB multiply on
            // additive particles so the fire pushes well past 1.0 in the float scene buffer and survives the
            // auto-exposure (keyed to the bright sky) + tonemap - otherwise the fire reads faint. Floored at 1
            // so it only ever BOOSTS (our single additive pass is all the fire has; dimming it would hide it).
            if ((BlendMode == ParticleBlendMode.Additive) && (colourBeh?.EmissiveIntensityKFP != null))
            {
                var e = ParticleKeyframeEval.Query(colourBeh.EmissiveIntensityKFP, nt, Vector4.One);
                float raw = Math.Max(Math.Max(e.X, e.Y), 1f);
                float emissive = 1f + (raw - 1f) * EmissiveScale; // scaled-down boost (see EmissiveScale)
                col.X *= emissive;
                col.Y *= emissive;
                col.Z *= emissive;
            }
            p.Colour = col;

            // rotation: angle KFP is in degrees; accumulate mode = spin rate (deg/s), else absolute offset
            if (rotBeh != null)
            {
                float aMin = ParticleKeyframeEval.Query(rotBeh.AngleMinKFP, nt, Vector4.Zero).X;
                float aMax = ParticleKeyframeEval.Query(rotBeh.AngleMaxKFP, nt, Vector4.Zero).X;
                float ang = (aMin + (aMax - aMin) * p.RandRot) * DegToRad;
                if (rotAccumulate) p.Rotation += ang * dt; //ang is radians/sec
                else p.Rotation = p.InitialAngle + ang;    //ang is an absolute offset
            }

            // animated texture frame
            if (atlasFrames > 1)
            {
                int frame = p.InitFrame;
                if (animBeh != null)
                {
                    float rate = ParticleKeyframeEval.Query(animBeh.AnimRateKFP, nt, new Vector4(1f, 1f, 0f, 0f)).X;
                    float frames = (animBeh.IsScaledOverParticleLife != 0) ? (nt * (animBeh.LastFrameID + 1)) : (p.Age * rate);
                    frame = p.InitFrame + (int)frames;
                    int max = animBeh.LastFrameID + 1;
                    if (animBeh.IsHeldOnLastFrame != 0) frame = Math.Min(frame, max - 1);
                    else if (max > 0) frame %= max;
                }
                p.UVRect = FrameUV(frame);
            }
            else
            {
                p.UVRect = new Vector4(0f, 0f, 1f, 1f);
            }
        }

        void UpdateParticles(float effectRatio, float dt)
        {
            Vector4 effectTint = ComputeEffectTint(effectRatio);

            for (int i = Particles.Count - 1; i >= 0; i--)
            {
                var p = Particles[i];
                //the effect's PlaybackRateScalar scales how fast this particle moves and ages (rmptfx: per-point
                //m_playbackRate applied to velocity integration and life), but NOT the emission timeline.
                float pdt = dt * ((p.PlaybackRate > 0f) ? p.PlaybackRate : 1f);
                p.Age += pdt;
                if (p.Age >= p.Life)
                {
                    Particles.RemoveAt(i);
                    continue;
                }
                float nt = (p.Life > 0f) ? (p.Age / p.Life) : 0f;

                // acceleration -> velocity
                if (accelBeh != null)
                {
                    var amin = ParticleKeyframeEval.Query(accelBeh.XYZMinKFP, nt, Vector4.Zero);
                    var amax = ParticleKeyframeEval.Query(accelBeh.XYZMaxKFP, nt, Vector4.Zero);
                    Vector3 a = LerpXYZ(amin, amax, p.RandAccel);
                    a *= p.AccnScalar;
                    if (accelBeh.EnableGravity != 0) a.Z -= 9.81f;
                    p.Velocity += a * pdt;
                }

                // dampening (velocity drag)
                if (dampBeh != null)
                {
                    var dmin = ParticleKeyframeEval.Query(dampBeh.XYZMinKFP, nt, Vector4.Zero);
                    var dmax = ParticleKeyframeEval.Query(dampBeh.XYZMaxKFP, nt, Vector4.Zero);
                    Vector3 damp = LerpXYZ(dmin, dmax, p.RandDamp) * p.DampScalar;
                    Vector3 velScale = new Vector3(
                        Math.Max(0f, 1f - damp.X * pdt),
                        Math.Max(0f, 1f - damp.Y * pdt),
                        Math.Max(0f, 1f - damp.Z * pdt));
                    p.Velocity = new Vector3(p.Velocity.X * velScale.X, p.Velocity.Y * velScale.Y, p.Velocity.Z * velScale.Z);
                }

                // velocity -> position (only when the rule has a velocity behaviour, as the game does)
                if (velBeh != null)
                {
                    p.Position += p.Velocity * pdt;
                }

                EvaluateVisuals(ref p, nt, pdt, effectTint);

                // EffectSpawnerAtRatio: when this particle's life ratio crosses the trigger ratio, spawn the child
                // effect at the particle's current world position (rmptfx ptxu_Age sets PTXPOINT_FLAG_SPAWN_EFFECT).
                if ((atRatioChildRule != null) && (ownerEffect != null) && (pdt > 0f))
                {
                    float prevNt = (p.Life > 0f) ? ((p.Age - pdt) / p.Life) : 0f;
                    float trig = atRatioSpawner.TriggerInfo;
                    // rmptfx ptxu_Age: fire when trig is in [prevLifeRatio, nextLifeRatio). trig==0 must fire on the
                    // first update frame (prevNt==0), so the lower bound is inclusive.
                    if ((trig >= 0f) && (trig >= prevNt) && (trig < nt))
                    {
                        ownerEffect.SpawnChildEffect(atRatioChildRule, p.Position, atRatioSpawner, Math.Max(0f, p.Life - p.Age));
                    }
                }

                Particles[i] = p;
            }
        }

        Vector4 FrameUV(int frame)
        {
            if (atlasFrames <= 1) return new Vector4(0f, 0f, 1f, 1f);
            if (frame < 0) frame = 0;
            frame %= atlasFrames;
            int col = frame % atlasCols;
            int row = frame / atlasCols;
            float fw = 1f / atlasCols;
            float fh = 1f / atlasRows;
            return new Vector4(col * fw, row * fh, (col + 1) * fw, (row + 1) * fh);
        }

        void SpawnParticle(float emitRatio, Vector4 effectTint, Random rnd)
        {
            var p = new Particle();

            p.Life = Math.Max(0.05f, ParticleKeyframeEval.QueryRanged(kfParticleLife, emitRatio, 1f, 1f, (float)rnd.NextDouble()));
            p.Age = 0f;
            p.PlaybackRate = effectPlaybackRate; //effect PlaybackRateScalar - scales this particle's movement + aging

            // creation domain: spawn position
            Vector3 pos = SampleDomain(EmitterRule?.CreationDomainObj, emitRatio, rnd);

            // target domain: velocity direction
            float speed = ParticleKeyframeEval.QueryRanged(kfSpeedScalar, emitRatio, 0f, 0f, (float)rnd.NextDouble());
            Vector3 vel = Vector3.Zero;
            var target = EmitterRule?.TargetDomainObj;
            if (target != null)
            {
                Vector3 tpos = SampleDomain(target, emitRatio, rnd);
                vel = (target.IsPointRelative != 0) ? tpos : (tpos - pos);
                vel *= speed;
            }

            p.Position = effectOrigin + pos; //effectOrigin is non-zero for spawned child effects (world placement)
            p.Velocity = vel;

            // per-emitter scalars (queried at emit time, randomised in min/max band)
            float sizeScalarPct = Math.Max(0f, ParticleKeyframeEval.QueryRanged(kfSizeScalar, emitRatio, 100f, 100f, (float)rnd.NextDouble()));
            p.SizeScalar = (sizeScalarPct / 100f) * emitterZoom;
            p.AccnScalar = ParticleKeyframeEval.QueryRanged(kfAccnScalar, emitRatio, 100f, 100f, (float)rnd.NextDouble()) / 100f;
            p.DampScalar = ParticleKeyframeEval.QueryRanged(kfDampScalar, emitRatio, 100f, 100f, (float)rnd.NextDouble()) / 100f;
            p.BaseSize = new Vector2(0.25f, 0.25f);

            // per-channel random biases (the game uses a random table indexed per channel)
            p.RandSize = RandVec4(rnd);
            p.RandColour = RandVec4(rnd);
            p.RandAccel = (float)rnd.NextDouble();
            p.RandDamp = (float)rnd.NextDouble();
            p.Tint = ColourTint(rnd);

            // rotation: random initial angle (degrees -> radians); RandRot biases the over-life angle curve
            p.RandRot = (float)rnd.NextDouble();
            if (rotBeh != null)
            {
                float ia0 = ParticleKeyframeEval.Query(rotBeh.InitialAngleMinKFP, 0f, Vector4.Zero).X;
                float ia1 = ParticleKeyframeEval.Query(rotBeh.InitialAngleMaxKFP, 0f, Vector4.Zero).X;
                p.InitialAngle = (ia0 + (ia1 - ia0) * (float)rnd.NextDouble()) * DegToRad;
            }
            p.Rotation = p.InitialAngle;

            // model particles pick a random drawable
            if ((DrawMode == ParticleDrawMode.Model) && (Drawables != null) && (Drawables.Length > 0))
            {
                p.DrawableIndex = rnd.Next(Drawables.Length);
                // give models a random initial orientation + spin so they don't all face the same way
                p.ModelYaw = (float)(rnd.NextDouble() * Math.PI * 2.0);
                p.ModelPitch = (float)(rnd.NextDouble() * Math.PI * 2.0);
                p.ModelScale = new Vector3(Math.Max(0.01f, p.SizeScalar)); //avoid a zero-scale first frame
            }

            // initial atlas frame: each particle picks a random cell in [TexFrameIDMin, TexFrameIDMax]
            if (atlasFrames > 1)
            {
                int range = Math.Max(1, texFrameMax - texFrameMin + 1);
                p.InitFrame = texFrameMin + rnd.Next(range);
            }

            // evaluate the nt=0 appearance now so a just-spawned particle renders with its real initial look
            // (correct size, fade-in alpha, cell UV) instead of flashing full-white BaseSize for one frame.
            EvaluateVisuals(ref p, 0f, 0f, effectTint);

            Particles.Add(p);
        }

        Vector3 SampleDomain(ParticleDomain? dom, float t, Random rnd)
        {
            if (dom == null) return Vector3.Zero;

            var posv = ParticleKeyframeEval.Query(dom.PositionKFP, t, Vector4.Zero);
            Vector3 center = new Vector3(posv.X, posv.Y, posv.Z);
            var sov = ParticleKeyframeEval.Query(dom.SizeOuterKFP, t, Vector4.Zero);
            Vector3 so = new Vector3(sov.X, sov.Y, sov.Z);
            var siv = ParticleKeyframeEval.Query(dom.SizeInnerKFP, t, Vector4.Zero);
            Vector3 si = new Vector3(siv.X, siv.Y, siv.Z);

            Vector3 local;
            switch (dom.DomainType)
            {
                case ParticleDomainType.Sphere:
                    {
                        Vector3 dir = RandDir(rnd);
                        float outer = so.X;
                        float inner = (si.X > 0f && si.X < outer) ? si.X : 0f;
                        float r = inner + (outer - inner) * (float)rnd.NextDouble();
                        local = dir * r;
                        break;
                    }
                case ParticleDomainType.Cylinder:
                    {
                        double ang = rnd.NextDouble() * Math.PI * 2.0;
                        float ox = so.X * (float)Math.Cos(ang);
                        float oz = so.Z * (float)Math.Sin(ang);
                        Vector3 d = new Vector3(ox, 0f, oz);
                        float outerR = d.Length();
                        if (outerR > 1e-6f) d /= outerR;
                        float innerR = (si.X > 0f && si.X < outerR) ? si.X : 0f;
                        float r = innerR + (outerR - innerR) * (float)rnd.NextDouble();
                        local = d * r;
                        local.Y = (float)(rnd.NextDouble() * 2.0 - 1.0) * so.Y;
                        break;
                    }
                case ParticleDomainType.Box:
                default:
                    {
                        local = new Vector3(
                            SignedRangedHollow(so.X, si.X, rnd),
                            SignedRangedHollow(so.Y, si.Y, rnd),
                            SignedRangedHollow(so.Z, si.Z, rnd));
                        break;
                    }
            }

            // apply domain rotation (euler radians)
            var rotv = ParticleKeyframeEval.Query(dom.RotationKFP, t, Vector4.Zero);
            if ((rotv.X != 0f) || (rotv.Y != 0f) || (rotv.Z != 0f))
            {
                var q = Quaternion.RotationYawPitchRoll(rotv.Z, rotv.Y, rotv.X);
                local = Vector3.Transform(local, q);
            }

            return center + local;
        }

        static float SignedRangedHollow(float outer, float inner, Random rnd)
        {
            float v = (float)(rnd.NextDouble() * 2.0 - 1.0) * outer;
            if (inner > 0f && inner < outer)
            {
                // bias away from the hollow inner region (cheap approximation)
                if (Math.Abs(v) < inner)
                {
                    float sign = (rnd.NextDouble() < 0.5) ? -1f : 1f;
                    v = sign * (inner + (float)rnd.NextDouble() * (outer - inner));
                }
            }
            return v;
        }

        Vector4 ColourTint(Random rnd)
        {
            if (IsWhiteOrZero(colourTintMin) && IsWhiteOrZero(colourTintMax)) return Vector4.One;
            return Vector4.Lerp(colourTintMin, colourTintMax, (float)rnd.NextDouble());
        }

        static bool IsWhiteOrZero(Vector4 c)
        {
            return (c == Vector4.Zero) || (c == Vector4.One);
        }

        static Vector4 RandVec4(Random rnd)
        {
            return new Vector4((float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble());
        }
        static Vector3 LerpXYZ(Vector4 a, Vector4 b, float t)
        {
            return new Vector3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        }
        static Vector4 LerpChannels(Vector4 a, Vector4 b, Vector4 t)
        {
            return new Vector4(
                a.X + (b.X - a.X) * t.X,
                a.Y + (b.Y - a.Y) * t.Y,
                a.Z + (b.Z - a.Z) * t.Z,
                a.W + (b.W - a.W) * t.W);
        }

        static Vector3 RandDir(Random rnd)
        {
            Vector3 d;
            float len;
            int guard = 0;
            do
            {
                d = new Vector3(
                    (float)(rnd.NextDouble() * 2.0 - 1.0),
                    (float)(rnd.NextDouble() * 2.0 - 1.0),
                    (float)(rnd.NextDouble() * 2.0 - 1.0));
                len = d.Length();
            } while (len < 1e-4f && guard++ < 4);
            if (len < 1e-4f) return new Vector3(0f, 0f, 1f);
            return d / len;
        }

        static float Clamp01(float v) { return (v < 0f) ? 0f : ((v > 1f) ? 1f : v); }

        static Vector4 UnpackColour(uint c)
        {
            float r = (c & 0xFF) / 255f;
            float g = ((c >> 8) & 0xFF) / 255f;
            float b = ((c >> 16) & 0xFF) / 255f;
            float a = ((c >> 24) & 0xFF) / 255f;
            return new Vector4(r, g, b, a);
        }
    }


    public struct Particle
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Age;
        public float Life;
        public float PlaybackRate;  //scales this particle's movement + aging (effect PlaybackRateScalar)

        public Vector2 BaseSize;
        public float SizeScalar;
        public float AccnScalar;
        public float DampScalar;
        public Vector2 Size;       //current half-extents
        public Vector4 Colour;     //current rgba
        public Vector4 Tint;       //per-particle tint
        public float Rotation;
        public float InitialAngle;
        public float RandRot;
        public int InitFrame;
        public Vector4 UVRect;

        public int DrawableIndex;  //model particles
        public float ModelYaw;
        public float ModelPitch;
        public Vector3 ModelScale;

        public Vector4 RandSize;   //per-channel size bias
        public Vector4 RandColour; //per-channel colour bias
        public float RandAccel;
        public float RandDamp;
    }
}
