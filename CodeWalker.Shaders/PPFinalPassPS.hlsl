
#include "WorldPostFx.hlsli"

struct VS_Output
{
    float4 Pos : SV_POSITION;              
    float2 Tex : TEXCOORD0;
};


Texture2D<float4> tex : register( t0 );
StructuredBuffer<float> lum : register( t1 );
Texture2D<float4> bloom : register( t2 );

Texture2D<float> SceneDepth : register(t3);

SamplerState PointSampler : register (s0);
SamplerState LinearSampler : register (s1);


// The old 0.72 target compensated for the missing display gamma. With gamma
// encoding enabled, reduce exposure by roughly half a stop.
static const float  MIDDLE_GRAY = 0.5f;
static const float  LUM_WHITE = 1.5f;

cbuffer cbPS : register( b0 )
{
    float4    g_param;   
    float4 ExposureParams; // bias, minimum stops, maximum stops, bloom strength
    float4 BrightFilmic0;
    float4 BrightFilmic1;
    float4 DarkFilmic0;
    float4 DarkFilmic1;
    float4 DofPlanes;
    float4 DofParams;
    float4 DofDepth;
};


float ViewDistance(float2 uv)
{
    float depth = SceneDepth.SampleLevel(PointSampler, uv, 0);
    return abs(DofDepth.y / max(abs(depth + DofDepth.x), 1e-7));
}

float BlurAmount(float distance)
{
    float nearBlur = (DofPlanes.y - distance) / max(DofPlanes.y - DofPlanes.x, 1e-5);
    float farBlur = (distance - DofPlanes.z) / max(DofPlanes.w - DofPlanes.z, 1e-5);
    return saturate(max(nearBlur, farBlur) * DofParams.x);
}

float4 main(VS_Output input) : SV_TARGET
{
    float4 vColor = tex.Sample(PointSampler, input.Tex);
    if (DofParams.w > 0)
    {
        float centerDistance = ViewDistance(input.Tex);
        float radius = BlurAmount(centerDistance);
        if (radius > 0.01)
        {
            float3 sum = vColor.rgb;
            float weight = 1;
            [unroll] for (int i = 0; i < 12; i++)
            {
                float angle = i * 2.39996323;
                float2 offset = float2(cos(angle), sin(angle)) * sqrt((i + 1.0) / 12.0);
                float2 uv = saturate(input.Tex + offset * radius * DofParams.yz);
                float distance = ViewDistance(uv);
                // Keep focused foreground surfaces from bleeding into the blurred background.
                float tapWeight = distance >= centerDistance ? 1 : BlurAmount(distance);
                sum += tex.SampleLevel(LinearSampler, uv, 0).rgb * tapWeight;
                weight += tapWeight;
            }
            vColor.rgb = sum / weight;
        }
    }
    float fLum = min(max(lum[0]*g_param.x, 0.2), 10); //limit amplification...
    float3 vBloom = bloom.Sample(LinearSampler, input.Tex).rgb;

    if (g_param.y > 0)
    {
        float average = max(lum[0] * g_param.x, 1e-6);
        float3 mapped = WorldToneMap(vColor.rgb, average, ExposureParams,
            BrightFilmic0, BrightFilmic1, DarkFilmic0, DarkFilmic1);
        mapped += vBloom * ExposureParams.w;
        return float4(pow(saturate(mapped), 1.0 / 2.2), 1);
    }

    // Tone mapping
    vColor.rgb *= MIDDLE_GRAY / (fLum + 0.001f);
    vColor.rgb *= (1.0f + vColor.rgb/LUM_WHITE);
    vColor.rgb /= (1.0f + vColor.rgb);

    vColor.rgb += 0.6f * vBloom;
    // The swap-chain target is UNORM, so it does not encode linear light for
    // display. Match the reference postfx output gamma after tone mapping and
    // bloom; otherwise low ambient values in shadows appear almost black.
    vColor.rgb = pow(max(vColor.rgb, 0.0f), 1.0f / 2.2f);
    vColor.a = 1.0f;

    return vColor;
}
