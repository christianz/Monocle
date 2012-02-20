using System.Collections.Generic;

namespace Monocle.Caching
{
    public interface IMonocleCache
    {
        void Add(string key, object obj);
        T Get<T>(string key);

        void SetDirty(string key);
        bool Contains(string key);
        bool Enabled { get; set; }

        Dictionary<string, object> AsDictionary();
        void Clear();
    }
}
