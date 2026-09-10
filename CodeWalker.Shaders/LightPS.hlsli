#include "Shadowmap.hlsli"


struct PS_OUTPUT
{
    float4 Colour : SV_TARGET;
    float Depth : SV_DEPTH;
};

cbuffer PSLightVars : register(b0)
{
    ShaderGlobalLightParams GlobalLights;
    float4x4 ViewProjInv;
    float4 CameraPos;
    uint EnableShadows;
    uint RenderMode; //0=default, 1=normals, 2=tangents, 3=colours, 4=texcoords, 5=diffuse, 6=normalmap, 7=spec, 8=direct
    uint RenderModeIndex;
    uint RenderSamplerCoord;
    uint LightType; //0=directional, 1=Point, 2=Spot, 4=Capsule
    uint IsLOD; //useful or not?
    uint SampleCount;//for MSAA
    float SampleMult;//for MSAA
    float4 InteriorAmbientUp;
    float4 InteriorAmbientDown;
}

cbuffer PSLightInstVars : register(b2)
{
    float3 InstPosition;//camera relative
    float InstIntensity;
    float3 InstColour;
    float InstFalloff;
    float3 InstDirection;
    float InstFalloffExponent;
    float3 InstTangentX;
    float InstConeInnerAngle;
    float3 InstTangentY;
    float InstConeOuterAngle;
    float3 InstCapsuleExtent;
    uint InstType;
    float3 InstCullingPlaneNormal;
    float InstCullingPlaneOffset;
    uint InstCullingPlaneEnable;
    uint InstUnused1;
    uint InstUnused2;
    uint InstUnused3;
}



struct LODLight
{
    float3 Position;
    uint Colour;
    float3 Direction;
    uint TimeAndStateFlags;
    float4 TangentX;
    float4 TangentY;
    float Falloff;
    float FalloffExponent;
    float InnerAngle; //for cone
    float OuterAngleOrCapExt; //outer angle for cone, cap extent for capsule
};

StructuredBuffer<LODLight> LODLights : register(t6);




float3 GetReflectedDir(float3 camRel, float3 norm)
{
    float3 incident = normalize(camRel);
    float3 refl = normalize(reflect(incident, norm));
    return refl;
}

float4 GetLineSegmentNearestPoint(float3 v, float3 a, float3 b)
{
    float3 ab = b - a;
    float t = saturate(dot(v - a, ab) / max(dot(ab, ab), 1e-12));
    float3 offset = v - (a + ab * t);
    return float4(offset, length(offset));
}

float __powapprox(float a, float b)
{
    return a / max((1.0 - b) * a + b, 1e-12);
}

float GetAttenuation(float ldist, float falloff, float falloffExponent)
{
    if (falloff <= 0) return 0;
    float distSqr = ldist * ldist;
    float invSqrFalloff = 1.0 / (falloff * falloff);
    float t = saturate(1.0 - distSqr * invSqrFalloff);
    return __powapprox(t, max(falloffExponent, 0));
}

float GetSpotAngularAttenuation(float3 surfaceToLightDir, float3 lightDirection,
                                float cosInnerAngle, float cosOuterAngle)
{
    float cosAngle = dot(surfaceToLightDir, -lightDirection);
    float spotScale = 1.0 / max(cosInnerAngle - cosOuterAngle, 0.000001);
    float spotOffset = -cosOuterAngle * spotScale;
    return saturate(cosAngle * spotScale + spotOffset);
}


float3 DeferredDirectionalLight(float3 camRel, float3 norm, float4 diffuse, float4 specular, float4 irradiance)
{
    diffuse.rgb = MaterialDiffuseColour(diffuse.rgb);
    norm = LightingDirection(norm);
    MaterialSpecular material = DecodeSpecular(specular.rgb);
    float3 viewDir = LightingDirection(-camRel);
    float3 spec = GlobalLights.LightDirColour.rgb
        * MaterialSpecularLight(material, norm, GlobalLights.LightDir, viewDir);
    float diffuseScale = MaterialDiffuseScale(material, norm, viewDir);
    float4 ambient = float4(DecodeAmbient(irradiance.rg), 0, 0);
    float4 lightspacepos;
    float shadowdepth = ShadowmapSceneDepth(camRel, lightspacepos);
    bool interior = irradiance.b > 0.5;
    ShaderGlobalLightParams materialLights = GlobalLights;
    if (interior)
    {
        materialLights.LightArtificialAmbUp = InteriorAmbientUp;
        materialLights.LightArtificialAmbDown = InteriorAmbientDown;
    }
    float3 c = FullLighting(diffuse.rgb * diffuseScale, spec, norm, ambient, materialLights, EnableShadows, shadowdepth, lightspacepos);
    c += diffuse.rgb * saturate(irradiance.b * 3 - (interior ? 2 : 0)); //emissive multiplier
    return c;
}

