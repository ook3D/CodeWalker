using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using CodeWalker.GameFiles;
using CodeWalker.Properties;
using CodeWalker.World;
using Device = SharpDX.Direct3D11.Device;

namespace CodeWalker.Rendering;

public class RenderableCache
{
    private readonly RenderableCacheLookup<Bounds, RenderableBoundComposite> boundcomps =
        new(Settings.Default.GPUBoundCompCacheSize, Settings.Default.GPUCacheTime);

    public double CacheTime = Settings.Default.GPUCacheTime; // 10.0; //seconds to keep something that's not used

    private Device currentDevice;

    private readonly RenderableCacheLookup<YmapDistantLODLights, RenderableDistantLODLights> distlodlights =
        new(33554432, Settings.Default.GPUCacheTime); //32MB - todo: make this a setting

    private readonly RenderableCacheLookup<YmapGrassInstanceBatch, RenderableInstanceBatch> instbatches =
        new(67108864, Settings.Default.GPUCacheTime); //64MB - todo: make this a setting

    public DateTime LastUnload = DateTime.UtcNow;
    public DateTime LastUpdate = DateTime.UtcNow;

    private readonly RenderableCacheLookup<YmapFile, RenderableLODLights> lodlights = new(33554432,
        Settings.Default.GPUCacheTime); //32MB - todo: make this a setting

    public int MaxItemsPerLoop = 1; //to keep things flowing

    private readonly RenderableCacheLookup<BasePathData, RenderablePathBatch> pathbatches =
        new(536870912 /*33554432*/, Settings.Default.GPUCacheTime); // 512MB /*32MB*/ - todo: make this a setting


    private readonly RenderableCacheLookup<DrawableBase, Renderable> renderables =
        new(Settings.Default.GPUGeometryCacheSize, Settings.Default.GPUCacheTime);

    private readonly RenderableCacheLookup<Texture, RenderableTexture> textures =
        new(Settings.Default.GPUTextureCacheSize, Settings.Default.GPUCacheTime);

    public double UnloadTime = Settings.Default.GPUCacheFlushTime; // 0.1; //seconds between running unload cycles


    private readonly object updateSyncRoot = new();

    private readonly RenderableCacheLookup<WaterQuad, RenderableWaterQuad> waterquads = new(4194304,
        Settings.Default.GPUCacheTime); //4MB - todo: make this a setting

    public long TotalGraphicsMemoryUse =>
        renderables.CacheUse
        + textures.CacheUse
        + boundcomps.CacheUse
        + instbatches.CacheUse
        + lodlights.CacheUse
        + distlodlights.CacheUse
        + pathbatches.CacheUse
        + waterquads.CacheUse;

    public int TotalItemCount =>
        renderables.CurrentLoadedCount
        + textures.CurrentLoadedCount
        + boundcomps.CurrentLoadedCount
        + instbatches.CurrentLoadedCount
        + lodlights.CurrentLoadedCount
        + distlodlights.CurrentLoadedCount
        + pathbatches.CurrentLoadedCount
        + waterquads.CurrentLoadedCount;

    public int TotalQueueLength =>
        renderables.QueueLength
        + textures.QueueLength
        + boundcomps.QueueLength
        + instbatches.QueueLength
        + lodlights.QueueLength
        + distlodlights.QueueLength
        + pathbatches.QueueLength
        + waterquads.QueueLength;

    public int LoadedRenderableCount => renderables.CurrentLoadedCount; // loadedRenderables.Count;

    public int LoadedTextureCount => textures.CurrentLoadedCount; // loadedTextures.Count;

    public int MemCachedRenderableCount => renderables.CurrentCacheCount; // cacheRenderables.Count;

    public int MemCachedTextureCount => textures.CurrentCacheCount; // cacheTextures.Count;


    public void OnDeviceCreated(Device device)
    {
        currentDevice = device;
    }

    public void OnDeviceDestroyed()
    {
        currentDevice = null;

        renderables.Clear();
        textures.Clear();
        boundcomps.Clear();
        instbatches.Clear();
        lodlights.Clear();
        distlodlights.Clear();
        pathbatches.Clear();
        waterquads.Clear();
    }

