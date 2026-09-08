using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Device = SharpDX.Direct3D11.Device;
using Buffer = SharpDX.Direct3D11.Buffer;
using CodeWalker.World;
using System.Collections.Concurrent;
using CodeWalker.GameFiles;
using System.Threading;
using CodeWalker.Properties;

namespace CodeWalker.Rendering
{
    public class RenderableCache
    {
        public DateTime LastUpdate = DateTime.UtcNow;
        public DateTime LastUnload = DateTime.UtcNow;
        public double CacheTime = Settings.Default.GPUCacheTime;// 10.0; //seconds to keep something that's not used
        public double UnloadTime = Settings.Default.GPUCacheFlushTime;// 0.1; //seconds between running unload cycles
        public int MaxItemsPerLoop = 4; //GPU uploads per loop - kept small to keep things flowing rather than hitching

        public long TotalGraphicsMemoryUse
        {
            get
            {
                return renderables.CacheUse
                    + textures.CacheUse 
                    + boundcomps.CacheUse 
                    + instbatches.CacheUse
                    + lodlights.CacheUse
                    + distlodlights.CacheUse
                    + pathbatches.CacheUse 
                    + waterquads.CacheUse;
            }
        }
        public int TotalItemCount
        {
            get
            {
                return renderables.CurrentLoadedCount
                    + textures.CurrentLoadedCount
                    + boundcomps.CurrentLoadedCount
                    + instbatches.CurrentLoadedCount
                    + lodlights.CurrentLoadedCount
                    + distlodlights.CurrentLoadedCount
                    + pathbatches.CurrentLoadedCount
                    + waterquads.CurrentLoadedCount;
            }
        }
        public int TotalQueueLength
        {
            get
            {
                return renderables.QueueLength
                    + textures.QueueLength
                    + boundcomps.QueueLength
                    + instbatches.QueueLength
                    + lodlights.QueueLength
                    + distlodlights.QueueLength
                    + pathbatches.QueueLength
                    + waterquads.QueueLength;
            }
        }
        public int LoadedRenderableCount
        {
            get
            {
                return renderables.CurrentLoadedCount;// loadedRenderables.Count;
            }
        }
        public int LoadedTextureCount
        {
            get
            {
                return textures.CurrentLoadedCount;// loadedTextures.Count;
            }
        }
        public int MemCachedRenderableCount
        {
            get
            {
                return renderables.CurrentCacheCount;// cacheRenderables.Count;
            }
        }
        public int MemCachedTextureCount
        {
            get
            {
                return textures.CurrentCacheCount;// cacheTextures.Count;
            }
        }


        private RenderableCacheLookup<DrawableBase, Renderable> renderables = new RenderableCacheLookup<DrawableBase, Renderable>(Settings.Default.GPUGeometryCacheSize, Settings.Default.GPUCacheTime);
        private RenderableCacheLookup<Texture, RenderableTexture> textures = new RenderableCacheLookup<Texture, RenderableTexture>(Settings.Default.GPUTextureCacheSize, Settings.Default.GPUCacheTime);
        private RenderableCacheLookup<Bounds, RenderableBoundComposite> boundcomps = new RenderableCacheLookup<Bounds, RenderableBoundComposite>(Settings.Default.GPUBoundCompCacheSize, Settings.Default.GPUCacheTime);
        private RenderableCacheLookup<YmapGrassInstanceBatch, RenderableInstanceBatch> instbatches = new RenderableCacheLookup<YmapGrassInstanceBatch, RenderableInstanceBatch>(67108864, Settings.Default.GPUCacheTime); //64MB - todo: make this a setting
        private RenderableCacheLookup<YmapFile, RenderableLODLights> lodlights = new RenderableCacheLookup<YmapFile, RenderableLODLights>(33554432, Settings.Default.GPUCacheTime); //32MB - todo: make this a setting
        private RenderableCacheLookup<YmapDistantLODLights, RenderableDistantLODLights> distlodlights = new RenderableCacheLookup<YmapDistantLODLights, RenderableDistantLODLights>(33554432, Settings.Default.GPUCacheTime); //32MB - todo: make this a setting
        private RenderableCacheLookup<BasePathData, RenderablePathBatch> pathbatches = new RenderableCacheLookup<BasePathData, RenderablePathBatch>(536870912 /*33554432*/, Settings.Default.GPUCacheTime); // 512MB /*32MB*/ - todo: make this a setting
        private RenderableCacheLookup<WaterQuad, RenderableWaterQuad> waterquads = new RenderableCacheLookup<WaterQuad, RenderableWaterQuad>(4194304, Settings.Default.GPUCacheTime); //4MB - todo: make this a setting


