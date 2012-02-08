using System;
using System.Collections.Generic;
using System.Linq;

namespace Monocle
{
    public abstract class Persistable : DbObject
    {
        private static readonly Dictionary<Type, string> ClassTableDictionary = new Dictionary<Type, string>();
        private static readonly IQueryFactory QueryGenerator = new MsSqlQueryFactory();

        public abstract Guid Id { get; set; }

        internal bool? ExistsInDb { get; set; }
        internal string TableName { get; set; }

        private readonly Type _type;

        protected string CacheId
        {
            get { return TableName + "_" + Id; }
        }

        protected Persistable()
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

        public new virtual void Save()
        {
            var parameters = GetParameters(_type, this).Where(p => p.Value != null).ToArray();

            var query = QueryGenerator.GetSaveQuery(this, parameters);

            MonocleDb.Execute(query, parameters);
            MonocleDb.SetDirty(CacheId);

            ExistsInDb = true;
        }

        public new virtual void Delete()
        {
            var query = QueryGenerator.GetDeleteQuery(this);

            MonocleDb.Execute(query);
            MonocleDb.SetDirty(CacheId);

            ExistsInDb = false;
        }
    }
}
