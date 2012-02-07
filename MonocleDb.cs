using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using Monocle.Profiler;

namespace Monocle
{
    public static class MonocleDb
    {
        private const int WhereClauseDefaultStringBuilderSize = 128;

        private static bool _useCaching;
        private static bool _useProfiling;
        private static string _connectionString;
        private static IMonocleLogWriter _logWriter;

        private static readonly MemoryCache Cache = new MemoryCache("memory", new NameValueCollection
                                                                                      {
                                                                                          { "CacheMemoryLimitMegabytes", "20" }
                                                                                      });
        
        public static void Initialize(string connectionString)
        {
            Initialize(connectionString, true);
        }
        
        public static void Initialize(string connectionString, bool useCaching)
        {
            Initialize(connectionString, useCaching, null);
        }

        public static void Initialize(string connectionString, bool useCaching, bool useProfiling)
        {
            Initialize(connectionString, useCaching, null);
        }

        public static void Initialize(string connectionString, IMonocleLogWriter logWriter)
        {
            Initialize(connectionString, true, logWriter);
        }

        public static void Initialize(string connectionString, bool useCaching, IMonocleLogWriter logWriter)
        {
            Initialize(connectionString, true, false, logWriter);
        }

        public static void Initialize(string connectionString, bool useCaching, bool useProfiling, IMonocleLogWriter logWriter)
        {
            _connectionString = connectionString;
            _useCaching = useCaching;
            _useProfiling = useProfiling;
            _logWriter = logWriter;
        }

        public static DataTable ExecuteDataTable(string cmdText, object parameters)
        {
            var sqlParams = GetParameters(parameters);

            return ExecuteDataTable(cmdText, true, sqlParams);
        }

        public static DataTable ExecuteDataTable(string cmdText)
        {
            return ExecuteDataTable(cmdText, new {});
        }

        public static IEnumerable<T> ExecuteList<T>(string cmdText) where T : class, new()
        {
            return ExecuteList<T>(cmdText, null);
        }

        public static IEnumerable<T> ExecuteList<T>(string cmdText, object parameters) where T : class, new()
        {
            var dt = ExecuteDataTable(cmdText, true, GetParameters(parameters));

            return PersistableHelper.TransformList<T>(dt);
        }

        public static void Execute(string cmdText)
        {
            Execute(cmdText, null);
        }

// ReSharper disable MethodOverloadWithOptionalParameter
        public static void Execute(string cmdText, params Parameter[] parameters)
// ReSharper restore MethodOverloadWithOptionalParameter
        {
            ExecuteDataTable(cmdText, false, parameters);
        }

        public static void Execute(string cmdText, object parameters)
        {
            ExecuteDataTable(cmdText, false, GetParameters(parameters));
        }

        public static T Execute<T>(string cmdText, object parameters) where T : class, new()
        {
            return PersistableHelper.Transform<T>(ExecuteDataTable(cmdText, true, GetParameters(parameters)));
        }

        public static T Execute<T>(string cmdText) where T : class, new()
        {
            return Execute<T>(cmdText, null);
        }

        public static T ExecuteScalar<T>(string cmdText, object parameters)
        {
            var dt = ExecuteDataTable(cmdText, true, GetParameters(parameters));

            if (dt.Rows.Count < 1)
                return default(T);

            var scalarValue = dt.Rows[0][0];
            return (T)ChangeType(scalarValue, typeof (T));
        }

        public static T ExecuteScalar<T>(string cmdText)
        {
            return ExecuteScalar<T>(cmdText, null);
        }

        public static object ChangeType(object value, Type type)
        {
            if (value == DBNull.Value)
            {
                if (type == typeof(Guid))
                    return Guid.Empty;

                if (type == typeof(Int32))
                    return 0;

                if (type == typeof(Single))
                    return 0F;

                if (type == typeof(Boolean))
                    return false;

                if (type == typeof(DateTime))
                    return DateTime.MinValue;

                return null;
            }

            if (value == null)
            {
                if (type.IsGenericType)
                    return Activator.CreateInstance(type);

                return null;
            }

            if (type == value.GetType())
                return value;

            if (type.IsEnum)
            {
                if (value is string)
                    return Enum.Parse(type, value as string);

                return Enum.ToObject(type, value);
            }

            if (!type.IsInterface && type.IsGenericType)
            {
                var innerType = type.GetGenericArguments()[0];
                var innerValue = ChangeType(value, innerType);
                return Activator.CreateInstance(type, new[] { innerValue });
            }