        private readonly Lock updateSyncRoot = new();

        private Device? currentDevice;


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

            bool itemsStillPending = false;

            lock (updateSyncRoot)
            {
                //load the queued items if possible
                int renderablecount = renderables.LoadProc(currentDevice, MaxItemsPerLoop);
                int texturecount = textures.LoadProc(currentDevice, MaxItemsPerLoop);
                int boundcompcount = boundcomps.LoadProc(currentDevice, MaxItemsPerLoop);
                int instbatchcount = instbatches.LoadProc(currentDevice, MaxItemsPerLoop);
                int lodlightcount = lodlights.LoadProc(currentDevice, MaxItemsPerLoop);
                int distlodlightcount = distlodlights.LoadProc(currentDevice, MaxItemsPerLoop);
                int pathbatchcount = pathbatches.LoadProc(currentDevice, MaxItemsPerLoop);
                int waterquadcount = waterquads.LoadProc(currentDevice, MaxItemsPerLoop);


                itemsStillPending =
                    (renderablecount >= MaxItemsPerLoop) ||
                    (texturecount >= MaxItemsPerLoop) ||
                    (boundcompcount >= MaxItemsPerLoop) ||
                    (instbatchcount >= MaxItemsPerLoop) ||
                    (lodlightcount >= MaxItemsPerLoop) ||
                    (distlodlightcount >= MaxItemsPerLoop) ||
                    (pathbatchcount >= MaxItemsPerLoop) ||
                    (waterquadcount >= MaxItemsPerLoop);


                //todo: change this to unload only when necessary (ie when something is loaded)
                var now = DateTime.UtcNow;
                var deltat = (now - LastUpdate).TotalSeconds;
                var unloadt = (now - LastUnload).TotalSeconds;
                if ((unloadt > UnloadTime) && (deltat < 0.25)) //don't try the unload on every loop... or when really busy
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
            }

            return itemsStillPending;
        }

        private int firstUnloadCache;

