#include "GrassFur.hlsli"

PS_OUTPUT_DEFERRED main(VS_OUTPUT input)
{
    float4 albo = GetFurAlbedo(input.Texcoord0, input.Texcoord1, input.Texcoord2, input.Colour0);
    float3 bita = GetFurBitangent(input.Normal, input.Tangent0);
    float4 norm = GetFurNormal(input.Texcoord0, input.Normal, input.Tangent0.xyz, bita);

    PS_OUTPUT_DEFERRED output;
    output.Diffuse = float4(albo.rgb, albo.a);
    output.Normal = float4(saturate(norm.xyz * 0.5 + 0.5), albo.a);
    output.Specular = float4(0, 0, 1, albo.a);
    float2 grassIrr = EncodeAmbient(input.Colour0.rg);
    output.Irradiance = float4(grassIrr, 0, albo.a);
    return output;
}
