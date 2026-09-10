using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeWalker.GameFiles
{


    public static class StatsNames
    {
        public static Dictionary<uint, string> Index = new();
        private static readonly System.Threading.Lock syncRoot = new();

        public static volatile bool FullIndexBuilt = false;

        public static void Clear()
        {
            lock (syncRoot)
            {
                Index.Clear();
            }
        }

        public static bool Ensure(string str)
        {
            uint hash = JenkHash.GenHash(str);
            if (hash == 0) return true;
            lock (syncRoot)
            {
                return !Index.TryAdd(hash, str);
            }
        }

        public static bool Ensure(string str, uint hash)
        {
            if (hash == 0) return true;
            lock (syncRoot)
            {
                return !Index.TryAdd(hash, str);
            }
        }

        public static string GetString(uint hash)
        {
            string? res;
            lock (syncRoot)
            {
                if (!Index.TryGetValue(hash, out res))
                {
                    res = hash.ToString();
                }
            }
            return res;
        }
        public static string TryGetString(uint hash)
        {
            string? res;
            lock (syncRoot)
            {
                if (!Index.TryGetValue(hash, out res))
                {
                    res = string.Empty;
                }
            }
            return res;
        }

    }



}
