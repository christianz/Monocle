using System;
using System.Collections.Generic;

namespace Monocle
{
    interface IQueryFactory
    {
        string GetDeleteQuery(Persistable obj);
        string GetSaveQuery(Persistable obj, IEnumerable<Parameter> parameters);
    }
}
