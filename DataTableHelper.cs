using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Monocle
{
    public static class DataTableHelper
    {
        public static Dictionary<string, object> GetFirstRowAsDictionary(DataTable table)
        {
            var columnNames = GetColumnNames(table.Columns);

            var dict = new Dictionary<string, object>(columnNames.Count);
            var dr = table.Rows[0];

            for (var i = 0; i < dr.ItemArray.Length; i++)
            {
                dict.Add(columnNames[i], dr.ItemArray[i]);
            }

            return dict;
        }

        private static List<string> GetColumnNames(ICollection columns)
        {
            var columnNames = new List<string>(columns.Count);
            columnNames.AddRange(from DataColumn col in columns select col.ColumnName.ToLower());

            return columnNames;
        }

        public static IEnumerable<Dictionary<string, object>> GetAllRowsAsDictionary(DataTable table)
        {
            var columnNames = GetColumnNames(table.Columns);

            foreach (DataRow dr in table.Rows.AsParallel())
            {
                var dict = new Dictionary<string, object>(columnNames.Count);

                for (var i = 0; i < dr.ItemArray.Length; i++)
                {
                    dict.Add(columnNames[i], dr.ItemArray[i]);
                }

                yield return dict;
            }
        }
    }
}
