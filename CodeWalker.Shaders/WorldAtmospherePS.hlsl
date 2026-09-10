// TimeCycle.cpp fog constants and common.fxh __CalcFogData, applied in HDR.
Texture2D<float> DepthTex : register(t0);
cbuffer AtmosphereVars : register(b0)
{
    float4x4 ViewProjInv;
    float4 Distances; // near, far clip, haze start, height falloff
    float4 Densities; // ground at viewer, haze, ground alpha, haze alpha
    float4 GroundColour;
    float4 AtmosphereColour; // w = horizon tint / far clip
    float4 HazeColour;
    float4 SunColour;
    float4 MoonColour;
    float4 SunDirection;
    float4 MoonDirection;
};

struct VS_Output { float4 Pos : SV_POSITION; float2 Tex : TEXCOORD0; };

float4 main(VS_Output input) : SV_TARGET
{
    float depth = DepthTex.Load(int3(input.Pos.xy, 0));
    float4 p = mul(float4(input.Tex * float2(2, -2) + float2(-1, 1), max(depth, 1e-7), 1), ViewProjInv);
    float3 ray = p.xyz / p.w;
    float fullDistance = max(length(ray), 1e-5);
    float3 direction = ray / fullDistance;
    // Sky receives height fog but no distance haze, as in the source sky pass.
    if (depth == 0) fullDistance = Distances.y;
    float distance = max(0, fullDistance - Distances.x);
    float t = Distances.w * direction.z * distance;
    // Analytic horizontal-ray limit also handles a zero height falloff.
    float integral = abs(t) < 1e-3 ? 1 - t * 0.5 : (1 - exp(clamp(-t, -80, 80))) / t;
    float ground = (1 - exp(-Densities.x * distance * integral)) * Densities.z;
    float hazeBlend = (depth > 0 ? 1 : 0) * Densities.w * (1 - ground);
    float haze = hazeBlend * (1 - exp(-Densities.y * max(0, distance - Distances.z)));
    float sun = pow(saturate(dot(direction, SunDirection.xyz)), SunDirection.w);
    float moon = pow(saturate(dot(direction, MoonDirection.xyz)), MoonDirection.w);
    float3 atmosphere = lerp(lerp(AtmosphereColour.rgb, MoonColour.rgb, moon), SunColour.rgb, sun);
    float horizon = 1 - exp(-AtmosphereColour.w * distance);
    float3 colour = lerp(lerp(GroundColour.rgb, atmosphere, horizon), HazeColour.rgb, hazeBlend);
    return float4(colour, saturate(ground + haze));
}
