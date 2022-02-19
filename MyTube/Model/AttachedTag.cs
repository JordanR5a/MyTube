using System.Collections.Generic;

namespace MyTube.Model
{
    public class AttachedTag
    {
        public string Name { get; set; }
        public List<AttachedVideo> Videos { get; set; }
        public AttachedTag Parent { get; set; }
        public List<AttachedTag> Children { get; set; }
        public int Size
        {
            get
            {
                int total = Children.Count;
                foreach (var child in Children) total += child.Size;
                return total;
            }
        }

        public AttachedTag(string name)
        {
            Parent = null;
            Name = name;
            Videos = new List<AttachedVideo>();
            Children = new List<AttachedTag>();
        }

        public AttachedTag(AttachedTag parent, string name)
        {
            Parent = parent;
            Name = name;
            Videos = new List<AttachedVideo>();
            Children = new List<AttachedTag>();
        }

        public AttachedTag() { Children = new List<AttachedTag>(); }
    }
}