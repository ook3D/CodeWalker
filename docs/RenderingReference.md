# World rendering reference

The world lighting and HDR output now have separate source-based implementations:

- `CodeWalker/Rendering/WorldLighting.cs` evaluates sun yaw/orbit, sun/moon transitions, the directional-light elevation clamp, and intensity-weighted ambient colours.
- `CodeWalker.Shaders/MaterialLighting.hlsli` supplies the shared material response used by forward and deferred lighting.
- `Common.hlsli` applies GTA's square-law diffuse colour conversion before lighting and squares the decoded material ambient visibility. Deferred diffuse targets retain the authored colour; each lighting pass converts it once, including local lights.
- `WorldAtmosphere.cs` and `WorldAtmospherePS.hlsl` apply timecycle ground fog and distance haze in HDR, before exposure, bloom and tone mapping. They reconstruct world rays from depth and use the source height integral, density unit conversions, sun/moon tint, and separate sky haze behaviour. Fog runs before editor overlays can clear world depth.
- `CodeWalker.Shaders/WorldPostFx.hlsli` evaluates exposure in stops and the bright/dark filmic curves. `PostProcessor.cs` supplies the stock visualsettings defaults and timecycle overrides.

References inspected locally under `X:\gta5\src\dev_ng`: `game/timecycle/TimeCycle.cpp` (`CalcDirectionalLightandPos`), `game/renderer/Lights/lights.cpp` (`UpdateBaseLights`), `game/shader_source/PostFX/postfx.fx`, `exposureGPU.fx`, and `common.fxh`. Filmic/exposure defaults come from `X:\gta5\build\dev\common\data\visualsettings.dat`.

## Stock lighting with modded assets

The Lighting tab's **Original GTA lighting (restart)** option defaults to enabled. It selects unmodded time/weather XML while retaining the existing mod resolution for world assets. Disable it and restart to use mod weather again. This cannot recover stock files that have been overwritten directly in base archives.

A runtime check at 12:00, EXTRASUNNY, GLOBAL found natural ambient base red of `0.066` with this installation's mod weather, versus `4.3969502` with stock weather. The stock sky ambient red was `2.9885`, versus `0.0609375` modded. That discrepancy was a major cause of black walls even after shader adjustments. Stock and modded exposure assumptions must not be mixed accidentally.

## Verification

Build shaders before the application so its output receives the current bytecode:

```powershell
msbuild CodeWalker.Shaders/CodeWalker.Shaders.vcxproj /t:Build /p:Configuration=Release /p:Platform=x64 /v:quiet /nologo
dotnet run --project tests/ReferenceRendering -c Release
```

Run from the repository root on Windows. The checks execute the compiled final-pass shader on D3D11 WARP, compare seven HDR inputs with a CPU filmic reference, check noon lighting and hourly direction validity, verify timecycle whitespace parsing, and verify independent stock/mod archive selection. They require no game installation.

Additional GPU checks verify fog at zero, 100 and 1000 metres, including the zero-height-falloff limit, and verify the diffuse/ambient conversions against analytic values with forward/deferred agreement.

A live world capture confirmed that stock ambient lighting reaches the renderer and shadowed walls retain visible detail. It was not captured with the identical camera/weather state as the supplied GTA screenshot; it is not a pixel-parity test.

## Remaining differences

This is a replacement of core world lighting and tone mapping, not a complete port of GTA's renderer. CodeWalker's shadow cascades, luminance reduction/adaptation, bloom extraction and reflection implementations remain. Full source colour correction, SSAO, environment probes, specialized material passes, timecycle direction overrides and lunar phase intensity are not reproduced here. The moon uses a fixed cycle day unless one is supplied to the evaluator. HDR must be enabled to use the filmic output and atmosphere paths. Stock visualsettings defaults are embedded rather than parsed from a mod's visualsettings file.

Atmosphere supports the deferred renderer (including its MSAA/SSAA paths) and single-sample forward rendering. Forward MSAA skips atmosphere because the existing primary depth target is not shader-readable on every supported DX10 device. It does not yet implement fog volumes, piecewise density layers, fog rays, or separate fog depths for blended transparent layers. Map view skips atmosphere. The separately selected cloud preset is still used; identical time and weather do not by themselves guarantee identical clouds to an in-game screenshot.

An exact match still requires reproducing those passes and comparing identical scene, weather, time, camera and game-version settings.

## Fence and cloth alpha

`cutout_fence` and `cutout_fence_normal` now retain fractional texture coverage in a sorted forward alpha pass rather than discarding everything below the general 0.33 cutoff. `HardAlphaBlend` uses the source's narrow hard-alpha ramp. No screen-space dithering is used. Shadow maps use a fixed 0.5 coverage threshold and the same HD diffuse texture selection; this does not reproduce GTA's temporal/MSAA subsample reconstruction.

`cloth_spec_alpha` and `cloth_normal_spec_alpha` use the drawable's render bucket: bucket 3 uses binary cutouts, while alpha cloth renders with fences after opaque lighting, back-to-front by drawable distance, with depth testing and no depth writes. Texture and vertex alpha multiply once. Intersecting transparent triangles within one drawable are not individually sorted, and this forward pass does not yet receive deferred local lights. Other BasicShader alpha/glass families now use this same sorted path when their render bucket is alpha. Specialized shader families retain their own paths.

Render buckets control opacity for all BasicShader materials, including `spec_const`, `normal_spec` and ordinary building materials. Opaque (0), nosplash (4) and nowater (5) ignore diffuse texture alpha in forward, deferred and shadow rendering. Alpha (1/7), decal (2) and cutout (3) retain their respective coverage behavior. Shader names only refine fence coverage within alpha/cutout buckets; they cannot make an opaque material transparent.

The alpha regression checks execute the compiled Basic forward, Basic deferred and shadow pixel shaders on D3D11 WARP. They verify binary cutout and hard-alpha thresholds, spatially uniform fractional fence alpha, cloth texture alpha multiplied by vertex alpha, opaque pixels with zero texture alpha in all three passes, and shader/bucket classification.
