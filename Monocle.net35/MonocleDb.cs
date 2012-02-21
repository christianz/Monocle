using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Monocle.Caching;
using Monocle.Profiler;
using Monocle.Utils;

namespace Monocle
{
    public static class MonocleDb
    {
        private const int WhereClauseDefaultStringBuilderSize = 64;

        private static bool _useProfiling;
        private static string _connectionString;
        private static IMonocleLogWriter _logWriter;
        private static readonly IMonocleCache InternalCache = new MonocleDictionaryCache();

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
            _useProfiling = useProfiling;
            _logWriter = logWriter;
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

        public static IEnumerable<T> List<T>() where T : new()
        {
            var tableDef = TableDefinition.FromType(typeof(T));

            return ExecuteList<T>(string.Concat("select * from [dbo].[", tableDef.TableName, "]"));
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
            return (T)TypeHelper.ChangeType(scalarValue, typeof(T));
        }

        public static T ExecuteScalar<T>(string cmdText)
        {
            return ExecuteScalar<T>(cmdText, null);
        }

        public static T FindById<T>(Guid id) where T : Persistable, new()
        {
            var tableDef = TableDefinition.FromType(typeof(T));

            var cacheId = string.Concat(tableDef, "_", id);

            var cacheObj = InternalCache.Get<T>(cacheId);

            if (cacheObj != null)
                return cacheObj;

            var result = Execute<T>(string.Concat("select top 1 * from [", tableDef.TableName, "] where [id] = @id"), new[] { new Parameter("id", id) });

            InternalCache.Add(cacheId, result);

            if (result != null)
                result.ExistsInDb = true;

            return result;
        }

        public static T FindBy<T>(object parameters) where T : Persistable, new()
        {
            var tableName = TableDefinition.FromType(typeof(T)).TableName;

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

            sb.Append("1=1");

            var str = sb.ToString();

            return str;
        }

        private static IEnumerable<Parameter> GetDbParameters(object parameters)
        {
            if (parameters == null)
                return new List<Parameter>(0);

            return from PropertyDescriptor descriptor in TypeDescriptor.GetProperties(parameters)
                   let value = descriptor.GetValue(parameters)
                   select new Parameter(descriptor.Name, value);
        }

        public static IMonocleCache Cache
        {
            get { return InternalCache; }
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
    }
}
