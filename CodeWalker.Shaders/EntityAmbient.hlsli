// ShaderLib::SetGlobals supplies normalized entity scales. Multiply the baked
// vertex channels before lighting squares them or the G-buffer encodes them.
cbuffer EntityAmbientVars : register(b10)
{
    float2 EntityAmbientScale;
    float2 EntityAmbientPadding;
}

float4 ApplyEntityAmbient(float4 colour)
{
    colour.rg *= EntityAmbientScale;
    return colour;
}
