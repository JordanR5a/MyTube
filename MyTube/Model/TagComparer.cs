using System.Collections.Generic;

namespace MyTube.Model
{
    internal class TagComparer : IEqualityComparer<AttachedTag>
    {
        public bool Equals(AttachedTag x, AttachedTag y)
        {
            return x.Name == y.Name;
        }

        public int GetHashCode(AttachedTag obj)
        {
            return base.GetHashCode();
        }
    }
}