        public void RenderThreadSync()
        {
            if (currentDevice == null) return;
            // Resource disposal runs on the render thread. Spread streaming cleanup
            // over frames, with a shared 2 ms soft budget and at most 64 items per cache.
            // Rotate priority so a geometry backlog cannot starve texture cleanup.
            long deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 500;
            for (int i = 0; i < 8; i++)
            {
                switch ((firstUnloadCache + i) % 8)
                {
                    case 0: renderables.RenderThreadSync(currentDevice, 64, deadline); break;
                    case 1: textures.RenderThreadSync(currentDevice, 64, deadline); break;
                    case 2: boundcomps.RenderThreadSync(currentDevice, 64, deadline); break;
                    case 3: instbatches.RenderThreadSync(currentDevice, 64, deadline); break;
                    case 4: lodlights.RenderThreadSync(currentDevice, 64, deadline); break;
                    case 5: distlodlights.RenderThreadSync(currentDevice, 64, deadline); break;
                    case 6: pathbatches.RenderThreadSync(currentDevice, 64, deadline); break;
                    case 7: waterquads.RenderThreadSync(currentDevice, 64, deadline); break;
                }
            }
            firstUnloadCache = (firstUnloadCache + 1) % 8;
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
            if (lodlight.LodLights?.Ymap is { } ymap) lodlights.Invalidate(ymap);
            if (lodlight.DistLodLights is { } lights) distlodlights.Invalidate(lights);
        }
        public void InvalidateImmediate(YmapLODLights lodlightsonly)
        {
            if (lodlightsonly.Ymap is { } ymap && currentDevice != null) lodlights.UpdateImmediate(ymap, currentDevice);
        }

    }


    public abstract class RenderableCacheItem<TKey>
    {
        private TKey? key;
        public TKey Key { get => key ?? throw new InvalidOperationException("Cache item is not initialized."); set => key = value; }
        public volatile bool IsLoaded = false;
        public volatile bool LoadQueued = false;
        public long LastUseTime = 0;
        //public DateTime LastUseTime { get; set; }
        public long DataSize { get; set; }

        public abstract void Init(TKey key);
        public abstract void Load(Device device);
        public abstract void Unload();
    }

    public class RenderableCacheLookup<TKey, TVal> where TKey : notnull where TVal: RenderableCacheItem<TKey>, new()
    {
        private ConcurrentQueue<TVal> itemsToLoad = new ConcurrentQueue<TVal>();
        private ConcurrentQueue<TVal> itemsToUnload = new ConcurrentQueue<TVal>();
        private readonly ConcurrentQueue<TVal> failedLoads = new();
        private ConcurrentQueue<TKey> keysToInvalidate = new ConcurrentQueue<TKey>();
        private LinkedList<TVal> loadeditems = new LinkedList<TVal>();//only use from content thread!
        private Dictionary<TKey, TVal> cacheitems = new Dictionary<TKey, TVal>();//only use from render thread!
        public long CacheLimit;
        public long CacheUse = 0;
        public double CacheTime;
        public int LoadedCount = 0;//temporary, per loop
        private long LastFrameTime = 0;

        public RenderableCacheLookup(long limit, double time)
        {
            CacheLimit = limit;
            CacheTime = time;
            LastFrameTime = DateTime.UtcNow.ToBinary();
        }

        public int QueueLength
        {
            get
            {
                return itemsToLoad.Count;
            }
        }
        public int CurrentLoadedCount
        {
            get
            {
                return loadeditems.Count;
            }
        }
        public int CurrentCacheCount
        {
            get
            {
                return cacheitems.Count;
            }
        }

        public void Clear()
        {
            itemsToLoad = new ConcurrentQueue<TVal>();
            foreach (TVal rnd in loadeditems)
            {
                rnd.Unload();
            }
            loadeditems.Clear();
            // These items have already left loadeditems but still own GPU resources.
            while (itemsToUnload.TryDequeue(out var pending)) pending.Unload();
            while (failedLoads.TryDequeue(out var failed)) failed.Unload();
            cacheitems.Clear();
            itemsToUnload = new ConcurrentQueue<TVal>();
            keysToInvalidate = new ConcurrentQueue<TKey>();
            CacheUse = 0;
        }


        public int LoadProc(Device device, int maxitemsperloop)
        {
            TVal? item;
            LoadedCount = 0;
            while (itemsToLoad.TryDequeue(out item))
            {
                if (item.IsLoaded) continue; //don't load it again...
                LoadedCount++;
                long gcachefree = CacheLimit - Interlocked.Read(ref CacheUse);// CacheUse;
                if (gcachefree > item.DataSize)
                {
                    try
                    {
                        item.Load(device);
                        loadeditems.AddLast(item);
                        Interlocked.Add(ref CacheUse, item.DataSize);
                    }
                    catch (Exception ex)
                    {
                        // Keep cleanup on the render thread: it may still be resolving
                        // textures on this item. A failed upload must not remain queued forever.
                        item.IsLoaded = false;
                        failedLoads.Enqueue(item);
                        CodeWalker.GameFiles.LodDiag.Report("GPU upload failed for " + item.Key + ": " + ex.Message);
                    }
                }
                else
                {
                    item.LoadQueued = false; //can try load it again later..
                }
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

        public void RenderThreadSync(Device device) => RenderThreadSync(device, int.MaxValue);

        public void RenderThreadSync(Device device, int maxUnloads, long unloadDeadline = long.MaxValue)
        {
            LastFrameTime = DateTime.UtcNow.ToBinary();
            while (failedLoads.TryDequeue(out var failed))
            {
                if (cacheitems.TryGetValue(failed.Key, out var cached) && ReferenceEquals(cached, failed))
                {
                    cacheitems.Remove(failed.Key);
                }
                failed.Unload();
                failed.LoadQueued = false;
                // Failed uploads were never added to loadeditems or CacheUse.
            }
            TVal? item;
            TKey? key;
            while (keysToInvalidate.TryDequeue(out key))
            {
                if (cacheitems.TryGetValue(key, out item))
                {
                    Interlocked.Add(ref CacheUse, -item.DataSize);
                    item.Unload();
                    item.Init(key);
                    item.Load(device);
                    Interlocked.Add(ref CacheUse, item.DataSize);
                }
            }
            int unloaded = 0;
            while (unloaded < maxUnloads && Stopwatch.GetTimestamp() < unloadDeadline && itemsToUnload.TryDequeue(out item))
            {
                if (item.Key != null)
                {
                    cacheitems.Remove(item.Key);
                }
                item.Unload();
                item.LoadQueued = false;
                Interlocked.Add(ref CacheUse, -item.DataSize);
                unloaded++;
            }

        }

        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(key))]
        public TVal? Get(TKey? key)
        {
            if (key == null) return null;
            TVal? item = null;
            if (!cacheitems.TryGetValue(key, out item))
            {
                item = new TVal();
                item.Init(key);
                cacheitems.Add(key, item);
            }
            Interlocked.Exchange(ref item.LastUseTime, LastFrameTime);
            if ((!item.IsLoaded) && (!item.LoadQueued))// || 
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
            TVal? item;
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

}
