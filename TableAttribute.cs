using System;
using System.Collections.Generic;

namespace Monocle
{
    public class TableAttribute : Attribute
    {
        private static readonly Dictionary<Type, string> ClassTableDictionary = new Dictionary<Type, string>();

        public string TableName { get; private set; }

        public TableAttribute(string tableName)
        {
            TableName = tableName;
        }

        public static string GetTableName(Type t)
        {
            string tableName;

            if (!ClassTableDictionary.TryGetValue(t, out tableName))
            {
                var prop = t.GetCustomAttributes(typeof(TableAttribute), false);

                tableName = prop.Length != 1 ? t.Name : ((TableAttribute)prop[0]).TableName;

                ClassTableDictionary[t] = tableName;
            }

            return tableName;
        }
    }
}