            if (!(value is IConvertible))
                return value;

            return Convert.ChangeType(value, type);
        }

        public static T FindById<T>(Guid id) where T : Persistable, new()
        {
            var prop = typeof(T).GetCustomAttributes(typeof(TableAttribute), false);

            if (prop.Length != 1)
                throw new ArgumentException("Type does not contain a [Table] attribute on the top of the class.");

            var tableName = ((TableAttribute)prop[0]).TableName;

            var cacheId = tableName + "_" + id;

            if (_useCaching && Cache.Contains(cacheId))
            {
                var cacheObj = Cache[cacheId];

                if (cacheObj != null && cacheObj.GetType() == typeof(T))
                    return (T)Cache[cacheId];
            }

            var result = Execute<T>("select top 1 * from [" + tableName + "] where [id] = @id", new[] { new Parameter("id", id) });

            if (_useCaching && result != null)
                Cache[cacheId] = result;

            if (result != null)
                result.ExistsInDb = true;

            return result;
        }

        public static T FindBy<T>(object parameters) where T : Persistable, new()
        {
            var sqlParams = GetParameters(parameters);

            var prop = typeof(T).GetCustomAttributes(typeof(TableAttribute), false);

            if (prop.Length != 1)
                throw new ArgumentException("Type does not contain a [Table] attribute on the top of the class.");

            var tableName = ((TableAttribute)prop[0]).TableName;

            var result = Execute<T>("select * from [dbo].[" + tableName + "] where " + GetWhereClause(sqlParams), parameters);

            result.ExistsInDb = true;

            return result;
        }

        private static DataTable ExecuteDataTable(string cmdText, bool expectsResults, IEnumerable<Parameter> parameters)
        {
            var isStoredProcedure = IsStoredProcedure(cmdText);

            var cmd = new SqlCommand(cmdText) { CommandType = isStoredProcedure ? CommandType.StoredProcedure : CommandType.Text };

            foreach (var p in parameters ?? new List<Parameter>(0))
                cmd.Parameters.AddWithValue(p.Name, p.Value);

            using (var sqlRunner = new MsSqlCommand(_connectionString, cmd))
            {
                if (_useProfiling)
                {
                    var profiler = new MsSqlProfiler();
                    sqlRunner.Profile(profiler);
                }

                if (expectsResults)
                    return sqlRunner.ExecuteDataTable();

                sqlRunner.ExecuteNonCommand();
                return null;
            }
        }

        private static T Execute<T>(string cmdText, IEnumerable<Parameter> parameters) where T : class, new()
        {
            return PersistableHelper.Transform<T>(ExecuteDataTable(cmdText, true, parameters));
        }

        private static bool IsStoredProcedure(string cmdText)
        {
            return cmdText.Split().Length == 1;
        }

        private static string GetWhereClause(IEnumerable<Parameter> sqlParams)
        {
            var sb = new StringBuilder(WhereClauseDefaultStringBuilderSize);

            foreach (var p in sqlParams)
            {
                sb.Append("[" + p.Name + "]=@" + p.Name + " and ");
            }

            var str = sb.ToString();
            str = str.Remove(str.LastIndexOf(" and ", StringComparison.Ordinal));

            return str;
        }

        private static IEnumerable<Parameter> GetParameters(object parameters)
        {
            var list = new List<Parameter>();

            if (parameters == null)
                return list.ToArray();

            list.AddRange(from PropertyDescriptor descriptor in TypeDescriptor.GetProperties(parameters)
                          let value = descriptor.GetValue(parameters)
                          select new Parameter(descriptor.Name, value));

            return list.ToArray();
        }

        #region Logging

        private static void WriteToLog(SqlCommand cmd)
        {
            var cmdText = cmd.CommandText;

            foreach (Parameter param in cmd.Parameters)
            {
                cmdText = cmdText.Replace("@" + param.Name, param.Value.ToString());
            }

            _logWriter.Write(DateTime.Now, cmdText);
        }

        private static void WriteToLog(string text)
        {
            _logWriter.Write(DateTime.Now, text);
        }

        #endregion

        #region Caching
        
        internal static void SetDirty(string cacheId)
        {
            if (_useCaching)
                Cache.Remove(cacheId);
        }

        public static MemoryCache GetCache()
        {
            return Cache;
        }

        public static void ClearCache()
        {
            Cache.Trim(100);
        }

        public static bool Caching
        {
            get { return _useCaching; }
            set { _useCaching = value; }
        }

        #endregion
    }
}
