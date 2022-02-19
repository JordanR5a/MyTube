using System.Collections.Generic;

namespace MyTube.Model
{
    internal class VideoComparer : IEqualityComparer<AttachedVideo>
    {
        public bool Equals(AttachedVideo x, AttachedVideo y)
        {
            return x.Id == y.Id;
        }

        public int GetHashCode(AttachedVideo obj)
        {
            return base.GetHashCode();
        }
    }
}