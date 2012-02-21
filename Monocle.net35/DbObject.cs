using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using Monocle.HyperPropertyDescriptor;
using Monocle.Utils;

namespace Monocle
{
    [Serializable]
    public abstract class DbObject
    {
        private static readonly Dictionary<string, PropertyDescriptorCollection> CachedPropertyDescriptors = new Dictionary<string, PropertyDescriptorCollection>(128);
        private static readonly Dictionary<string, Dictionary<string, int>> CachedColumnDefs = new Dictionary<string, Dictionary<string, int>>();
        private static readonly HashSet<string> CachedHyperTypes = new HashSet<string>();

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

            CacheTypeDescriptor(type, typeName);

            var srcPropertyInfo = GetPropertyDescriptors(dbObject, typeName);

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
            var props = TypeDescriptor.GetProperties(objInstance);
            var tableDef = TableDefinition.FromType(objInstance.GetType());
            var toMap = (from PropertyDescriptor p in props where ShouldMapProperty(tableDef, p) select p).ToList();

            return new PropertyDescriptorCollection(toMap.ToArray());
        }

        private static bool ShouldMapProperty(TableDefinition tableDef, MemberDescriptor p)
        {
            if (tableDef.ColumnsAreAutoMapped)
            {
                var unmappedAttr = p.Attributes[typeof(UnmappedAttribute)];

                if (unmappedAttr != null)
                    return false;
            }
            else
            {
                var colAttr = p.Attributes[typeof(ColumnAttribute)];

                if (colAttr == null || ((ColumnAttribute)colAttr).Identity)
                    return false;
            }

            return true;
        }

        private static PropertyDescriptorCollection GetPropertyDescriptors<T>(T objInstance, string typeName)
        {
            PropertyDescriptorCollection propertyInfo;

            if (!CachedPropertyDescriptors.TryGetValue(typeName, out propertyInfo))
            {
                propertyInfo = BuildPropertyInfo(objInstance);
                CachedPropertyDescriptors[typeName] = propertyInfo;
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

        private static void SetProperty<T>(T objInstance, IDictionary<string, int> cols, object[] objData, PropertyDescriptor prop) where T : new()
        {
            var key = prop.Name;

            int colOrdinal;

            if (!cols.TryGetValue(key, out colOrdinal))
                throw new Exception("Couldn't get the ordinal for column " + key + ".");

            var drVal = objData.GetValue(colOrdinal);

            if (drVal == null)
                return;

            var resVal = TypeHelper.ChangeType(drVal, prop.PropertyType);

            prop.SetValue(objInstance, resVal);
        }

        private static Dictionary<string, int> CacheColumnDefinitions(IDataRecord reader, string typeName)
        {
            Dictionary<string, int> cols;

            if (!CachedColumnDefs.TryGetValue(typeName, out cols))
            {
                var fldCnt = reader.FieldCount;
                cols = new Dictionary<string, int>();

                for (var i = 0; i < fldCnt; i++)
                {
                    cols.Add(reader.GetName(i).ToLower(), i);
                }

                CachedColumnDefs[typeName] = cols;
            }

            return cols;
        }

        /// <summary>
        /// Wrapper method around Transform(DataRow) that takes the first row of the passed-in DataReader and returns the result of Transform(DataRow).
        /// </summary>
        /// <typeparam name="T">The type we want to transform the DataRow properties to.</typeparam>
        /// <param name="reader">A DataReader containing 1 record (if it contains more, an exception is thrown) to transform into an instance of type T</param>
        /// <returns>An instance of type T with the properties filled from the DataReader</returns>
        internal static T FromParameters<T>(IDataReader reader) where T : new()
        {
            var type = typeof(T);

            if (type.IsValueType)
            {
                return (T)reader[0];
            }

            var typeName = GetTypeName(type);

            var cols = CacheColumnDefinitions(reader, typeName);

            var objArr = new object[cols.Count];
            reader.GetValues(objArr);

            return Transform<T>(cols, objArr);
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
            while (reader.Read())
            {
                yield return FromParameters<T>(reader);
            }
        }

        private static string GetTypeName(Type type)
        {
            var typeName = type.FullName;

            if (typeName == null)
                throw new ArgumentException("The FullName of type " + type + " is null.");

            return typeName;
        }

        private static T Transform<T>(IDictionary<string, int> cols, object[] objData) where T : new()
        {
            var type = typeof(T);

            var typeName = GetTypeName(type);

            CacheTypeDescriptor(type, typeName);

            var objInstance = ParameterlessConstructor<T>.Create();

            var propertyInfo = GetPropertyDescriptors(objInstance, typeName);

            foreach (PropertyDescriptor prop in propertyInfo)
            {
                SetProperty(objInstance, cols, objData, prop);
            }

            return objInstance;
        }
    }
}
