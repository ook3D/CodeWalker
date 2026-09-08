using CodeWalker.Rendering;
using SharpDX.Direct3D11;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class RenderableCacheTests
{
    [Fact]
    public void UnloadsAreSpreadAcrossFramesAndMemoryRemainsAccountedFor()
    {
        var cache = new RenderableCacheLookup<string, TestItem>(1000, 10);
        for (int i = 0; i < 20; i++)
        {
            var item = cache.Get(i.ToString());
            item.LastUseTime = DateTime.UtcNow.AddMinutes(-1).ToBinary();
        }
        cache.LoadProc(null!, 20);
        cache.UnloadProc();
        Assert.Equal(200, cache.CacheUse);
        cache.RenderThreadSync(null!, 2, System.Diagnostics.Stopwatch.GetTimestamp() - 1);
        Assert.Equal(200, cache.CacheUse); // exhausted time budget leaves the queue intact
        for (int frame = 1; frame <= 10; frame++)
        {
            cache.RenderThreadSync(null!, 2);
            Assert.Equal(200 - frame * 20, cache.CacheUse);
            Assert.Equal(20 - frame * 2, cache.CurrentCacheCount);
        }
        Assert.Equal(0, cache.CurrentLoadedCount);
    }

    [Fact]
    public void ClearReleasesResourcesWaitingForBudgetedDisposal()
    {
        var cache = new RenderableCacheLookup<string, TestItem>(100, 10);
        var item = cache.Get("drawable");
        cache.LoadProc(null!, 1);
        item.LastUseTime = DateTime.UtcNow.AddMinutes(-1).ToBinary();
        cache.UnloadProc();
        cache.RenderThreadSync(null!, 0);
        Assert.False(item.Disposed);
        cache.Clear();
        Assert.True(item.Disposed);
        Assert.Equal(0, cache.CacheUse);
        Assert.Equal(0, cache.CurrentCacheCount);
    }

    [Fact]
    public void FailedUploadIsCleanedUpAndCanBeRequestedAgain()
    {
        var cache = new RenderableCacheLookup<string, TestItem>(100, 10);
        var failed = cache.Get("drawable");
        failed.Fail = true;
        cache.LoadProc(null!, 1);
        Assert.False(failed.IsLoaded);
        Assert.Equal(0, cache.CacheUse);

        cache.RenderThreadSync(null!);
        Assert.True(failed.Disposed);
        Assert.Equal(0, cache.CurrentCacheCount);
        var retry = cache.Get("drawable");
        Assert.NotSame(failed, retry);
        cache.LoadProc(null!, 1);
        Assert.True(retry.IsLoaded);
        Assert.Equal(10, cache.CacheUse);
        Assert.Equal(1, cache.CurrentLoadedCount);
        cache.Clear();
        Assert.True(retry.Disposed);
        Assert.Equal(0, cache.CacheUse);
    }

    [Fact]
    public void ClearDisposesPartiallyLoadedItems()
    {
        var cache = new RenderableCacheLookup<string, TestItem>(100, 10);
        var item = cache.Get("drawable");
        item.Fail = true;
        cache.LoadProc(null!, 1);
        cache.Clear();
        Assert.True(item.Disposed);
        Assert.Equal(0, cache.CurrentCacheCount);
        Assert.Equal(0, cache.CacheUse);
    }

    public class TestItem : RenderableCacheItem<string>
    {
        public bool Fail;
        public bool Disposed;
        public override void Init(string key) { Key = key; DataSize = 10; }
        public override void Load(Device device)
        {
            if (Fail) throw new InvalidOperationException("Simulated upload failure");
            IsLoaded = true;
        }
        public override void Unload() { Disposed = true; IsLoaded = false; LoadQueued = false; }
    }
}
