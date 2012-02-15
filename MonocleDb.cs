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
        private const int WhereClauseDefaultStringBuilderSize = 64;

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

        public static IDataReader ExecuteReader(string cmdText, object parameters)
        {
            var sqlParams = GetDbParameters(parameters);

            return ExecuteReader(cmdText, true, sqlParams);
        }

        public static IDataReader ExecuteReader(string cmdText)
        {
            return ExecuteReader(cmdText, new {});
        }

        public static DataTable ExecuteDataTable(string cmdText)
        {
            return ExecuteDataTable(cmdText, null);
        }

        public static DataTable ExecuteDataTable(string cmdText, object parameters)
        {
            return ExecuteDataTable(cmdText, GetDbParameters(parameters));
        }

        public static DataTable ExecuteDataTable(string cmdText, IEnumerable<Parameter> parameters)
        {
            var isStoredProcedure = IsStoredProcedure(cmdText);

            var cmd = new SqlCommand(cmdText)
                          {
                              CommandType = isStoredProcedure ? CommandType.StoredProcedure : CommandType.Text
                          };

            foreach (var p in parameters ?? new List<Parameter>(0))
                cmd.Parameters.AddWithValue(p.Name, p.Value);

            var sqlRunner = new MsSqlCommand(_connectionString, cmd);

            if (_useProfiling)
            {
                var profiler = new MsSqlProfiler();
                sqlRunner.Profile(profiler);
            }

            return sqlRunner.ExecuteDataTable();
        }

        public static IEnumerable<T> ExecuteList<T>(string cmdText) where T : new()
        {
            return ExecuteList<T>(cmdText, null);
        }

        public static IEnumerable<T> ExecuteList<T>(string cmdText, object parameters) where T : new()
        {
            var dt = ExecuteReader(cmdText, true, GetDbParameters(parameters));

            return DbObject.ListFromParameters<T>(dt);
        }

        public static void Execute(string cmdText)
        {
            Execute(cmdText, null);
        }

// ReSharper disable MethodOverloadWithOptionalParameter
        public static void Execute(string cmdText, params Parameter[] parameters)
// ReSharper restore MethodOverloadWithOptionalParameter
        {
            ExecuteReader(cmdText, false, parameters);
        }

        public static void Execute(string cmdText, object parameters)
        {
            if (!(parameters is IEnumerable<Parameter>))
                parameters = GetDbParameters(parameters);
                
            ExecuteReader(cmdText, false, (IEnumerable<Parameter>)parameters);
        }

        public static T Execute<T>(string cmdText, object parameters) where T : class, new()
        {
            if (!(parameters is IEnumerable<Parameter>))
                parameters = GetDbParameters(parameters);
                
            return DbObject.FromParameters<T>(ExecuteReader(cmdText, true, (IEnumerable<Parameter>)parameters));
        }

        public static T Execute<T>(string cmdText) where T : class, new()
        {
            return Execute<T>(cmdText, null);
        }

        public static T ExecuteScalar<T>(string cmdText, object parameters)
        {
            var dt = ExecuteReader(cmdText, true, GetDbParameters(parameters));

            if (!dt.Read())
                return default(T);

            var scalarValue = dt.GetValue(0);
            return (T)TypeHelper.ChangeType(scalarValue, typeof (T));
        }

        public static T ExecuteScalar<T>(string cmdText)
        {
            return ExecuteScalar<T>(cmdText, null);
        }

        public static T FindById<T>(Guid id) where T : Persistable, new()
        {
            var tableName = TableAttribute.GetTableName(typeof(T));
            
            var cacheId = string.Concat(tableName, "_", id);

            if (_useCaching && Cache.Contains(cacheId))
            {
                var cacheObj = Cache[cacheId];

                if (cacheObj != null && cacheObj.GetType() == typeof(T))
                    return (T)Cache[cacheId];
            }

            var result = Execute<T>(string.Concat("select top 1 * from [", tableName, "] where [id] = @id"), new[] { new Parameter("id", id) });

            if (_useCaching && result != null)
                Cache[cacheId] = result;

            if (result != null)
                result.ExistsInDb = true;

            return result;
        }

        public static T FindBy<T>(object parameters) where T : Persistable, new()
        {
            var tableName = TableAttribute.GetTableName(typeof(T));

            var sqlParams = GetDbParameters(parameters);

            var result = Execute<T>(string.Concat("select * from [dbo].[", tableName, "] where ", GetWhereClause(sqlParams), parameters));

            result.ExistsInDb = true;

            return result;
        }

        private static IDataReader ExecuteReader(string cmdText, bool expectsResults, IEnumerable<Parameter> parameters)
        {
            var isStoredProcedure = IsStoredProcedure(cmdText);

            var cmd = new SqlCommand(cmdText)
                          {
                              CommandType = isStoredProcedure ? CommandType.StoredProcedure : CommandType.Text
                          };

            foreach (var p in parameters ?? new List<Parameter>(0))
                cmd.Parameters.AddWithValue(p.Name, p.Value);

            var sqlRunner = new MsSqlCommand(_connectionString, cmd);

            if (_useProfiling)
            {
                var profiler = new MsSqlProfiler();
                sqlRunner.Profile(profiler);
            }

            if (expectsResults)
                return sqlRunner.ExecuteReader();

            sqlRunner.ExecuteNonCommand();
            return null;
        }

        private static T Execute<T>(string cmdText, IEnumerable<Parameter> parameters) where T : class, new()
        {
            return DbObject.FromParameters<T>(ExecuteReader(cmdText, true, parameters));
        }

        private static bool IsStoredProcedure(string cmdText)
        {
            return cmdText.Split().Length == 1;
        }

        private static string GetWhereClause(IEnumerable<Parameter> sqlParams)
        {
            var sb = new StringBuilder(WhereClauseDefaultStringBuilderSize);

            foreach (var name in sqlParams.Select(p => p.Name))
            {
                sb.Append(string.Concat("[", name, "]=@", name, " and "));
            }

            var str = sb.ToString();
            str = str.Remove(str.LastIndexOf(" and ", StringComparison.Ordinal));

            return str;
        }

        private static IEnumerable<Parameter> GetDbParameters(object parameters)
        {
            if (parameters == null)
                return new List<Parameter>(0);

            return from PropertyDescriptor descriptor in TypeDescriptor.GetProperties(parameters).AsParallel()
                          let value = descriptor.GetValue(parameters)
                          select new Parameter(descriptor.Name, value);
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

        public static Dictionary<string, object> GetCache()
        {
            return Cache.ToDictionary(o => o.Key, o => o.Value);
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
