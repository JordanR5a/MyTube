using System.Collections.Generic;

namespace MyTube.Model
{
    public class DetachedTag
    {
        public string Name { get; set; }
        public string Parent { get; set; }
        public List<DetachedTag> Children { get; set; }
    }
}