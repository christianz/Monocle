using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using Monocle.HyperPropertyDescriptor;
using Monocle.Utils;

namespace Monocle
{
    public abstract class DbObject
    {
        private static readonly Dictionary<string, PropertyDescriptorCollection> ReadColumns =
            new Dictionary<string, PropertyDescriptorCollection>(128);

        private static readonly Dictionary<string, PropertyDescriptorCollection> WriteColumns =
            new Dictionary<string, PropertyDescriptorCollection>(128);

        private static readonly HashSet<string> CachedHyperTypes = new HashSet<string>();
        private static readonly Dictionary<string, int> CachedColumnOrdinals = new Dictionary<string, int>();

        public virtual void Save()
        {
        }

        public virtual void Delete()
        {
        }

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

            if (typeName == null)
                throw new ArgumentException("The FullName of type " + type + " is null.");

            if (!CachedHyperTypes.Contains(typeName))
            {
                CachedHyperTypes.Add(typeName);
                HyperTypeDescriptionProvider.Add(type);
            }

            PropertyDescriptorCollection srcPropertyInfo;

            if (!WriteColumns.TryGetValue(typeName, out srcPropertyInfo))
            {
                WriteColumns[typeName] = BuildPropertyInfo(dbObject);
            }

            foreach (PropertyDescriptor prop in srcPropertyInfo)
            {
                var key = prop.Name;
                var value = prop.GetValue(dbObject);

                if (value is DateTime)
                {
                    var dtVal = (DateTime) value;

                    if (dtVal == DateTime.MinValue)
                        value = null;
                }

                if (value != null)
                    yield return new Parameter(key, value);
            }
        }

        public IEnumerable<Parameter> GetParameters<T>() where T : DbObject
        {
            return GetParameters(typeof (T), (T) this);
        }

        /// <summary>
        /// Retrieve all the properties for a given type. This is cached later.
        /// </summary>
        /// <returns>A PropertyDescriptorCollection containing all the properties for the given type.</returns>
        private static PropertyDescriptorCollection BuildPropertyInfo(object objInstance)
        {
            var props = TypeDescriptor.GetProperties(objInstance);
            var tableDef = TableDefinition.FromType(objInstance.GetType());
            var toMap = (from PropertyDescriptor p in props where ShouldMapProperty(tableDef, p) select p).ToList();

            return new PropertyDescriptorCollection(toMap.ToArray());
        }

        private static bool ShouldMapProperty(TableDefinition tableDef, PropertyDescriptor p)
        {
            if (tableDef.ColumnsAreAutoMapped)
            {
                var unmappedAttr = p.Attributes[typeof (UnmappedAttribute)];

                if (unmappedAttr != null)
                    return false;
            }
            else
            {
                var colAttr = p.Attributes[typeof (ColumnAttribute)];

                if (colAttr == null || ((ColumnAttribute)colAttr).Identity)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Transforms a single DataRow into an instance of type T. The values contained in the DataRow's cells are copied into
        /// the corresponding properties in a new instance of type T.
        /// </summary>
        /// <typeparam name="T">The type to transform the DataRow into</typeparam>
        /// <param name="objData">The DataRow containing the source values</param>
        /// <returns>A new instance of T with the values from the DataRow.</returns>
        private static T Transform<T>(IDataRecord objData) where T : new()
        {
            var type = typeof (T);

            if (type.IsValueType)
            {
                return (T)objData[0];
            }

            var typeName = type.FullName;

            CacheTypeDescriptor(type, typeName);

            var objInstance = ParameterlessConstructor<T>.Create();
            var propertyInfo = GetPropertyInfo(objInstance, typeName);

            foreach (PropertyDescriptor prop in propertyInfo)
            {
                SetProperty(objData, objInstance, typeName, prop);
            }

            return objInstance;
        }

        private static PropertyDescriptorCollection GetPropertyInfo<T>(T objInstance, string typeName) where T : new()
        {
            PropertyDescriptorCollection propertyInfo;

            if (!ReadColumns.TryGetValue(typeName, out propertyInfo))
            {
                propertyInfo = BuildPropertyInfo(objInstance);
                ReadColumns[typeName] = propertyInfo;
            }
            return propertyInfo;
        }

        private static void CacheTypeDescriptor(Type type, string typeName)
        {
            if (CachedHyperTypes.Contains(typeName)) 
                return;

            CachedHyperTypes.Add(typeName);
            HyperTypeDescriptionProvider.Add(type);
        }

        private static void SetProperty<T>(IDataRecord objData, T objInstance, string typeName, PropertyDescriptor prop) where T : new()
        {
            var objColAttr = prop.Attributes[typeof (ColumnAttribute)];
            var colAttr = objColAttr as ColumnAttribute;

            if (colAttr == null)
                return;

            var key = prop.Name.ToLower();
            var typeKey = typeName + key;
            int ord;

            if (!CachedColumnOrdinals.TryGetValue(typeKey, out ord))
                CachedColumnOrdinals[typeKey] = ord = objData.GetOrdinal(key);

            var drVal = objData.GetValue(ord);

            if (drVal == null)
                return;

            var resVal = TypeHelper.ChangeType(drVal, prop.PropertyType);

            prop.SetValue(objInstance, resVal);
        }

        /// <summary>
        /// Wrapper method around Transform(DataRow) that takes the first row of the passed-in DataReader and returns the result of Transform(DataRow).
        /// </summary>
        /// <typeparam name="T">The type we want to transform the DataRow properties to.</typeparam>
        /// <param name="reader">A DataReader containing 1 record (if it contains more, an exception is thrown) to transform into an instance of type T</param>
        /// <returns>An instance of type T with the properties filled from the DataReader</returns>
        public static T FromParameters<T>(IDataReader reader) where T : class, new()
        {
            if (!reader.Read())
                return default(T);

            try
            {
                return Transform<T>(reader);
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        /// Makes it possible to transform a DataReader into a list of instances of type T. Every record in the DataReader will contain a set of fields
        /// where the values correspond to the properties that are defined on the type T.
        /// </summary>
        /// <typeparam name="T">The type we want to transform the records to</typeparam>
        /// <param name="reader">A DataReader containing a collection of records that are transformed</param>
        /// <returns>An IEnumerable containing a collection of instances of type T</returns>
        public static IEnumerable<T> ListFromParameters<T>(IDataReader reader) where T : new()
        {
            var fldCnt = reader.FieldCount;
            var cols = new Dictionary<string, int>();

            for (var i = 0; i < fldCnt; i++)
            {
                cols.Add(reader.GetName(i).ToLower(), i);
            }

            var lst = new List<object[]>();

            while (reader.Read())
            {
                var objArr = new object[fldCnt];
                reader.GetValues(objArr);
                lst.Add(objArr);
            }

            foreach (var p in lst)
            {
                yield return Transform<T>(cols, p);
            }

            reader.Close();
        }

        private static T Transform<T>(IDictionary<string, int> cols, object[] objData) where T : new()
        {
            var type = typeof(T);

            if (type.IsValueType)
            {
                return (T)objData[0];
            }

            var typeName = type.FullName;

            if (typeName == null)
                throw new ArgumentException("The FullName of type " + type + " is null.");

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

            foreach (PropertyDescriptor prop in propertyInfo)
            {
                var key = prop.Name.ToLower();

                var drVal = objData[cols[key]];

                if (drVal == null)
                    continue;

                var resVal = TypeHelper.ChangeType(drVal, prop.PropertyType);

                prop.SetValue(objInstance, resVal);
            }

            return objInstance;
        }
    }
}
