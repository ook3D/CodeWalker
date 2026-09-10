// Reference: postfx.fx fullFilmicTonemap/getFilmicParams and exposureGPU.fx.
// Luminance is still supplied by CodeWalker's reduction/adaptation buffers.
float3 FilmicCurve(float3 x, float4 p0, float4 p1)
{
    float3 numerator = x * (p0.x * x + p0.z * p0.y) + p0.w * p1.x;
    float3 denominator = x * (p0.x * x + p0.y) + p0.w * p1.y;
    return numerator / max(denominator, 1e-6) - p1.x / max(p1.y, 1e-6);
}

float3 WorldToneMap(float3 colour, float averageLuminance, float4 exposureParams,
    float4 bright0, float4 bright1, float4 dark0, float4 dark1)
{
    // Stock visualsettings.dat exposure curve, in stops, followed by timecycle bounds.
    float stops = -106.2 * pow(max(averageLuminance, 1e-6), 0.007) + 104.7 + exposureParams.x;
    stops = clamp(stops, exposureParams.y, exposureParams.z);
    float blend = saturate((stops - bright1.w) / max(dark1.w - bright1.w, 1e-6));
    float4 p0 = lerp(bright0, dark0, blend);
    float4 p1 = lerp(bright1, dark1, blend);
    float3 white = max(FilmicCurve(p1.zzz, p0, p1), 1e-6);
    return saturate(FilmicCurve(max(colour, 0) * exp2(stops), p0, p1) / white);
}
