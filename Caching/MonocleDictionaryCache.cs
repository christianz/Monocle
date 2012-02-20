using System;
using System.Collections.Generic;

namespace Monocle.Caching
{
    internal class MonocleDictionaryCache : IMonocleCache
    {
        public bool Enabled { get; set; }

        private static readonly Dictionary<string, object> Cache = new Dictionary<string, object>();

        public void Add(string key, object obj)
        {
            Cache.Add(key, obj);
        }

        public T Get<T>(string key)
        {
            object obj;

            if (!Cache.TryGetValue(key, out obj))
                return default(T);

            return (T)obj;
        }

        public void SetDirty(string key)
        {
            Cache.Remove(key);
        }

        public bool Contains(string key)
        {
            return Cache.ContainsKey(key);
        }

        public Dictionary<string, object> AsDictionary()
        {
            return Cache;
        }

        public void Clear()
        {
            Cache.Clear();
        }
    }
}
