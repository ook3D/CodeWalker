#include "MaterialAlpha.hlsli"
#include "BasicPS.hlsli"


PS_OUTPUT main(VS_OUTPUT input)
{
    // Calculate parallax offset if height mapping is enabled
    float2 parallaxTexOffset = float2(0, 0);
    if (EnableHeightMap && RenderMode == 0)
    {
        float3 viewDir = -normalize(input.CamRelPos); // Negate to get direction FROM surface TO camera
        parallaxTexOffset = ParallaxOffset(
            Heightmap, TextureSS, input.Texcoord0,
            viewDir, normalize(input.Normal),
            normalize(input.Tangent.xyz), normalize(input.Bitangent.xyz),
            heightScale, heightBias);
    }

    // Apply parallax offset to base texture coordinates
    float2 texc0 = input.Texcoord0 + parallaxTexOffset;

    float4 c = float4(0.5, 0.5, 0.5, 1);
    if (RenderMode == 0) c = float4(1, 1, 1, 1);
    if (EnableTexture > 0)
    {
        float2 texc = texc0;
        if (RenderMode >= 5)
        {
            if (RenderSamplerCoord == 2)
                texc = input.Texcoord1 + parallaxTexOffset;
            else if (RenderSamplerCoord == 3)
                texc = input.Texcoord2 + parallaxTexOffset;
        }

        c = Colourmap.Sample(TextureSS, texc);

        if (EnableTexture > 1) //2+ enables diffuse2
        {
            float4 c2 = Colourmap2.Sample(TextureSS, input.Texcoord1);
            c = c2.a * c2 + (1 - c2.a) * c;
        }
        if (EnableTint == 2)
        {
            //weapon tint
            float tx = (round(c.a * 255.009995) - 32.0) * 0.007813; //okay R* this is just silly
            float ty = 0.03125 * 0.5; // //1;//what to use for Y value? cb12[2].w in R* shader
            float4 c3 = TintPalette.Sample(TextureSS, float2(tx, ty));
            c.rgb *= c3.rgb;
            c.a = 1;
        }

        if (IsDistMap) c = float4(c.rgb * 2, (c.r + c.g + c.b) - 1);
        if (IsDecal == 4) c.a = c.r;
        c.a = HairFlags.w != 0 ? 1 : HairCoverage(c.a, texc0);
        if (AlphaMode == 3) c.a = 1;
        if (AlphaMode == 4) c.a = MaterialAlphaCoverage(c.a, HardAlphaBlend);
        if (AlphaMode == 1) ClipMaterialCoverage(c.a * AlphaScale, HardAlphaBlend);
        if ((AlphaMode == 0) && (IsDecal == 0) && (c.a <= 0.33)) discard;
        if ((IsDecal == 1) && (c.a <= 0.0)) discard;
        if ((IsDecal >= 3) && (c.a <= 0.0)) discard;
        if ((IsDecal == 0) && (AlphaMode != 2) && (AlphaMode != 4)) c.a = 1;
        if (IsDecal == 2)
        {
            float4 mask = TextureAlphaMask * c;
            c.a = saturate(mask.r + mask.g + mask.b + mask.a);
            c.rgb = 0;
        }
        c.a = saturate(c.a * AlphaScale);
    }
    if (EnableTint == 1)
    {
        c.rgb *= input.Tint.rgb;
    }
    if ((IsDecal == 1) || (IsDecal >= 3))
    {
        c.a *= input.Colour0.a;
    }

    float3 norm = normalize(input.Normal);

    if (RenderMode == 1) //normals
    {
        c.rgb = norm * 0.5 + 0.5;
    }
    else if (RenderMode == 2) //tangents
    {
        c.rgb = normalize(input.Tangent.rgb) * 0.5 + 0.5;
    }
    else if (RenderMode == 3) //colours
    {
        c.rgb = input.Colour0.rgb;
        if (RenderModeIndex == 2)
            c.rgb = input.Colour1.rgb;
    }
    else if (RenderMode == 4) //texcoords
    {
        c.rgb = float3(input.Texcoord0, 0);
        if (RenderModeIndex == 2) c.rgb = float3(input.Texcoord1, 0);
        if (RenderModeIndex == 3) c.rgb = float3(input.Texcoord2, 0);
    }


    float3 spec = 0;

    if (RenderMode == 0)
    {

        MaterialSpecular material;
        float normalAlpha;
        SampleBasicMaterial(input, texc0, norm, material, normalAlpha);
        float3 hairColour = ApplyHairMaterial(input, texc0, material);
        if (HairFlags.x != 0) c.rgb = sqrt(max(c.rgb * c.rgb + hairColour, 0));
        spec = EncodeSpecular(material);

    }


    // emissiveIntensity *= surface_baseColor.b
    // Vertex COLOR0.b modulates the emissive intensity
    float emiss = (IsEmissive == 1) ? input.Colour0.b : 0.0;

    c.a = (AlphaMode == 3) ? 1.0 : saturate(c.a);
    
    
    float4 a = c.aaaa;
    if(IsDecal==3) a.xzw = 0; //normal_only
    if(IsDecal==4) a.xyw = 0; //spec_only
    
    
    PS_OUTPUT output;
    output.Diffuse = float4(c.rgb, a.x);
    output.Normal = float4(saturate(norm * 0.5 + 0.5), a.y);
    output.Specular = float4(spec, a.z);
    float2 irr = EncodeAmbient(input.Colour0.rg);
    output.Irradiance = float4(irr, (saturate(emiss) + 2 * InteriorFlags.x) / 3, a.w);

    return output;
}



