using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Monocle
{
    public static class SqlHelper
    {
        public static SqlParameter[] Transform<T>(T dbObject)
        {
            var sqlParams = new List<SqlParameter>();

            var properties = dbObject.GetType().GetProperties();
            var colAttr = new ColumnAttribute();

            foreach (var prop in properties)
            {
                if (!prop.GetCustomAttributes(false).Contains(colAttr)) continue;

                var key = prop.Name;
                var value = prop.GetValue(dbObject, null);

                if (value != null)
                    sqlParams.Add(new SqlParameter(key, value));
            }

            return sqlParams.ToArray();
        }

        public static T Transform<T>(DataRow dataRow) where T : class, new()
        {
            var properties = typeof (T).GetProperties();

            var objInstance = new T();

            foreach (var prop in properties)
            {
                if (!prop.GetCustomAttributes(false).Contains(new ColumnAttribute())) continue;

                var key = prop.Name;
                var drVal = dataRow[key];

                if (drVal == null)
                    continue;

                var tryVal = drVal.ToString();
                object resVal;

                var propType = prop.PropertyType;

                if (propType == typeof(Guid))
                {
                    if (tryVal.Length == 0)
                        resVal = Guid.Empty;
                    else
                        resVal = new Guid(tryVal);
                }
                else
                {
                    resVal = MonocleDb.ChangeType(drVal, prop.PropertyType);
                }

                prop.SetValue(objInstance, resVal, null);
            }

            return objInstance;
        }

        public static T Transform<T>(DataTable dataTable) where T : class, new()
        {
            if (dataTable.Rows.Count == 0)
                return default(T);

            if (dataTable.Rows.Count > 1)
                throw new ArgumentException("Query returned more than one row. Use the TransformList<T> method for transforming more than one object.");

            return Transform<T>(dataTable.Rows[0]);
        }

        public static IEnumerable<T> TransformList<T>(DataTable dataTable) where T : class, new()
        {
            return dataTable.AsEnumerable().Select(Transform<T>);
        }
    }
}
