using System.Collections.Generic;

namespace MyTube.Model
{
    public class DatabaseCore
    {
        public List<DetachedVideo> Videos { get; set; }
        public List<DetachedUnknown> Unknowns { get; set; }
        public DetachedTag TagOrigin { get; set; }

        public DatabaseCore()
        {
            Videos = new List<DetachedVideo>();
            Unknowns = new List<DetachedUnknown>();
            TagOrigin = new DetachedTag();
        }
    }
}