    public bool ContentThreadProc()
    {
        if (currentDevice == null) return false; //can't do anything with no device

        Monitor.Enter(updateSyncRoot);


        //load the queued items if possible
        var renderablecount = renderables.LoadProc(currentDevice, MaxItemsPerLoop);
        var texturecount = textures.LoadProc(currentDevice, MaxItemsPerLoop);
        var boundcompcount = boundcomps.LoadProc(currentDevice, MaxItemsPerLoop);
        var instbatchcount = instbatches.LoadProc(currentDevice, MaxItemsPerLoop);
        var lodlightcount = lodlights.LoadProc(currentDevice, MaxItemsPerLoop);
        var distlodlightcount = distlodlights.LoadProc(currentDevice, MaxItemsPerLoop);
        var pathbatchcount = pathbatches.LoadProc(currentDevice, MaxItemsPerLoop);
        var waterquadcount = waterquads.LoadProc(currentDevice, MaxItemsPerLoop);


        var itemsStillPending =
            renderablecount >= MaxItemsPerLoop ||
            texturecount >= MaxItemsPerLoop ||
            boundcompcount >= MaxItemsPerLoop ||
            instbatchcount >= MaxItemsPerLoop ||
            lodlightcount >= MaxItemsPerLoop ||
            distlodlightcount >= MaxItemsPerLoop ||
            pathbatchcount >= MaxItemsPerLoop ||
            waterquadcount >= MaxItemsPerLoop;


        //todo: change this to unload only when necessary (ie when something is loaded)
        var now = DateTime.UtcNow;
        var deltat = (now - LastUpdate).TotalSeconds;
        var unloadt = (now - LastUnload).TotalSeconds;
        if (unloadt > UnloadTime && deltat < 0.25) //don't try the unload on every loop... or when really busy
        {
            //unload items that haven't been used in longer than the cache period.
            renderables.UnloadProc();
            textures.UnloadProc();
            boundcomps.UnloadProc();
            instbatches.UnloadProc();
            lodlights.UnloadProc();
            distlodlights.UnloadProc();
            pathbatches.UnloadProc();
            waterquads.UnloadProc();

            LastUnload = DateTime.UtcNow;
        }


        LastUpdate = DateTime.UtcNow;

        Monitor.Exit(updateSyncRoot);

        return itemsStillPending;
    }

    public void RenderThreadSync()
    {
        renderables.RenderThreadSync(currentDevice);
        textures.RenderThreadSync(currentDevice);
        boundcomps.RenderThreadSync(currentDevice);
        instbatches.RenderThreadSync(currentDevice);
        lodlights.RenderThreadSync(currentDevice);
        distlodlights.RenderThreadSync(currentDevice);
        pathbatches.RenderThreadSync(currentDevice);
        waterquads.RenderThreadSync(currentDevice);
    }

    public Renderable GetRenderable(DrawableBase drawable)
    {
        return renderables.Get(drawable);
    }

    public RenderableTexture GetRenderableTexture(Texture texture)
    {
        return textures.Get(texture);
    }

    public RenderableBoundComposite GetRenderableBoundComp(Bounds bound)
    {
        return boundcomps.Get(bound);
    }

    public RenderableInstanceBatch GetRenderableInstanceBatch(YmapGrassInstanceBatch batch)
    {
        return instbatches.Get(batch);
    }

    public RenderableDistantLODLights GetRenderableDistantLODLights(YmapDistantLODLights lights)
    {
        return distlodlights.Get(lights);
    }

    public RenderableLODLights GetRenderableLODLights(YmapFile ymap)
    {
        return lodlights.Get(ymap);
    }

    public RenderablePathBatch GetRenderablePathBatch(BasePathData pathdata)
    {
        return pathbatches.Get(pathdata);
    }

    public RenderableWaterQuad GetRenderableWaterQuad(WaterQuad quad)
    {
        return waterquads.Get(quad);
    }


    public void Invalidate(Bounds bounds)
    {
        boundcomps.Invalidate(bounds);
    }

    public void Invalidate(BasePathData path)
    {
        pathbatches.Invalidate(path);
    }

    public void Invalidate(YmapGrassInstanceBatch batch)
    {
        instbatches.Invalidate(batch);
    }

    public void Invalidate(YmapLODLight lodlight)
    {
        lodlights.Invalidate(lodlight.LodLights?.Ymap);
        distlodlights.Invalidate(lodlight.DistLodLights);
    }

    public void InvalidateImmediate(YmapLODLights lodlightsonly)
    {
        lodlights.UpdateImmediate(lodlightsonly?.Ymap, currentDevice);
    }
}

public abstract class RenderableCacheItem<TKey>
{
    public volatile bool IsLoaded = false;
    public TKey Key;
    public long LastUseTime;

    public volatile bool LoadQueued;

    //public DateTime LastUseTime { get; set; }
    public long DataSize { get; set; }

    public abstract void Init(TKey key);
    public abstract void Load(Device device);
    public abstract void Unload();
}

