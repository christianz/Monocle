using System;
using Monocle.Utils;

namespace Monocle
{
    public class TableAttribute : Attribute
    {
        public string TableName;
        public AutoMapColumns AutoMap;

        public TableAttribute()
        {
            TableName = null;
            AutoMap = AutoMapColumns.None;
        }
    }
}
