#include "Common.hlsli"


struct VS_INPUT
{
    float4 Position : POSITION;
    float2 Texcoord : TEXCOORD0;
};
struct VS_OUTPUT
{
    float4 Position : SV_POSITION;
    float2 Texcoord : TEXCOORD0;
    float4 Colour   : COLOR0;
};

struct ParticleInstance
{
    float3 Position;
    float Pad0;
    float3 Right; // world-space half-extent axes, built on the CPU (ptxDrawInterface::BatchSprite)
    float Pad1;
    float3 Up;
    float Pad2;
    float4 UVRect;
    float4 Colour;
};
StructuredBuffer<ParticleInstance> ParticleInstances : register(t0);

cbuffer VSSceneVars : register(b0)
{
    float4x4 ViewProj;
    float4x4 ViewInv;
    float3 CamPos;
    float Pad0;
};


VS_OUTPUT main(VS_INPUT input, uint iid : SV_InstanceID)
{
    VS_OUTPUT output;

    ParticleInstance p = ParticleInstances[iid];

    // expand the unit quad corner along the sprite's world axes (camera/velocity/axis alignment is done on the CPU)
    float2 q = input.Position.xy;
    float3 wpos = (p.Position - CamPos) + q.x * p.Right + q.y * p.Up;

    output.Position = mul(float4(wpos, 1.0), ViewProj);
    output.Texcoord = lerp(p.UVRect.xy, p.UVRect.zw, input.Texcoord);
    output.Colour = p.Colour;

    return output;
}
