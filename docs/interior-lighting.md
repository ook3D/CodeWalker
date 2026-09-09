# Interior artificial ambient lighting

Read-only inspection of prp_southside_houses/stream/[subham]/subham_brogue_house_01_{shell,walls,windows}.ydr found ordinary default/spec/normal materials, with wall and shell COLOR0 values R=0, G=128 and window values around R=0, G=164. These request artificial ambient lighting, with no natural ambient contribution. WorldLighting previously supplied only light_artificial_ext_* values to all objects.

MLO child instances now select light_artificial_int_* colours. Both sets use the same intensity, HDR and ambient-wrap conversion. Forward Basic shaders select the colours directly; deferred Basic shaders carry the per-object selection in the irradiance blue channel alongside emissivity (exterior [0,1/3], interior [2/3,1]). Directional deferred lighting decodes both values. Other deferred material producers emit zero in this channel. All affected pixel shader binaries are rebuilt together, including MSAA variants. This avoids changing exterior objects through a window when the camera enters a room.

Validation: actual asset inspection, independent indoor/outdoor weather-colour test, buffer alignment checks, shader compilation with warnings as errors, and viewer tests. The scene has not been visually verified. Portal-based lighting transitions, non-ambient room timecycle effects, exterior objects associated with interior rooms, and room-aware atmospheric fog remain separate limitations.

## Follow-up: room modifier and shader handoff

The shader manager now copies InteriorAmbientUp/Down as well as the main light parameter struct. Without this handoff the earlier interior selection received black, even when WorldLighting computed a nonzero value.

The camera room is selected each frame using the MLO inverse transform and room bounds, excluding limbo (room zero). Degenerate authored bounds fall back to the existing loader-computed attached-entity bounds. The selected room's primary and secondary timecycle modifiers supply artificial ambient colour/intensity from valA, matching the interior-cache path in the reference source. Secondary entries override only supplied fields. Weather values remain the fallback; the weather object is not mutated, and selection resets every frame. Exterior artificial ambient is unchanged.

Actual loose YTYP/YMAP and game timecycle XML inspection at (220.98, -1744.96, 29.31) selected brogue_house_01_main with int_GasStation (hash 1109546596). Its stored room bounds are zero. Using the calculated room bounds, the CPU probe produced HDR ambient RGB up=(0.961, 1, 0.855), down=(1.3995, 1.329, 1.1415). This validates asset selection and colour evaluation, not a visual end-to-end capture. Regression tests cover shader-manager propagation, transformed room bounds, degenerate bounds, secondary overrides and frame reset on exit.
