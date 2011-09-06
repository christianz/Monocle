using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace Monocle
{
    public abstract class CrudObject
    {
        public abstract Guid Id { get; set; }
        public bool ExistsInDb { get; set; }
        private string TableName { get; set; }

        protected string CacheId
        {
            get
            {
                return TableName + "_" + Id;
            }
        }
        
        protected CrudObject()
        {
            var prop = GetType().GetCustomAttributes(typeof (TableAttribute), false);
            if (prop.Length != 1)
                throw new ArgumentException("Type has no TableAttribute.");

            var table = (TableAttribute) prop[0];

            TableName = table.TableName;

            ExistsInDb = false;
        }

        public virtual void Save()
        {
            var parameters = SqlHelper.Transform(this).Where(p => p.Value != null).ToList();

            var query = ExistsInDb ? GetUpdateStatement(parameters) : GetInsertStatement(parameters);

            MonocleDb.Execute(query, parameters);
            MonocleDb.SetDirty(CacheId);

            ExistsInDb = true;
        }

        public virtual void Delete()
        {
            var query = "DELETE TOP (1) FROM [dbo].[" + TableName + "] WHERE [Id]=@id";

            MonocleDb.Execute(query, new { Id });
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

            query = query.Remove(query.LastIndexOf(","));

            query += " WHERE [Id]=@Id";

            return query;
        }

        private string GetInsertStatement(List<SqlParameter> parameters)
        {
            var query = "INSERT INTO [dbo].[" + TableName + "] (";

            foreach (var parameter in parameters)
            {
                query += parameter.ParameterName + ",";
            }

            query = query.Remove(query.LastIndexOf(","));

            query += ") VALUES (";

            foreach (var parameter in parameters)
            {
                query += "@" + parameter.ParameterName + ",";
            }

            query = query.Remove(query.LastIndexOf(","));

            query += ")";

            return query;
        }
    }
}
