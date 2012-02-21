using System.Collections.Generic;
using System.Linq;

namespace Monocle
{
    internal class MsSqlQueryFactory : IQueryFactory
    {
        public string GetDeleteQuery(Persistable obj)
        {
            return string.Concat("DELETE FROM [dbo].[", obj.TableDef.TableName, "] WHERE [Id]=@Id");
        }

        public string GetSaveQuery(Persistable obj, IEnumerable<Parameter> parameters)
        {
            var parameterList = parameters.ToList();
            var exists = obj.ExistsInDb;
            var name = obj.TableDef.TableName;

            if (exists.HasValue)
            {
                return exists.Value ? GetUpdateStatement(name, parameterList) : GetInsertStatement(name, parameterList);
            }

            return string.Concat("IF EXISTS (SELECT 1 FROM [dbo].[", name, "] WHERE [Id]=@Id) ",
                                 GetUpdateStatement(name, parameterList), " ELSE ",
                                 GetInsertStatement(name, parameterList));
        }

        private static string GetUpdateStatement(string tableName, IEnumerable<Parameter> parameters)
        {
            var query = string.Concat("UPDATE TOP (1) [dbo].[", tableName, "] SET ");
            var param = parameters.ToArray();
            var numParams = param.Length - 1;

            for (var i = 0; i < numParams; i++)
            {
                var name = param[i].Name;

                query = string.Concat(query, "[", name, "]", "=@", name, ",");
            }

            var lastName = param[numParams].Name;

            query = string.Concat(query, lastName, "=@", lastName);
            query = string.Concat(query, " WHERE [Id]=@Id");

            return query;
        }

        private static string GetInsertStatement(string tableName, IEnumerable<Parameter> parameters)
        {
            var query = "INSERT INTO [dbo].[" + tableName + "](";
            var param = parameters.ToArray();
            var numParams = param.Length - 1;

            for (var i = 0; i < numParams; i++)
            {
                query = string.Concat(query, "[", param[i].Name, "],");
            }

            query = string.Concat(query, "[", param[numParams].Name, "]) VALUES (");

            for (var i = 0; i < numParams; i++)
            {
                query = string.Concat("@", param[i].Name, ",");
            }

            query = string.Concat(query, "@", param[numParams].Name, ")");

            return query;
        }
    }
}
