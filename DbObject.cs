using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using Monocle.HyperPropertyDescriptor;

namespace Monocle
{
    public abstract class DbObject
    {
        private static readonly Dictionary<string, PropertyDescriptorCollection> ReadColumns = new Dictionary<string, PropertyDescriptorCollection>(128);
        private static readonly Dictionary<string, PropertyDescriptorCollection> WriteColumns = new Dictionary<string, PropertyDescriptorCollection>(128);
        private static readonly HashSet<string> CachedHyperTypes = new HashSet<string>();

        public virtual void Save() { }
        public virtual void Delete() { }

        /// <summary>
        /// Transforms an instance of type T into a list of parameters that other components can use to update or insert into the database.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="type"></param>
        /// <param name="dbObject"></param>
        /// <returns></returns>
        internal IEnumerable<Parameter> GetParameters<T>(Type type, T dbObject)
        {
            var typeName = type.FullName;

            if (!CachedHyperTypes.Contains(typeName))
            {
                CachedHyperTypes.Add(typeName);
                HyperTypeDescriptionProvider.Add(type);
            }

            PropertyDescriptorCollection srcPropertyInfo;

            if (!WriteColumns.TryGetValue(typeName, out srcPropertyInfo))
            {
                srcPropertyInfo = BuildPropertyInfo(dbObject);
                var propList = new List<PropertyDescriptor>(srcPropertyInfo.Count);

                foreach (PropertyDescriptor p in srcPropertyInfo.AsParallel())
                {
                    var objColAttr = p.Attributes[typeof(ColumnAttribute)];
                    var colAttr = objColAttr as ColumnAttribute;

                    if (colAttr == null || colAttr.Identity)
                        continue;

                    propList.Add(p);
                }

                WriteColumns[typeName] = srcPropertyInfo = new PropertyDescriptorCollection(propList.ToArray());
            }

            foreach (PropertyDescriptor prop in srcPropertyInfo.AsParallel())
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
                    yield return new Parameter(key, value);
            }
        }

        public IEnumerable<Parameter> GetParameters<T>() where T : DbObject
        {
            return GetParameters(typeof(T), (T)this);
        }

        /// <summary>
        /// Retrieve all the properties for a given type. This is cached later.
        /// </summary>
        /// <returns>A PropertyDescriptorCollection containing all the properties for the given type.</returns>
        private static PropertyDescriptorCollection BuildPropertyInfo(object objInstance)
        {
            return TypeDescriptor.GetProperties(objInstance);
        }

        /// <summary>
        /// Transforms a single DataRow into an instance of type T. The values contained in the DataRow's cells are copied into
        /// the corresponding properties in a new instance of type T.
        /// </summary>
        /// <typeparam name="T">The type to transform the DataRow into</typeparam>
        /// <param name="objData">The DataRow containing the source values</param>
        /// <returns>A new instance of T with the values from the DataRow.</returns>
        private static T Transform<T>(IDictionary<string, object> objData) where T : new()
        {
            var type = typeof(T);
            var typeName = type.FullName;

            if (!CachedHyperTypes.Contains(typeName))
            {
                CachedHyperTypes.Add(typeName);
                HyperTypeDescriptionProvider.Add(type);
            }

            var objInstance = ParameterlessConstructor<T>.Create();

            PropertyDescriptorCollection propertyInfo;

            if (!ReadColumns.TryGetValue(typeName, out propertyInfo))
            {
                propertyInfo = BuildPropertyInfo(objInstance);
                ReadColumns[typeName] = propertyInfo;
            }

            foreach (PropertyDescriptor prop in propertyInfo.AsParallel())
            {
                var objColAttr = prop.Attributes[typeof(ColumnAttribute)];
                var colAttr = objColAttr as ColumnAttribute;

                if (colAttr == null)
                    continue;

                var key = prop.Name.ToLower();
                object drVal;

                if (!objData.TryGetValue(key, out drVal))
                    continue;

                var resVal = MonocleDb.ChangeType(drVal, prop.PropertyType);

                prop.SetValue(objInstance, resVal);
            }

            return objInstance;
        }

        /// <summary>
        /// Wrapper method around Transform(DataRow) that takes the first row of the passed-in DataTable and returns the result of Transform(DataRow).
        /// </summary>
        /// <typeparam name="T">The type we want to transform the DataRow properties to.</typeparam>
        /// <param name="dataTable">A DataTable containing 1 row (if it contains more, an exception is thrown) to transform into an instance of type T</param>
        /// <returns>An instance of type T with the properties filled from the DataTable</returns>
        public static T FromParameters<T>(DataTable dataTable) where T : class, new()
        {
            if (dataTable.Rows.Count == 0)
                return default(T);

            if (dataTable.Rows.Count > 1)
                throw new ArgumentException("Query returned more than one row. Use the TransformList<T> method for transforming more than one object.");

            var firstRowAsDict = DataTableHelper.GetFirstRowAsDictionary(dataTable);

            return Transform<T>(firstRowAsDict);
        }

        /// <summary>
        /// Makes it possible to transform a DataTable into a list of instances of type T. Every DataRow in the DataTable will contain a set of DataColumns
        /// where the values correspond to the properties that are defined on the type T.
        /// </summary>
        /// <typeparam name="T">The type we want to transform the DataRow[] to</typeparam>
        /// <param name="dataTable">A DataTable containing a collection of DataRows that are transformed</param>
        /// <returns>An IEnumerable containing a collection of instances of type T</returns>
        public static IEnumerable<T> ListFromParameters<T>(DataTable dataTable) where T : new()
        {
            return from dict in DataTableHelper.GetAllRowsAsDictionary(dataTable).AsParallel() select Transform<T>(dict);
        }

    }
}
