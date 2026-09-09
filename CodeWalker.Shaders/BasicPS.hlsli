#include "Shadowmap.hlsli"

Texture2D<float4> Colourmap : register(t0);
Texture2D<float4> Bumpmap : register(t2);
Texture2D<float4> Specmap : register(t3);
Texture2D<float4> Detailmap : register(t4);
Texture2D<float4> Colourmap2 : register(t5);
Texture2D<float4> TintPalette : register(t6);
Texture2D<float4> Heightmap : register(t7);
Texture2D<float4> HairNoise : register(t8);
SamplerState TextureSS : register(s0);


cbuffer PSSceneVars : register(b0)
{
    ShaderGlobalLightParams GlobalLights;
    uint EnableShadows;
    uint RenderMode;//0=default, 1=normals, 2=tangents, 3=colours, 4=texcoords, 5=diffuse, 6=normalmap, 7=spec, 8=direct
    uint RenderModeIndex;
    uint RenderSamplerCoord;
    float4 InteriorAmbientUp;
    float4 InteriorAmbientDown;
    float4 HairViewDirection;
}
cbuffer PSGeomVars : register(b2)
{
    uint EnableTexture;//1+=diffuse1, 2+=diffuse2
    uint EnableTint;//1=default, 2=weapons (use diffuse.a for tint lookup)
    uint EnableNormalMap;
    uint EnableSpecMap;
    uint EnableDetailMap;
    uint IsDecal;
    uint IsEmissive;
    uint IsDistMap;
    float bumpiness;
    float AlphaScale;
    float HardAlphaBlend;
    uint AlphaMode; // 0 = legacy, 1 = cutout, 2 = alpha cloth, 3 = opaque, 4 = alpha fence
    float4 detailSettings;
    float3 specMapIntMask;
    float specularIntensityMult;
    float specularFalloffMult;
    float specularFresnel;
    float wetnessMultiplier;
    uint SpecOnly;
	float4 TextureAlphaMask;
    uint EnableHeightMap;
    float heightScale;
    float heightBias;
    uint UsePedSpecular;
    float4 InteriorFlags;
    float4 HairFlags;
    float4 HairSpecular;
    float4 HairColour;
    float4 HairNoiseUV;
}


struct VS_OUTPUT
{
    float4 Position  : SV_POSITION;
    float3 Normal    : NORMAL;
    float2 Texcoord0 : TEXCOORD0;
    float2 Texcoord1 : TEXCOORD1;
    float2 Texcoord2 : TEXCOORD2;
    float4 Shadows   : TEXCOORD3;
    float4 LightShadow : TEXCOORD4;
    float4 Colour0   : COLOR0;
    float4 Colour1   : COLOR1;
    float4 Tint      : COLOR2;
    float4 Tangent   : TEXCOORD5;
    float4 Bitangent : TEXCOORD6;
    float3 CamRelPos : TEXCOORD7;
};

struct PS_OUTPUT
{
    float4 Diffuse : SV_Target0;
    float4 Normal : SV_Target1;
    float4 Specular : SV_Target2;
    float4 Irradiance : SV_Target3;
};

// Hair noise modulates coverage independently of its specular-map blue mask.
float HairCoverage(float alpha, float2 uv)
{
    if (HairFlags.x == 0 || HairFlags.y == 0) return alpha;
    float noiseAlpha = HairNoise.Sample(TextureSS, uv * HairNoiseUV.xy * 0.5).a;
    return alpha * saturate(noiseAlpha * 2 + HairFlags.z);
}

// ped_common.fxh: two shifted strand lobes, enabled by the specular blue mask.
// Apply before encoding the G-buffer so forward and deferred agree.
float3 ApplyHairMaterial(VS_OUTPUT input, float2 uv, inout MaterialSpecular material)
{
    if (HairFlags.x == 0 || HairFlags.y == 0 || EnableSpecMap == 0) return 0;
    if (Specmap.Sample(TextureSS, uv).b >= 1.0 / 32.0) return 0;
    float4 noise0 = HairNoise.Sample(TextureSS, uv * HairNoiseUV.xy * 0.5);
    float2 shifts = (HairNoise.Sample(TextureSS, uv * HairNoiseUV.zw * 0.5).rg * 2 - 1) * 0.1;
    float3 normal = LightingDirection(input.Normal);
    float3 binormal = LightingDirection(input.Bitangent.xyz);
    float3 light = LightingDirection(-input.CamRelPos);
    float3 view = LightingDirection(HairViewDirection.xyz);
    float3 strand0 = LightingDirection(binormal + normal * shifts.x);
    float3 strand1 = LightingDirection(binormal + normal * shifts.y);
    float2 lt = clamp(float2(dot(light, strand0), dot(light, strand1)), -1, 1);
    float2 vt = clamp(float2(dot(view, strand0), dot(view, strand1)), -1, 1);
    float2 lobes = abs(sqrt(saturate(1 - lt * lt)) * sqrt(saturate(1 - vt * vt)) - lt * vt);
    lobes = pow(saturate(lobes), max(HairSpecular.xy + float2(8, 16), 0));
    material.Intensity *= dot(lobes, HairSpecular.zw) * noise0.r;
    return HairColour.rgb * lobes.y * 0.5;
}

// Shared by the forward and deferred paths. Keep the original specular alpha
// for detail normals: only R/G are squared when decoding the specular material.
void SampleBasicMaterial(VS_OUTPUT input, float2 texcoord, out float3 normal,
    out MaterialSpecular material, out float normalAlpha)
{
    float4 normalSample = Bumpmap.Sample(TextureSS, texcoord);
    float4 specularSample = Specmap.Sample(TextureSS, texcoord);
    normalAlpha = normalSample.a;
    normal = normalize(input.Normal);
    if (EnableNormalMap)
    {
        float2 normalXY = normalSample.xy;
        if (EnableDetailMap)
        {
            float2 detailUV = texcoord * detailSettings.zw;
            float2 detail = Detailmap.Sample(TextureSS, detailUV).xy - 0.5;
            detail += Detailmap.Sample(TextureSS, detailUV * 3.17).xy - 0.5;
            normalXY += detail * detailSettings.y * specularSample.a;
        }
        normal = NormalMap(normalXY, bumpiness, input.Normal, input.Tangent.xyz, input.Bitangent.xyz);
    }

    material = ReadSpecularMaterial(specularSample, EnableSpecMap != 0,
        specMapIntMask, specularIntensityMult, specularFalloffMult, specularFresnel);
    if (UsePedSpecular != 0 && EnableSpecMap != 0)
    {
        // Ped R/G encode intensity/exponent; alpha is a detail mask, not shininess.
        material.Intensity = max(specularSample.r * specularSample.r * specularIntensityMult, 0);
        material.Exponent = max(specularSample.g * specularSample.g * specularFalloffMult, 0);
    }
}




ShaderGlobalLightParams BasicMaterialLights()
{
    ShaderGlobalLightParams lights = GlobalLights;
    if (InteriorFlags.x > 0)
    {
        lights.LightArtificialAmbUp = InteriorAmbientUp;
        lights.LightArtificialAmbDown = InteriorAmbientDown;
    }
    return lights;
}
