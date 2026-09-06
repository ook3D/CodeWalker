#ifndef CODEWALKER_MATERIAL_ALPHA
#define CODEWALKER_MATERIAL_ALPHA

float MaterialAlphaCoverage(float alpha, float hardAlphaBlend)
{
    // megashader.fxh: interpolate regular alpha and a narrow hard-cutout ramp.
    float hardAlpha = (alpha - 0.5) / 0.05;
    return saturate(lerp(alpha, hardAlpha, saturate(hardAlphaBlend)));
}

void ClipMaterialCoverage(float alpha, float hardAlphaBlend)
{
    // Binary cutouts and shadow maps use a fixed threshold. Smooth fences
    // blend the returned coverage in the forward pass, with no screen pattern.
    clip(MaterialAlphaCoverage(alpha, hardAlphaBlend) - 0.5);
}
#endif
