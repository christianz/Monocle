using System;

namespace Monocle
{
    public class TableAttribute : Attribute
    {
        public string TableName { get; private set; }
        public AutoMapColumns AutoMap { get; private set; }

        public TableAttribute(string tableName, AutoMapColumns autoMap = AutoMapColumns.All)
        {
            TableName = tableName;
            AutoMap = autoMap;
        }
    }
}
