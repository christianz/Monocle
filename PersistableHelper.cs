using System.Collections.Generic;
using System.Data;

namespace Monocle
{
    public static class PersistableHelper
    {
        public static Dictionary<string, object> GetFirstRowAsDictionary(IDataReader table)
        {
            var fldCount = table.FieldCount;
            var columnNames = GetColumnNames(fldCount, table);

            var dict = new Dictionary<string, object>(columnNames.Count);

            for (var i = 0; i < fldCount; i++)
            {
                dict.Add(columnNames[i], table.GetValue(i));
            }

            return dict;
        }

        private static List<string> GetColumnNames(int numColumns, IDataRecord columns)
        {
            var columnNames = new List<string>(numColumns);

            for (var i = 0; i < numColumns; i++)
            {
                columnNames.Add(columns.GetName(i).ToLower());
            }

            return columnNames;
        }

        public static IEnumerable<Dictionary<string, object>> GetAllRowsAsDictionary(IDataReader table)
        {
            var fldCount = table.FieldCount;
            var columnNames = GetColumnNames(fldCount, table);

            do
            {
                var dict = new Dictionary<string, object>(columnNames.Count);

                for (var i = 0; i < fldCount; i++)
                    dict.Add(columnNames[i], table.GetValue(i));

                yield return dict;
            } while (table.Read());
        }
    }
}
