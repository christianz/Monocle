using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace Monocle
{
    public abstract class CrudObject
    {
        private static readonly Dictionary<Type, string> ClassTableDictionary = new Dictionary<Type, string>();

        public abstract Guid Id { get; set; }
        internal bool? ExistsInDb { get; set; }
        private string TableName { get; set; }
        private readonly Type _type;

        protected string CacheId
        {
            get { return TableName + "_" + Id; }
        }

        protected CrudObject()
        {
            string tableName;
            _type = GetType();

            if (!ClassTableDictionary.TryGetValue(_type, out tableName))
            {
                var prop = _type.GetCustomAttributes(typeof (TableAttribute), false);
                if (prop.Length != 1)
                    throw new ArgumentException("Type has no TableAttribute.");

                tableName = ((TableAttribute) prop[0]).TableName;
                ClassTableDictionary[_type] = tableName;
            }

            TableName = tableName;
        }

        public virtual void Save()
        {
            var parameters = SqlHelper.Transform(_type, this).Where(p => p.Value != null).ToArray();

            string query;

            if (ExistsInDb.HasValue)
            {
                query = ExistsInDb.Value ? GetUpdateStatement(parameters) : GetInsertStatement(parameters);
            }
            else
            {
                query = "IF EXISTS (SELECT TOP 1 * FROM [dbo].[" + TableName + "] WHERE [Id]=@Id) " +
                        GetUpdateStatement(parameters) + " ELSE " + GetInsertStatement(parameters);
            }

            MonocleDb.Execute(query, parameters);
            MonocleDb.SetDirty(CacheId);

            ExistsInDb = true;
        }

        public virtual void Delete()
        {
            var query = "DELETE TOP (1) FROM [dbo].[" + TableName + "] WHERE [Id]='" + Id + "'";

            MonocleDb.Execute(query);
            MonocleDb.SetDirty(CacheId);

            ExistsInDb = false;
        }

        private string GetUpdateStatement(IEnumerable<SqlParameter> parameters)
        {
            var query = "UPDATE TOP (1) [dbo].[" + TableName + "] SET ";

            foreach (var parameter in parameters)
            {
                query += parameter.ParameterName + "=@" + parameter.ParameterName + ",";
            }

            query = query.Remove(query.LastIndexOf(",", StringComparison.Ordinal));

            query += " WHERE [Id] = @Id";

            return query;
        }

        private string GetInsertStatement(IEnumerable<SqlParameter> parameters)
        {
            var lParameters = parameters.ToList();

            var query = "INSERT INTO [dbo].[" + TableName + "] (";

            foreach (var parameter in lParameters)
            {
                query += parameter.ParameterName + ",";
            }

            query = query.Remove(query.LastIndexOf(",", StringComparison.Ordinal));

            query += ") VALUES (";

            foreach (var parameter in lParameters)
            {
                query += "@" + parameter.ParameterName + ",";
            }

            query = query.Remove(query.LastIndexOf(",", StringComparison.Ordinal));

            query += ")";

            return query;
        }
    }
}
