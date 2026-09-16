using CodeWalker.Rendering;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class ShaderKeyTests
{
    [Fact]
    public void BatchLookupUsesBothHashesWithoutAllocating()
    {
        var key = new ShaderKey { ShaderName = 1, ShaderFile = 2 };
        var batches = new Dictionary<ShaderKey, int>
        {
            [key] = 10,
            [new ShaderKey { ShaderName = 1, ShaderFile = 3 }] = 20,
            [new ShaderKey { ShaderName = 4, ShaderFile = 2 }] = 30
        };
        Assert.Equal(3, batches.Count);
        Assert.Equal(20, batches[new ShaderKey { ShaderName = 1, ShaderFile = 3 }]);
        Assert.Equal(30, batches[new ShaderKey { ShaderName = 4, ShaderFile = 2 }]);
        Assert.Equal(key, new ShaderKey { ShaderName = 1, ShaderFile = 2 });

        // Warm up the comparer before measuring the per-geometry lookup.
        for (int i = 0; i < 10000; i++) _ = batches[key];
        long before = GC.GetAllocatedBytesForCurrentThread();
        int sum = 0;
        for (int i = 0; i < 10000; i++) sum += batches[key];
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(100000, sum);
        Assert.Equal(0, allocated);
    }
}
