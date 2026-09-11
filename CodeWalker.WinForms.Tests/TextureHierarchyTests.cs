using System.Reflection;
using CodeWalker.GameFiles;
using CodeWalker.Rendering;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class TextureHierarchyTests
{
    [Fact]
    public void TextureHierarchyRecoversAfterCpuCacheRejectsInitialRequest()
    {
        var cache = new GameFileCache(0, 10, "", false, "", false, "") { IsInited = true };
        const uint sd = 101, hd = 102;
        cache.YtdDict[sd] = new RpfResourceFileEntry { Name = "sd.ytd", ShortNameHash = sd };
        cache.YtdDict[hd] = new RpfResourceFileEntry { Name = "hd.ytd", ShortNameHash = hd };
        GetLookup<MetaHash>(cache, "hdtexturelookup")[sd] = hd;
        var renderer = new Renderer(null!, cache) { renderhdtextures = true };
        var drawable = new gtaDrawable();
        var archetype = new Archetype { TextureDict = sd };
        var renderable = Resolve(renderer, archetype, drawable);
        Assert.False(renderable.SDtxds![0].LoadQueued);
        Assert.Equal(0u, renderable.SDtxds[0].Key.Hash);
        renderable.AllModels = [new RenderableModel
        {
            Mask = 1,
            Geometries = [new RenderableGeometry
            {
                Textures = [new TextureBase { NameHash = 7 }],
                TexturesHD = new Texture[1],
                RenderableTextures = new RenderableTexture[1],
                RenderableTexturesHD = new RenderableTexture[1],
            }],
        }];
        var cpuCache = (CodeWalker.Cache<GameFileCacheKey, GameFile>)typeof(GameFileCache)
            .GetField("mainCache", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache)!;
        cpuCache.MaxMemoryUsage = 1024 * 1024;

        Resolve(renderer, archetype, drawable);

        Assert.Same(cache.GetYtd(sd), renderable.SDtxds[0]);
        Assert.Same(cache.GetYtd(hd), renderable.HDtxds![0]);
        Assert.True(renderable.SDtxds[0].LoadQueued);
        Assert.True(renderable.HDtxds[0].LoadQueued);
        Assert.False(renderable.AllTexturesLoaded);

        var sdTexture = new Texture { NameHash = 7 };
        var hdTexture = new Texture { NameHash = 7 };
        renderable.SDtxds[0].TextureDict = new TextureDictionary();
        renderable.SDtxds[0].TextureDict!.BuildFromTextureList([sdTexture]);
        renderable.SDtxds[0].Loaded = true;
        renderable.HDtxds[0].TextureDict = new TextureDictionary();
        renderable.HDtxds[0].TextureDict!.BuildFromTextureList([hdTexture]);
        renderable.HDtxds[0].Loaded = true;
        Resolve(renderer, archetype, drawable);
        Assert.True(renderable.AllTexturesLoaded);
        var geometry = renderable.AllModels[0].Geometries[0];
        Assert.Same(sdTexture, geometry.RenderableTextures[0]!.Key);
        Assert.Same(hdTexture, geometry.RenderableTexturesHD[0]!.Key);
    }

    [Fact]
    public void RendererResolvesExternalAndParentTextureDictionaries()
    {
        var cache = new GameFileCache(1024 * 1024, 10, "", false, "", false, "") { IsInited = true };
        const uint sd = 101, parent = 102, hd = 103;
        foreach (uint hash in new[] { sd, parent, hd })
        {
            cache.YtdDict[hash] = new RpfResourceFileEntry { Name = $"{hash}.ytd", ShortNameHash = hash };
        }
        GetLookup<MetaHash>(cache, "textureParents")[sd] = parent;
        GetLookup<MetaHash>(cache, "hdtexturelookup")[sd] = hd;
        var renderer = new Renderer(null!, cache) { renderhdtextures = false };
        var drawable = new gtaDrawable();
        var archetype = new Archetype { TextureDict = sd };

        var renderable = Resolve(renderer, archetype, drawable);

        Assert.Equal(new[] { cache.GetYtd(sd), cache.GetYtd(parent) }, renderable.SDtxds);
        Assert.Null(renderable.HDtxds);

        renderer.renderhdtextures = true;
        var sdHierarchy = renderable.SDtxds;
        Assert.Same(renderable, Resolve(renderer, archetype, drawable));
        Assert.Same(sdHierarchy, renderable.SDtxds);
        Assert.Equal(new[] { cache.GetYtd(hd) }, renderable.HDtxds);
    }

    [Fact]
    public void ResolvedEmptyHierarchyIsCached()
    {
        var cache = new GameFileCache(1024 * 1024, 10, "", false, "", false, "") { IsInited = true };
        var renderer = new Renderer(null!, cache);
        var drawable = new gtaDrawable();
        var archetype = new Archetype();

        var renderable = Resolve(renderer, archetype, drawable);

        Assert.NotNull(renderable.SDtxds);
        Assert.NotNull(renderable.HDtxds);
        Assert.Empty(renderable.SDtxds);
        Assert.Empty(renderable.HDtxds);
        var sd = renderable.SDtxds;
        var hd = renderable.HDtxds;
        Resolve(renderer, archetype, drawable);
        Assert.Same(sd, renderable.SDtxds);
        Assert.Same(hd, renderable.HDtxds);
    }

    private static Dictionary<T, T> GetLookup<T>(GameFileCache cache, string name) where T : notnull =>
        (Dictionary<T, T>)typeof(GameFileCache).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cache)!;

    private static Renderable Resolve(Renderer renderer, Archetype archetype, gtaDrawable drawable) =>
        (Renderable)typeof(Renderer).GetMethod("TryGetRenderable", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(renderer, [archetype, drawable, 0u, null, null])!;
}
