using System;
using System.Linq;
using Monocle.Utils;

namespace Monocle
{
    [Serializable]
    public abstract class Persistable : DbObject
    {
        private static readonly IQueryFactory QueryGenerator = new MsSqlQueryFactory();
        private Guid _id;

        [Column]
        public Guid Id
        {
            get { return _id; }
            set
            {
                _id = value;
                UpdateCacheId();
            }
        }

        internal bool? ExistsInDb { get; set; }
        internal TableDefinition TableDef { get; private set; }

        private readonly Type _type;

        private void UpdateCacheId()
        {
            CacheId = TableDef + "_" + Id;
        }

        internal string CacheId { get; private set; }

        protected Persistable()
        {
            _type = GetType();

            TableDef = TableDefinition.FromType(_type);
            UpdateCacheId();
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
