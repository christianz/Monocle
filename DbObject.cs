using System.Data.SqlClient;

namespace Monocle
{
    public abstract class DbObject
    {
        public SqlParameter[] Transform<T>(T obj) where T : class, new()
        {
            return SqlHelper.Transform(obj);
        }

        public virtual void Save() {}
        public virtual void Delete() {}
    }
}
