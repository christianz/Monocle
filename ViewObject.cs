using System;

namespace Monocle
{
    [Serializable]
    public abstract class ViewObject : DbObject
    {
        public override void Save() {}
        public override void Delete() {}
    }
}
