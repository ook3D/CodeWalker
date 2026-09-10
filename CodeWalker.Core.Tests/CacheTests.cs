using Xunit;

namespace CodeWalker.Core.Tests;

public class CacheTests
{
    private sealed class Item : Cacheable<int>;

    [Fact]
    public void DuplicateInsertionLeavesCacheAndRejectedItemUnchanged()
    {
        var cache = new Cache<int, Item>();
        var original = new Item { MemoryUsage = 10 };
        var duplicate = new Item { Key = 42, MemoryUsage = 20 };
        Assert.True(cache.TryAdd(1, original));
        Assert.False(cache.TryAdd(1, duplicate));
        Assert.Equal(42, duplicate.Key);
        Assert.Equal(default, duplicate.LastUseTime);
        Assert.Equal(1, cache.Count);
        Assert.Equal(10, cache.CurrentMemoryUsage);
        Assert.Same(original, cache.TryGet(1));
        cache.Remove(1);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.CurrentMemoryUsage);
        Assert.Null(cache.TryGet(1));
    }

    [Fact]
    public void CompactEvictsExpiredItemsButRetainsRecentlyUsedItems()
    {
        var cache = new Cache<int, Item>(100, 10);
        Assert.True(cache.TryAdd(1, new Item { MemoryUsage = 10 }));
        Assert.True(cache.TryAdd(2, new Item { MemoryUsage = 20 }));
        cache.CurrentTime += TimeSpan.FromSeconds(9);
        Assert.NotNull(cache.TryGet(1));
        cache.CurrentTime += TimeSpan.FromSeconds(1);
        cache.Compact();
        Assert.Null(cache.TryGet(2));
        Assert.NotNull(cache.TryGet(1));
        Assert.Equal(1, cache.Count);
        Assert.Equal(10, cache.CurrentMemoryUsage);
        cache.Clear();
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.CurrentMemoryUsage);
    }

    [Fact]
    public void FullCacheRejectsFreshItemsThenUsesShorterEvictionWindow()
    {
        var cache = new Cache<int, Item>(10, 10);
        Assert.True(cache.TryAdd(1, new Item { MemoryUsage = 12 }));
        var rejected = new Item { Key = 42, MemoryUsage = 5 };
        Assert.False(cache.TryAdd(2, rejected));
        Assert.Equal(42, rejected.Key);
        cache.CurrentTime += TimeSpan.FromSeconds(6);
        Assert.True(cache.TryAdd(2, rejected));
        Assert.Null(cache.TryGet(1));
        Assert.Equal(5, cache.CurrentMemoryUsage);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void ConcurrentDuplicateAddsPreserveAccounting()
    {
        var cache = new Cache<int, Item>();
        Parallel.For(0, 2000, i => cache.TryAdd(i % 100, new Item { MemoryUsage = 1 }));
        Assert.Equal(100, cache.Count);
        Assert.Equal(100, cache.CurrentMemoryUsage);
        Parallel.For(0, 100, cache.Remove);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.CurrentMemoryUsage);
    }
}
