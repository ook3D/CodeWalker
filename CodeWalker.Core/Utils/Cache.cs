using System;
using System.Collections.Generic;
using System.Threading;

namespace CodeWalker
{
    public class Cache<TKey, TVal> where TKey : notnull where TVal : Cacheable<TKey>
    {
        public long MaxMemoryUsage = 536870912; //512mb
        public long CurrentMemoryUsage = 0;
        public double CacheTime = 10.0; //seconds to keep something that's not used
        public DateTime CurrentTime = DateTime.Now;

        private readonly LinkedList<TVal> loadedList = new();
        private readonly Dictionary<TKey, LinkedListNode<TVal>> loadedListDict = new();
        private readonly Lock cacheLock = new();

        public int Count
        {
            get
            {
                lock (cacheLock)
                {
                    return loadedList.Count;
                }
            }
        }

        public Cache()
        {
        }
        public Cache(long maxMemoryUsage, double cacheTime)
        {
            MaxMemoryUsage = maxMemoryUsage;
            CacheTime = cacheTime;
        }

        public void BeginFrame()
        {
            CurrentTime = DateTime.Now;
            Compact();
        }

        public TVal? TryGet(TKey key)
        {
            lock (cacheLock)
            {
                LinkedListNode<TVal>? lln = null;
                if (loadedListDict.TryGetValue(key, out lln))
                {
                    loadedList.Remove(lln);
                    loadedList.AddLast(lln);
                    lln.Value.LastUseTime = CurrentTime;
                }
                return (lln != null) ? lln.Value : null;
            }
        }
        public bool TryAdd(TKey key, TVal item)
        {
            lock (cacheLock)
            {
                ArgumentNullException.ThrowIfNull(item);
                // Reject duplicates before changing either collection or the caller's item.
                if (loadedListDict.ContainsKey(key)) return false;

                // Preserve the soft limit: one insertion may take usage over the budget.
                var cacheTime = CacheTime;
                for (int attempt = 0; !CanAdd() && attempt < 2; attempt++)
                {
                    while (!CanAdd() && loadedList.First is { } oldest &&
                        (CurrentTime - oldest.Value.LastUseTime).TotalSeconds > cacheTime)
                    {
                        RemoveNode(oldest, oldest.Value.Key);
                    }
                    cacheTime *= 0.5;
                }

                if (!CanAdd()) return false;

                item.Key = key;
                item.LastUseTime = CurrentTime;
                var node = new LinkedListNode<TVal>(item);
                loadedListDict.Add(key, node);
                loadedList.AddLast(node);
                Interlocked.Add(ref CurrentMemoryUsage, item.MemoryUsage);
                return true;
            }
        }

        public bool CanAdd()
        {
            return Interlocked.Read(ref CurrentMemoryUsage) < MaxMemoryUsage;
        }


        public void Clear()
        {
            lock (cacheLock)
            {
                loadedList.Clear();
                loadedListDict.Clear();
                Interlocked.Exchange(ref CurrentMemoryUsage, 0);
            }
        }

        public void Remove(TKey key)
        {
            lock (cacheLock)
            {
                if (loadedListDict.TryGetValue(key, out var node))
                {
                    RemoveNode(node, key);
                }
            }
        }

        // All callers hold cacheLock so the list, index and accounting change together.
        private void RemoveNode(LinkedListNode<TVal> node, TKey key)
        {
            loadedListDict.Remove(key);
            loadedList.Remove(node);
            Interlocked.Add(ref CurrentMemoryUsage, -node.Value.MemoryUsage);
        }


        public void Compact()
        {
            lock (cacheLock)
            {
                while (loadedList.First is { } oldest &&
                    (CurrentTime - oldest.Value.LastUseTime).TotalSeconds >= CacheTime)
                {
                    RemoveNode(oldest, oldest.Value.Key);
                }
            }
        }


    }

    public abstract class Cacheable<TKey> where TKey : notnull
    {
        public TKey Key = default!;
        public DateTime LastUseTime;
        public long MemoryUsage;
    }

}
