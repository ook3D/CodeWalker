
#include "WorldPostFx.hlsli"

struct VS_Output
{
    float4 Pos : SV_POSITION;              
    float2 Tex : TEXCOORD0;
};


Texture2D<float4> tex : register( t0 );
StructuredBuffer<float> lum : register( t1 );
Texture2D<float4> bloom : register( t2 );

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
};


float4 main(VS_Output input) : SV_TARGET
{
    float4 vColor = tex.Sample(PointSampler, input.Tex);
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