float4 DeferredLODLight(float3 camRel, float3 norm, float4 diffuse, float4 specular, float4 irradiance, uint iid)
{
    diffuse.rgb = MaterialDiffuseColour(diffuse.rgb);
    norm = LightingDirection(norm);
    LODLight lodlight = LODLights[iid];
    float3 srpos = lodlight.Position - (camRel + CameraPos.xyz); //light position relative to surface position
    float ldist = length(srpos);
    if (LightType == 4)//capsule
    {
        float3 ext = lodlight.Direction.xyz * lodlight.OuterAngleOrCapExt;
        float4 lsn = GetLineSegmentNearestPoint(srpos, ext, -ext);
        ldist = lsn.w;
        srpos.xyz = lsn.xyz;
    }

    if (ldist > lodlight.Falloff) return 0; //out of range of the light...
    if (ldist <= 0) return 0;
    
    float4 rgbi = Unpack4x8UNF(lodlight.Colour).gbar;
    float3 lcol = rgbi.rgb * rgbi.a * 96.0f;
    float3 ldir = srpos / ldist;
    float pclit = saturate(dot(ldir, norm));
    float lamt = 1;
    
    if (LightType == 1)//point (sphere)
    {
        lamt *= GetAttenuation(ldist, lodlight.Falloff, lodlight.FalloffExponent);
    }
    else if (LightType == 2)//spot (cone)
    {
        float cosInner = cos(lodlight.InnerAngle);
        float cosOuter = cos(lodlight.OuterAngleOrCapExt);
        lamt *= GetSpotAngularAttenuation(ldir, lodlight.Direction, cosInner, cosOuter);
        lamt *= GetAttenuation(ldist, lodlight.Falloff, lodlight.FalloffExponent);
    }
    else if (LightType == 4)//capsule
    {
        lamt *= GetAttenuation(ldist, lodlight.Falloff, lodlight.FalloffExponent);
    }

    pclit *= lamt;

    if (pclit <= 0) return 0;

    MaterialSpecular material = DecodeSpecular(specular.rgb);
    float3 viewDir = LightingDirection(-camRel);
    float3 spec = lcol * lamt * MaterialSpecularLight(material, norm, ldir, viewDir);
    float diffuseScale = MaterialDiffuseScale(material, norm, viewDir);
    lcol = lcol * diffuse.rgb * diffuseScale * pclit + spec;

    return float4(lcol, 1);
}

float4 DeferredLight(float3 camRel, float3 norm, float4 diffuse, float4 specular, float4 irradiance)
{
    diffuse.rgb = MaterialDiffuseColour(diffuse.rgb);
    norm = LightingDirection(norm);
    float3 srpos = InstPosition - camRel; //light position relative to surface position
    float ldist = length(srpos);
    if (InstCullingPlaneEnable == 1)
    {
        float d = dot(srpos, InstCullingPlaneNormal) - InstCullingPlaneOffset;
        if (d > 0) return 0;
    }
    if (InstType == 4)//capsule
    {
        float3 ext = InstDirection.xyz * (InstCapsuleExtent.x * 0.5);
        float4 lsn = GetLineSegmentNearestPoint(srpos, ext, -ext);
        ldist = lsn.w;
        srpos.xyz = lsn.xyz;
    }
    if (ldist > InstFalloff) return 0;
    if (ldist <= 0) return 0;
    float4 rgbi = float4(InstColour, InstIntensity);
    float3 lcol = rgbi.rgb;// * rgbi.a; // * 5.0f;
    float3 ldir = srpos / ldist;
    float pclit = saturate(dot(ldir, norm));
    float lamt = 1;
    
    if (InstType == 1)//point (sphere)
    {
        lamt *= GetAttenuation(ldist, InstFalloff, InstFalloffExponent);
    }
    else if (InstType == 2)//spot (cone)
    {
        float cosInner = cos(InstConeInnerAngle);
        float cosOuter = cos(InstConeOuterAngle);
        lamt *= GetSpotAngularAttenuation(ldir, InstDirection, cosInner, cosOuter);
        lamt *= GetAttenuation(ldist, InstFalloff, InstFalloffExponent);
    }
    else if (InstType == 4)//capsule
    {
        lamt *= GetAttenuation(ldist, InstFalloff, InstFalloffExponent);
    }

    pclit *= lamt;

    if (pclit <= 0) return 0;

    MaterialSpecular material = DecodeSpecular(specular.rgb);
    float3 viewDir = LightingDirection(-camRel);
    float3 spec = lcol * lamt * MaterialSpecularLight(material, norm, ldir, viewDir);
    float diffuseScale = MaterialDiffuseScale(material, norm, viewDir);
    lcol = lcol * diffuse.rgb * diffuseScale * pclit + spec;

    return float4(lcol, 1);
}





