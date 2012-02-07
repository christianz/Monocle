﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Monocle
{
    class MsSqlQueryFactory : IQueryFactory
    {
        public string GetDeleteQuery(Persistable obj)
        {
            return "DELETE FROM [dbo].[" + obj.TableName + "] WHERE [Id]=@Id";
        }

        public string GetSaveQuery(Persistable obj, IEnumerable<Parameter> parameters)
        {
            var exists = obj.ExistsInDb;
            var name = obj.TableName;

            if (exists.HasValue)
            {
                return exists.Value ? GetUpdateStatement(name, parameters) : GetInsertStatement(name, parameters);
            }

            return string.Format("IF EXISTS (SELECT TOP 1 * FROM [dbo].[" + name + "] WHERE [Id]=@Id) {0} ELSE {1}",
                                 GetUpdateStatement(name, parameters),
                                 GetInsertStatement(name, parameters));
        }

        private static string GetUpdateStatement(string tableName, IEnumerable<Parameter> parameters)
        {
            var query = "UPDATE TOP (1) [dbo].[" + tableName + "] SET ";

            foreach (var parameter in parameters)
            {
                query += parameter.Name + "=@" + parameter.Name + ",";
            }

            query = query.Remove(query.LastIndexOf(",", StringComparison.Ordinal));

            query += " WHERE [Id] = @Id";

            return query;
        }

        private static string GetInsertStatement(string tableName, IEnumerable<Parameter> parameters)
        {
            var lParameters = parameters.ToList();

            var query = "INSERT INTO [dbo].[" + tableName + "] (";

            foreach (var parameter in lParameters)
            {
                query += parameter.Name + ",";
            }

            query = query.Remove(query.LastIndexOf(",", StringComparison.Ordinal));

            query += ") VALUES (";

            foreach (var parameter in lParameters)
            {
                query += "@" + parameter.Name + ",";
            }

            query = query.Remove(query.LastIndexOf(",", StringComparison.Ordinal));

            query += ")";

            return query;
        }
    }
}