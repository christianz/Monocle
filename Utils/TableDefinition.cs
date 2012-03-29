using System;
using System.Collections.Generic;

namespace Monocle.Utils
{
    internal class TableDefinition
    {
        private static readonly Dictionary<Type, TableDefinition> ClassTableDictionary = new Dictionary<Type, TableDefinition>();

        internal string TableName { get; set; }
        internal AutoMapColumns AutoMap { get; set; }

        private readonly bool _columnsAreAutoMapped;

        public TableDefinition(string tableName, AutoMapColumns autoMap)
        {
            TableName = tableName;
            AutoMap = autoMap;
            _columnsAreAutoMapped = (autoMap == AutoMapColumns.All);
        }

        internal static TableDefinition FromType(Type t)
        {
            TableDefinition tableDef;

            lock (ClassTableDictionary)
            {
                if (!ClassTableDictionary.TryGetValue(t, out tableDef))
                    ClassTableDictionary[t] = tableDef = BuildTableDefinition(t);
            }

            return tableDef;
        }

        private static TableDefinition BuildTableDefinition(Type t)
        {
            var customAttributes = t.GetCustomAttributes(typeof(TableAttribute), false);
            var tableName = t.Name;
            var autoMap = AutoMapColumns.None;

            foreach (var prop in customAttributes)
            {
                var tableAttr = (TableAttribute)prop;

                if (tableAttr == null)
                    continue;

                if (!string.IsNullOrEmpty(tableAttr.TableName))
                    tableName = tableAttr.TableName;

                autoMap = tableAttr.AutoMap;

                break;
            }

            return new TableDefinition(tableName, autoMap);
        }

        internal bool ColumnsAreAutoMapped
        {
            get { return _columnsAreAutoMapped; }
        }
    }

}
