using System.Collections.Generic;

namespace Monocle
{
    public abstract class DbObject
    {
        public IEnumerable<Parameter> Transform<T>(T obj) where T : class, new()
        {
            return PersistableHelper.Transform(obj);
        }

        public virtual void Save() { }
        public virtual void Delete() { }
    }
}
