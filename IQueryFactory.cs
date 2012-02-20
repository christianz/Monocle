using System.Collections.Generic;

namespace Monocle
{
    internal interface IQueryFactory
    {
        string GetDeleteQuery(Persistable obj);
        string GetSaveQuery(Persistable obj, IEnumerable<Parameter> parameters);
    }
}
