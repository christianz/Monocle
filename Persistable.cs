using System;
using System.Linq;

namespace Monocle
{
    public abstract class Persistable : DbObject
    {
        private static readonly IQueryFactory QueryGenerator = new MsSqlQueryFactory();

        public Guid Id { get; set; }

        internal bool? ExistsInDb { get; set; }
        internal TableDefinition TableDef { get; set; }

        private readonly Type _type;

        protected string CacheId
        {
            get { return TableDef + "_" + Id; }
        }

        protected Persistable()
        {
            _type = GetType();

            TableDef = TableDefinition.FromType(_type);
        }

        public new virtual void Save()
        {
            var parameters = GetParameters(_type, this).Where(p => p.Value != null).ToArray();

            var query = QueryGenerator.GetSaveQuery(this, parameters);

            MonocleDb.Execute(query, parameters);
            MonocleDb.Cache.SetDirty(CacheId);

            ExistsInDb = true;
        }

        public new virtual void Delete()
        {
            var query = QueryGenerator.GetDeleteQuery(this);

            MonocleDb.Execute(query);
            MonocleDb.Cache.SetDirty(CacheId);

            ExistsInDb = false;
        }
    }
}
