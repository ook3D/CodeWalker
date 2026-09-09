using CodeWalker.GameFiles;
using System.Collections.Generic;
using System.Linq;

namespace CodeWalker.Rendering;

public static class PedMaterial
{
    // Detailed ped variants enable SPEC_MAP_INTFALLOFF_PACK in ped_common.fxh.
    // Basic/LOD peds and unrelated skinned materials must retain their own decoding.
    private static readonly HashSet<uint> PackedSpecularShaders = new string[]
    {
        "ped.sps",
        "ped_alpha.sps",
        "ped_cloth.sps",
        "ped_cloth_enveff.sps",
        "ped_decal_decoration.sps",
        "ped_decal_exp.sps",
        "ped_decal_medals.sps",
        "ped_decal_nodiff.sps",
        "ped_emissive.sps",
        "ped_enveff.sps",
        "ped_fur.sps",
        "ped_hair_cutout_alpha.sps",
        "ped_hair_cutout_alpha_cloth.sps",
        "ped_hair_spiked.sps",
        "ped_hair_spiked_mask.sps",
        "ped_nopeddamagedecals.sps",
        "ped_palette.sps",
        "ped_wrinkle.sps",
        "ped_wrinkle_cloth.sps",
        "ped_wrinkle_cloth_enveff.sps",
        "ped_wrinkle_cs.sps",
        "ped_wrinkle_enveff.sps",
    }.Select(JenkHash.GenHash).ToHashSet();

    private static readonly HashSet<uint> HairShaders = new string[]
    {
        "ped_hair_cutout_alpha.sps", "ped_hair_cutout_alpha_cloth.sps",
        "ped_hair_spiked.sps", "ped_hair_spiked_mask.sps", "ped_hair_spiked_enveff.sps"
    }.Select(JenkHash.GenHash).ToHashSet();

    // ShaderHairSort selects the dedicated proxy when present, otherwise strands.
    public static bool CastsHairShadow(int order, bool hasProxy) => order == (hasProxy ? 8 : 0);

    public static bool UsesAnisotropicHair(uint shaderFileHash) => HairShaders.Contains(shaderFileHash);

    public static bool UsesPackedSpecular(uint shaderFileHash) => PackedSpecularShaders.Contains(shaderFileHash);
}
