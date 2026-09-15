using CodeWalker.World;
using Xunit;

namespace CodeWalker.Core.Tests;

public class WeaponCatalogTests
{
    [Fact]
    public void ReadsAttachmentPointsVariantsAndDlcOverrides()
    {
        var catalog = new WeaponCatalog();
        catalog.AddXml("""
            <CWeaponInfoBlob>
              <TintSpecValues><Item><Name>TINT_TEST</Name><Tints>
                <Item><SpecFresnel value="0.75" /><SpecIntMult value="1.25" /><SpecFalloffMult value="40" />
                  <Spec2Factor value="80" /><Spec2ColorInt value="2" /><Spec2Color value="0x00000000ff8000" />
                </Item>
                <Item><SpecFresnel value="0.8" /><SpecIntMult value="2" /><SpecFalloffMult value="80" /></Item>
              </Tints></Item></TintSpecValues>
              <Infos><Item><Infos>
              <Item type="CWeaponInfo"><Name>WEAPON_TEST</Name><Model>w_test</Model>
                <TintSpecValues ref="TINT_TEST" />
                <AttachPoints><Item><AttachBone>WAPClip</AttachBone><Components>
                  <Item><Name>COMPONENT_CLIP</Name><Default value="true" /></Item>
                  <Item><Name>COMPONENT_CLIP_EXT</Name><Default value="false" /></Item>
                </Components></Item></AttachPoints>
              </Item>
              <Item type="CWeaponInfo"><Name>WEAPON_UNARMED</Name><Model /></Item>
              <Item type="CAmmoInfo"><Name>AMMO_TEST</Name><Model>ammo</Model></Item>
            </Infos></Item></Infos></CWeaponInfoBlob>
            """);
        catalog.AddXml("""
            <CWeaponComponentInfoBlob><Infos>
              <Item type="CWeaponComponentClipInfo"><Name>COMPONENT_CLIP</Name><Model>mag</Model><AttachBone>AAPClip</AttachBone></Item>
              <Item type="CWeaponComponentVariantModelInfo"><Name>COMPONENT_FINISH</Name><Model>gold</Model><TintIndexOverride value="2" />
                <ApplyWeaponTint value="false" />
                <ExtraComponents><Item><ComponentName>COMPONENT_CLIP</ComponentName><ComponentModel>mag_gold</ComponentModel></Item></ExtraComponents>
              </Item>
            </Infos></CWeaponComponentInfoBlob>
            """);
        var weapon = Assert.Single(catalog.Weapons).Value;
        Assert.Equal("w_test", weapon.Model);
        Assert.Equal("TINT_TEST", weapon.TintSet);
        Assert.Equal(2, catalog.TintSets[weapon.TintSet].Length);
        Assert.Equal(1.25f, catalog.TintSets[weapon.TintSet][0].SpecularIntensity);
        Assert.Equal(80, catalog.TintSets[weapon.TintSet][0].SecondaryFalloff);
        Assert.Equal(2, catalog.TintSets[weapon.TintSet][0].SecondaryIntensity);
        Assert.Equal(new SharpDX.Vector3(1, 128f / 255f, 0), catalog.TintSets[weapon.TintSet][0].SecondaryColour);
        Assert.Equal(2, weapon.Attachments.Count);
        Assert.Equal("WAPClip", weapon.Attachments[0].Bone);
        Assert.True(weapon.Attachments[0].Default);
        Assert.False(weapon.Attachments[1].Default);
        Assert.Equal("AAPClip", catalog.Components["component_clip"].Bone);
        Assert.False(catalog.Components["component_clip"].ApplyTint);
        var finish = catalog.Components["COMPONENT_FINISH"];
        Assert.True(finish.Variant);
        Assert.Equal(2, finish.TintOverride);
        Assert.False(finish.ApplyTint);
        Assert.Equal("mag_gold", finish.ExtraModels["COMPONENT_CLIP"]);

        catalog.AddXml("""
            <Infos><Item type="CWeaponInfo"><Name>weapon_test</Name><Model>w_test_dlc</Model></Item></Infos>
            """);
        Assert.Equal("w_test_dlc", Assert.Single(catalog.Weapons).Value.Model);
        Assert.Empty(catalog.Weapons["WEAPON_TEST"].Attachments);
    }

    [Fact]
    public void CollectsWeaponAnimationSetsWithoutIncludingPedMotionSets()
    {
        var catalog = new WeaponCatalog();
        catalog.AddXml("""
            <CWeaponAnimationsSets><WeaponAnimationsSets>
              <Item key="Default"><WeaponAnimations><Item key="WEAPON_TEST">
                <WeaponClipSetHash>weapons@test@</WeaponClipSetHash>
                <WeaponClipSetStreamedHash>weapons@test@reload</WeaponClipSetStreamedHash>
                <MotionClipSetHash>move_ped</MotionClipSetHash>
                <WeaponClipSetHashInjured />
                <ScopeWeaponClipSet>NULL</ScopeWeaponClipSet>
              </Item></WeaponAnimations></Item>
              <Item key="Alternative"><WeaponAnimations><Item key="WEAPON_TEST">
                <WeaponClipSetHash>weapons@test@</WeaponClipSetHash>
                <CoverWeaponClipSetHash>weapons@test@cover</CoverWeaponClipSetHash>
              </Item></WeaponAnimations></Item>
            </WeaponAnimationsSets></CWeaponAnimationsSets>
            """);
        Assert.Empty(catalog.Weapons);
        var sets = Assert.Single(catalog.AnimationSets).Value;
        Assert.Equal(3, sets.Count);
        Assert.Contains("weapons@test@reload", sets);
        Assert.Contains("weapons@test@cover", sets);
        Assert.DoesNotContain("move_ped", sets);
    }
}
