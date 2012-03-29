#if NET40
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.Caching;

namespace Monocle.Caching
{
    internal class MonocleMemoryCache : IMonocleCache
    {
        public bool Enabled { get; set; }

        private static readonly MemoryCache Cache = new MemoryCache("memory", new NameValueCollection
                                                                                  {
                                                                                      { "CacheMemoryLimitMegabytes", "20" }
                                                                                  });

        public void Add(string key, object obj)
        {
            Cache[key] = obj;
        }

        public T Get<T>(string key)
        {
            if (!Enabled || !Contains(key))
                return default(T);

            return (T)Cache[key];
        }

        public void SetDirty(string key)
        {
            Cache.Remove(key);
        }

        public bool Contains(string key)
        {
            return Cache.Contains(key);
        }

        public Dictionary<string, object> AsDictionary()
        {
            return Cache.ToDictionary(o => o.Key, o => o.Value);
        }

        public void Clear()
        {
            Cache.Trim(100);
        }
    }
}
#endif