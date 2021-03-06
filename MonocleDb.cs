﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Monocle.Caching;
using Monocle.Utils;

namespace Monocle
{
    public static class MonocleDb
    {
        private const int WhereClauseDefaultStringBuilderSize = 64;

        private static bool _useProfiling;
        private static SqlConnection _connection;
        private static IMonocleLogWriter _logWriter;

#if NET40
        private static readonly IMonocleCache InternalCache = new MonocleMemoryCache();
#elif NET35
        private static readonly IMonocleCache InternalCache = new MonocleDictionaryCache();
#endif

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
            var connStr = new DbConnectionStringBuilder {ConnectionString = connectionString};

            connStr["MultipleActiveResultSets"] = true;

            _connection = new SqlConnection(connStr.ConnectionString);
            _connection.Open();

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
                CommandType = isStoredProcedure ? CommandType.StoredProcedure : CommandType.Text,
                Connection = _connection
            };

            foreach (var p in parameters ?? new List<Parameter>(0))
                cmd.Parameters.AddWithValue(p.Name, p.Value);

            WriteToLog(cmd);

            var dt = new DataTable("table");

            var da = new SqlDataAdapter(cmd);

            da.Fill(dt);

            return dt;
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

            return DbObject.ListFromParameters<T>(dt, cmdText).ToList();
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

            return Execute<T>(cmdText, (parameters as IEnumerable<Parameter>));
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

            var cacheId = string.Concat(tableDef.TableName, "_", id);

            var cacheObj = InternalCache.Get<T>(cacheId);

            if (cacheObj != null)
            {
                WriteToLog("Found cached item (id " + cacheId + ").");

                return cacheObj;
            }

            var strSelect = (string.Concat("select top 1 * from [", tableDef.TableName, "] where [id] = @id"));

            var result = Execute<T>(strSelect, new[] { new Parameter("id", id)} );

            if (result != null)
            {
                InternalCache.Add(cacheId, result);
                result.ExistsInDb = true;
            }

            return result;
        }

        public static T FindBy<T>(object parameters) where T : Persistable, new()
        {
            var tableName = TableDefinition.FromType(typeof(T)).TableName;

            var sqlParams = GetDbParameters(parameters);

            var result = Execute<T>(string.Concat("select * from [dbo].[", tableName, "] where ", GetWhereClause(sqlParams)), parameters);

            result.ExistsInDb = true;

            return result;
        }

        private static IDataReader ExecuteReader(string cmdText, bool expectsResults, IEnumerable<Parameter> parameters)
        {
            var isStoredProcedure = IsStoredProcedure(cmdText);

            var cmd = new SqlCommand(cmdText)
            {
                CommandType = isStoredProcedure ? CommandType.StoredProcedure : CommandType.Text,
                Connection = _connection
            };

            foreach (var p in parameters ?? new List<Parameter>(0))
                cmd.Parameters.AddWithValue(p.Name, p.Value);

            WriteToLog(cmd);

            if (expectsResults)
                return cmd.ExecuteReader();

            cmd.ExecuteNonQuery();
            return null;
        }

        private static T Execute<T>(string cmdText, IEnumerable<Parameter> parameters) where T : class, new()
        {
            var reader = ExecuteReader(cmdText, true, parameters);

            if (!reader.Read())
                return default(T);

            try
            {
                return DbObject.FromParameters<T>(reader, cmdText);
            }
            finally
            {
                reader.Close();
            }
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
            if (_logWriter == null)
                return;

            var cmdText = cmd.CommandText;

            foreach (DbParameter param in cmd.Parameters)
            {
                cmdText = cmdText.Replace("@" + param.ParameterName, param.Value.ToString());
            }

            _logWriter.Write(DateTime.Now, cmdText);
        }

        private static void WriteToLog(string text)
        {
            if (_logWriter == null)
                return;

            _logWriter.Write(DateTime.Now, text);
        }

        #endregion
    }
}
