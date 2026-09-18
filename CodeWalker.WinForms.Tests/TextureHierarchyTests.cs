using System.Reflection;
using CodeWalker.GameFiles;
using CodeWalker.Rendering;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class TextureHierarchyTests
{
    [Fact]
    public void MissingDictionariesStayCachedUntilProjectTexturesChange()
    {
        var cache = new GameFileCache(1024 * 1024, 10, "", false, "", false, "") { IsInited = true };
        var renderer = new Renderer(null!, cache);
        var drawable = new gtaDrawable();
        var archetype = new Archetype { TextureDict = 101 };
        cache.AddProjectFile(new YtdFile(new RpfResourceFileEntry { ShortNameHash = 101 }) { Loaded = true });
        GetLookup<MetaHash>(cache, "textureParents")[101] = 103;
        GetLookup<MetaHash>(cache, "hdtexturelookup")[101] = 102;
        var renderable = Resolve(renderer, archetype, drawable);
        var sd = renderable.SDtxds;
        var hd = renderable.HDtxds;

        for (int i = 0; i < 100; i++) Resolve(renderer, archetype, drawable);

        Assert.Same(sd, renderable.SDtxds);
        Assert.Same(hd, renderable.HDtxds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CircularTextureParentsStopAtFirstRepeatedDictionary(bool selfParent)
    {
        var cache = new GameFileCache(1024 * 1024, 10, "", false, "", false, "") { IsInited = true };
        foreach (uint hash in new uint[] { 101, 102 })
            cache.AddProjectFile(new YtdFile(new RpfResourceFileEntry { ShortNameHash = hash }) { Loaded = true });
        GetLookup<MetaHash>(cache, "textureParents")[101] = selfParent ? 101u : 102u;
        GetLookup<MetaHash>(cache, "textureParents")[102] = 101;
        var renderer = new Renderer(null!, cache);

        var renderable = Resolve(renderer, new Archetype { TextureDict = 101 }, new gtaDrawable());

        Assert.Equal(selfParent ? 1 : 2, renderable.SDtxds!.Length);
        Assert.Null(cache.TryFindTextureInParent(999, 101));
    }

    [Fact]
    public void MissingParentAndHdDictionariesRetryWithoutHidingAvailableTextures()
    {
        var cache = new GameFileCache(1024 * 1024, 10, "", false, "", false, "") { IsInited = true };
        const uint sd = 101, parent = 102, hd = 103;
        var sdFile = new YtdFile(new RpfResourceFileEntry { ShortNameHash = sd }) { Loaded = true };
        cache.AddProjectFile(sdFile);
        GetLookup<MetaHash>(cache, "textureParents")[sd] = parent;
        GetLookup<MetaHash>(cache, "hdtexturelookup")[sd] = hd;
        var renderer = new Renderer(null!, cache) { renderhdtextures = true };
        var drawable = new gtaDrawable();
        var archetype = new Archetype { TextureDict = sd };
        var renderable = Resolve(renderer, archetype, drawable);
        Assert.Same(sdFile, Assert.Single(renderable.SDtxds!));

        var parentFile = new YtdFile(new RpfResourceFileEntry { ShortNameHash = parent }) { Loaded = true };
        var hdFile = new YtdFile(new RpfResourceFileEntry { ShortNameHash = hd }) { Loaded = true };
        cache.AddProjectFile(parentFile);
        cache.AddProjectFile(hdFile);
        Resolve(renderer, archetype, drawable);

        Assert.Equal(new[] { sdFile, parentFile }, renderable.SDtxds);
        Assert.Same(hdFile, Assert.Single(renderable.HDtxds!));
    }

    [Fact]
    public void TextureHierarchyRetriesDictionaryImportedAfterDrawable()
    {
        var cache = new GameFileCache(1024 * 1024, 10, "", false, "", false, "") { IsInited = true };
        var hash = JenkHash.GenHash("southside_textures");
        var renderer = new Renderer(null!, cache) { renderhdtextures = false };
        var drawable = new gtaDrawable();
        var archetype = new Archetype { TextureDict = hash };
        var renderable = Resolve(renderer, archetype, drawable);
        var ytd = new YtdFile(new RpfResourceFileEntry { Name = "southside_textures.ytd" }) { Loaded = true };

        cache.AddProjectFile(ytd);
        Resolve(renderer, archetype, drawable);

        Assert.Same(ytd, Assert.Single(renderable.SDtxds!));
    }

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