public class RenderableCacheLookup<TKey, TVal> where TVal : RenderableCacheItem<TKey>, new()
{
    private readonly Dictionary<TKey, TVal> cacheitems = new(); //only use from render thread!
    public long CacheLimit;
    public double CacheTime;
    public long CacheUse;
    private ConcurrentQueue<TVal> itemsToLoad = new();
    private ConcurrentQueue<TVal> itemsToUnload = new();
    private ConcurrentQueue<TKey> keysToInvalidate = new();
    private long LastFrameTime;
    public int LoadedCount; //temporary, per loop
    private readonly LinkedList<TVal> loadeditems = new(); //only use from content thread!

    public RenderableCacheLookup(long limit, double time)
    {
        CacheLimit = limit;
        CacheTime = time;
        LastFrameTime = DateTime.UtcNow.ToBinary();
    }

    public int QueueLength => itemsToLoad.Count;

    public int CurrentLoadedCount => loadeditems.Count;

    public int CurrentCacheCount => cacheitems.Count;

    public void Clear()
    {
        itemsToLoad = new ConcurrentQueue<TVal>();
        foreach (var rnd in loadeditems) rnd.Unload();
        loadeditems.Clear();
        cacheitems.Clear();
        itemsToUnload = new ConcurrentQueue<TVal>();
        keysToInvalidate = new ConcurrentQueue<TKey>();
        CacheUse = 0;
    }


    public int LoadProc(Device device, int maxitemsperloop)
    {
        TVal item;
        LoadedCount = 0;
        while (itemsToLoad.TryDequeue(out item))
        {
            if (item.IsLoaded) continue; //don't load it again...
            LoadedCount++;
            var gcachefree = CacheLimit - Interlocked.Read(ref CacheUse); // CacheUse;
            if (gcachefree > item.DataSize)
                try
                {
                    item.Load(device);
                    loadeditems.AddLast(item);
                    Interlocked.Add(ref CacheUse, item.DataSize);
                }
                catch //(Exception ex)
                {
                    //todo: error handling...
                }
            else
                item.LoadQueued = false; //can try load it again later..

            if (LoadedCount >= maxitemsperloop) break;
        }

        return LoadedCount;
    }

    public void UnloadProc()
    {
        //unload items that haven't been used in longer than the cache period.
        var now = DateTime.UtcNow;
        var rnode = loadeditems.First;
        while (rnode != null)
        {
            var lu = DateTime.FromBinary(Interlocked.Read(ref rnode.Value.LastUseTime));
            if ((now - lu).TotalSeconds > CacheTime)
            {
                var nextnode = rnode.Next;
                itemsToUnload.Enqueue(rnode.Value);
                loadeditems.Remove(rnode);
                rnode = nextnode;
            }
            else
            {
                rnode = rnode.Next;
            }
        }
    }

    public void RenderThreadSync(Device device)
    {
        LastFrameTime = DateTime.UtcNow.ToBinary();
        TVal item;
        TKey key;
        while (keysToInvalidate.TryDequeue(out key))
            if (cacheitems.TryGetValue(key, out item))
            {
                Interlocked.Add(ref CacheUse, -item.DataSize);
                item.Unload();
                item.Init(key);
                item.Load(device);
                Interlocked.Add(ref CacheUse, item.DataSize);
            }

        while (itemsToUnload.TryDequeue(out item))
        {
            if (item.Key != null && cacheitems.ContainsKey(item.Key)) cacheitems.Remove(item.Key);
            item.Unload();
            item.LoadQueued = false;
            Interlocked.Add(ref CacheUse, -item.DataSize);
        }
    }

    public TVal Get(TKey key)
    {
        if (key == null) return null;
        TVal item = null;
        if (!cacheitems.TryGetValue(key, out item))
        {
            item = new TVal();
            item.Init(key);
            cacheitems.Add(key, item);
        }

        Interlocked.Exchange(ref item.LastUseTime, LastFrameTime);
        if (!item.IsLoaded && !item.LoadQueued) // || 
        {
            item.LoadQueued = true;
            itemsToLoad.Enqueue(item);
        }

        return item;
    }


    public void Invalidate(TKey key)
    {
        if (key == null) return;

        keysToInvalidate.Enqueue(key);
    }

    public void UpdateImmediate(TKey key, Device device)
    {
        TVal item;
        if (cacheitems.TryGetValue(key, out item))
        {
            Interlocked.Add(ref CacheUse, -item.DataSize);
            item.Unload();
            item.Init(key);
            item.Load(device);
            Interlocked.Add(ref CacheUse, item.DataSize);
        }
    }
}