// PPVHS.hlsl

Texture2D inputTexture : register(t0);
SamplerState samplerState : register(s0);

cbuffer TimeBuffer : register(b0)
{
    float time;
}

struct VS_OUTPUT
{
    float4 Pos : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

float4 main(VS_OUTPUT input) : SV_TARGET
{
    float2 uv = input.TexCoord;
    
    // Simulate VHS color distortion
    float2 offset = float2(sin(uv.y * 10.0 + time * 5.0) * time * 0.2, time * -0.2);
    float4 colorR = inputTexture.Sample(samplerState, uv + offset);
    float4 colorG = inputTexture.Sample(samplerState, uv);
    float4 colorB = inputTexture.Sample(samplerState, uv - offset);

    float4 color = float4(colorR.r, colorG.g, colorB.b, 1.0);
    
    // Simulate VHS noise
    uv.y += time * 10;
    float noise = (frac(sin(dot(uv.xy, float2(12.9898, 78.233))) * 43758.5453) - 0.5) * 0.1;
    color.rgb += noise;

    // Simulate VHS scanlines
    float scanline = sin(uv.y * 400.0) * 0.04;
    color.rgb += scanline;
    
    return color;
}
