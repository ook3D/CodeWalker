#ifndef CODEWALKER_MATERIAL_LIGHTING
#define CODEWALKER_MATERIAL_LIGHTING

// Linear material values. The deferred targets store sqrt(intensity) and
// sqrt(exponent / 512); Fresnel is GTA's coefficient (1 - normal reflectance).
struct MaterialSpecular
{
    float Intensity;
    float Exponent;
    float Fresnel;
};

MaterialSpecular ReadSpecularMaterial(float4 sampleValue, bool hasTexture,
    float3 intensityMask, float intensityMultiplier, float exponentMultiplier, float fresnel)
{
    MaterialSpecular material;
    sampleValue.xy *= sampleValue.xy;
    material.Intensity = max(intensityMultiplier * (hasTexture ? dot(sampleValue.rgb, intensityMask) : 1.0), 0);
    material.Exponent = max(exponentMultiplier * (hasTexture ? sampleValue.a : 1.0), 0);
    material.Fresnel = fresnel;
    return material;
}

float3 EncodeSpecular(MaterialSpecular material)
{
    return float3(sqrt(max(float2(material.Intensity, material.Exponent / 512.0), 0)), material.Fresnel);
}

MaterialSpecular DecodeSpecular(float3 encoded)
{
    MaterialSpecular material;
    material.Intensity = encoded.x * encoded.x;
    material.Exponent = encoded.y * encoded.y * 512.0;
    material.Fresnel = encoded.z;
    return material;
}

float2 EncodeAmbient(float2 ambient)
{
    return sqrt(max(ambient, 0) * 0.5);
}

float2 DecodeAmbient(float2 encoded)
{
    return encoded * encoded * 2.0;
}

float3 LightingDirection(float3 direction)
{
    return direction * rsqrt(max(dot(direction, direction), 1e-12));
}

float MaterialFresnel(float coefficient, float cosine)
{
    return (1.0 - coefficient) + coefficient * pow(1.0 - saturate(cosine), 5.0);
}

float MaterialDiffuseScale(MaterialSpecular material, float3 normal, float3 eyeDirection)
{
    return 1.0 - material.Intensity * MaterialFresnel(material.Fresnel, dot(normal, eyeDirection));
}

// lighting.fxh:GetLightValues: normalized Blinn-Phong, Schlick Fresnel,
// and the projected light contribution. Shadowing/attenuation are applied by callers.
float MaterialSpecularLight(MaterialSpecular material, float3 normal, float3 lightDirection, float3 eyeDirection)
{
    float3 halfVector = LightingDirection(lightDirection + eyeDirection);
    float exponent = max(material.Exponent, 0);
    float highlight = pow(saturate(dot(normal, halfVector) + 1e-8), exponent + 1e-8);
    float fresnel = MaterialFresnel(material.Fresnel, dot(halfVector, lightDirection));
    return material.Intensity * highlight * fresnel * ((exponent + 2.0) / 8.0)
        * saturate(dot(normal, lightDirection));
}

#endif
