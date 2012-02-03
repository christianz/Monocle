using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Monocle.HyperPropertyDescriptor;

namespace Monocle
{
    /// <summary>
    /// All HyperTypeDescriptor code used is written by Marc Gravell, http://www.codeproject.com/Members/Marc-Gravell
    /// </summary>
    public static class SqlHelper
    {
        private static readonly Dictionary<Type, PropertyDescriptorCollection> ReadColumns = new Dictionary<Type, PropertyDescriptorCollection>();
        private static readonly Dictionary<Type, PropertyDescriptorCollection> WriteColumns = new Dictionary<Type, PropertyDescriptorCollection>();
        private static readonly HashSet<Type> CachedHyperTypes = new HashSet<Type>();

        /// <summary>
        /// Transforms an instance of type T into a list of SqlParameters that other components can use to update or insert into the database.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="type"></param>
        /// <param name="dbObject"></param>
        /// <returns></returns>
        public static IEnumerable<SqlParameter> Transform<T>(Type type, T dbObject)
        {
            if (!CachedHyperTypes.Contains(type))
            {
                CachedHyperTypes.Add(type);
                HyperTypeDescriptionProvider.Add(type);
            }

            PropertyDescriptorCollection srcPropertyInfo;

            WriteColumns.TryGetValue(type, out srcPropertyInfo);

            if (srcPropertyInfo == null)
            {
                srcPropertyInfo = BuildPropertyInfo(dbObject);
                var propList = new List<PropertyDescriptor>(srcPropertyInfo.Count);

                foreach (PropertyDescriptor p in srcPropertyInfo)
                {
                    var objColAttr = p.Attributes[typeof(ColumnAttribute)];
                    var colAttr = objColAttr as ColumnAttribute;

                    if (colAttr == null || colAttr.Identity)
                        continue;

                    propList.Add(p);
                }

                var propertyInfo = new PropertyDescriptorCollection(propList.ToArray());

                WriteColumns[type] = propertyInfo;
                srcPropertyInfo = propertyInfo;
            }

            foreach (PropertyDescriptor prop in srcPropertyInfo)
            {
                var key = prop.Name;
                var value = prop.GetValue(dbObject);

                if (value is DateTime)
                {
                    var dtVal = (DateTime)value;

                    if (dtVal == DateTime.MinValue)
                        value = null;
                }

                if (value != null)
                    yield return new SqlParameter(key, value);
            }
        }

        public static IEnumerable<SqlParameter> Transform<T>(T dbObject)
        {
            return Transform(typeof(T), dbObject);
        }

        /// <summary>
        /// Transforms a single DataRow into an instance of type T. The values contained in the DataRow's cells are copied into
        /// the corresponding properties in a new instance of type T.
        /// </summary>
        /// <typeparam name="T">The type to transform the DataRow into</typeparam>
        /// <param name="dataRow">The DataRow containing the source values</param>
        /// <returns>A new instance of T with the values from the DataRow.</returns>
        public static T Transform<T>(DataRow dataRow) where T : new()
        {
            var type = typeof(T);

            if (!CachedHyperTypes.Contains(type))
            {
                CachedHyperTypes.Add(type);
                HyperTypeDescriptionProvider.Add(type);
            }

            var objInstance = new T();

            PropertyDescriptorCollection propertyInfo;

            ReadColumns.TryGetValue(type, out propertyInfo);

            if (propertyInfo == null)
            {
                propertyInfo = BuildPropertyInfo(objInstance);
                ReadColumns[type] = propertyInfo;
            }

            foreach (PropertyDescriptor prop in propertyInfo)
            {
                var objColAttr = prop.Attributes[typeof(ColumnAttribute)];
                var colAttr = objColAttr as ColumnAttribute;

                if (colAttr == null)
                    continue;

                var key = prop.Name;
                var drVal = dataRow[key];

                if (drVal == null)
                    continue;

                var resVal = MonocleDb.ChangeType(drVal, prop.PropertyType);

                prop.SetValue(objInstance, resVal);
            }

            return objInstance;
        }

        /// <summary>
        /// Retrieve all the properties for a given type. This is cached later.
        /// </summary>
        /// <typeparam name="T">The type to get all the properties for.</typeparam>
        /// <returns>A PropertyDescriptorCollection containing all the properties for the given type.</returns>
        private static PropertyDescriptorCollection BuildPropertyInfo(object objInstance)
        {
            return TypeDescriptor.GetProperties(objInstance);
        }

        /// <summary>
        /// Wrapper method around Transform(DataRow) that takes the first row of the passed-in DataTable and returns the result of Transform(DataRow).
        /// </summary>
        /// <typeparam name="T">The type we want to transform the DataRow properties to.</typeparam>
        /// <param name="dataTable">A DataTable containing 1 row (if it contains more, an exception is thrown) to transform into an instance of type T</param>
        /// <returns>An instance of type T with the properties filled from the DataTable</returns>
        public static T Transform<T>(DataTable dataTable) where T : class, new()
        {
            if (dataTable.Rows.Count == 0)
                return default(T);

            if (dataTable.Rows.Count > 1)
                throw new ArgumentException("Query returned more than one row. Use the TransformList<T> method for transforming more than one object.");

            return Transform<T>(dataTable.Rows[0]);
        }

        /// <summary>
        /// Makes it possible to transform a DataTable into a list of instances of type T. Every DataRow in the DataTable will contain a set of DataColumns
        /// where the values correspond to the properties that are defined on the type T.
        /// </summary>
        /// <typeparam name="T">The type we want to transform the DataRow[] to</typeparam>
        /// <param name="dataTable">A DataTable containing a collection of DataRows that are transformed</param>
        /// <returns>An IEnumerable containing a collection of instances of type T</returns>
        public static IEnumerable<T> TransformList<T>(DataTable dataTable) where T : new()
        {
            return from DataRow dataRow in dataTable.Rows select Transform<T>(dataRow);
        }
    }
}
