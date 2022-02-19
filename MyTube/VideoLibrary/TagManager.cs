using System.Collections.Generic;
using System.Linq;
using MyTube.Model;

namespace MyTube.VideoLibrary
{
    public class TagManager
    {
        public static readonly string CORE_NAME = "Origin";
        public AttachedTag TagCore { get; }
        public AttachedTag[] RawTags { get { return GetTags(TagCore).ToArray(); } }
        public string[] AllTags { get { return GetTags(TagCore).Select(x => x.Name).ToArray(); } }
        public string[] Tags { get { return TagCore.Children.Select(x => x.Name).ToArray(); } }
        public string[] DirectTags(AttachedTag parent)
        {
            return parent.Children.OrderByDescending(x => x.Size)
                                   .ThenByDescending(x => x.Videos.Count)
                                   .Select(x => x.Name).ToArray();
        }

        public TagManager() { TagCore = new AttachedTag(CORE_NAME); }
        public TagManager(AttachedTag tagCore) { TagCore = tagCore; }

        public void Reorganize()
        {
            var tags = RawTags;
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i].Videos.Count == 0)
                    if (Remove(null, tags[i].Name))
                    {
                        Reorganize();
                        return;
                    }
            }
        }

        public List<string> GetAncestors(string descendantName)
        {
            var tag = FindByName(descendantName);
            if (tag == null) return new List<string>();

            return IGetAncestors(tag).Select(x => x.Name).ToList();
        }

        private List<AttachedTag> IGetAncestors(AttachedTag descendant)
        {
            var ancestors = new List<AttachedTag>();
            if (descendant.Parent != null && descendant.Parent != TagCore)
            {
                ancestors.Add(descendant.Parent);
                ancestors.AddRange(IGetAncestors(descendant.Parent));
            }
            return ancestors;
        }

        public List<string> GetDescendants(string ancestorName)
        {
            return GetTags(FindByName(ancestorName)).Select(x => x.Name).ToList();
        }

        private List<AttachedTag> GetTags(AttachedTag parent)
        {
            List<AttachedTag> tags = new List<AttachedTag>() { parent };
            if (parent.Children == null || parent.Children.Count == 0) return tags;

            for (int i = 0; i < parent.Children.Count; i++) tags.AddRange(GetTags(parent.Children[i]));
            return tags;
        }
        public AttachedTag FindByName(string name) { return IfindByName(TagCore, name); }

        private AttachedTag IfindByName(AttachedTag parent, string name)
        {
            if (parent.Name != null && parent.Name.Equals(name)) return parent;
            else if (parent.Children != null)
            {
                AttachedTag tag = null;
                for (int i = 0; tag == null && i < parent.Children.Count; i++)
                {
                    tag = IfindByName(parent.Children[i], name);
                }
                return tag;
            }
            return null;
        }

        public void Add(AttachedVideo video, string name) { Add(video, TagCore, name); }

        public void Add(AttachedVideo video, AttachedTag parent, string name)
        {
            AttachedTag tag = FindByName(name);
            if (tag == TagCore) return;
            if (tag == null)
            {
                tag = new AttachedTag(parent, name);
                parent.Children.Add(tag);
            }
            lock (tag)
            {
                if (!tag.Videos.Contains(video, new VideoComparer())) tag.Videos.Add(video);
                if (!video.RawTags.Contains(tag, new TagComparer())) video.RawTags.Add(tag);
                if (tag.Parent != null) Add(video, tag.Parent.Name);
            }
        }

        private void RemoveBranches(AttachedVideo video, AttachedTag tag)
        {
            if (video == null) return;
            video.RawTags.Remove(tag);
            foreach (var child in tag.Children)
            {
                if (video.RawTags.Contains(child, new TagComparer())) Remove(video, child.Name);
            }
        }

        public bool Remove(AttachedVideo video, string tagName)
        {
            AttachedTag tag = FindByName(tagName);
            if (tag == null || tag.Parent == null || tag.Videos == null) return false;
            lock (tag)
            {
                if (tag.Videos.Count > 0)
                {
                    tag.Videos.Remove(video);
                    RemoveBranches(video, tag);
                    return true;
                }
                else if (tag.Children != null)
                {
                    RemoveBranches(video, tag);
                    tag.Parent.Children.Remove(tag);
                    tag.Parent.Children.AddRange(tag.Children);
                    for (int i = 0; i < tag.Children.Count; i++)
                        tag.Children[i].Parent = tag.Parent;
                    return true;
                }
                return false;
            }
        }

        public bool Rename(string oldName, string newName)
        {
            AttachedTag aTag = FindByName(oldName ?? "");
            if (aTag == null || newName == null || newName.Trim().Equals("") || aTag.Name.Equals(CORE_NAME)) return false;
            lock (aTag)
            {
                aTag.Name = newName;
                return true;
            }
        }

        internal void Adopt(string tagName, AttachedTag adoptingParent)
        {
            AttachedTag tag = FindByName(tagName);
            if (tag == null) return;
            lock (tag)
            {

                if (!adoptingParent.Children.Contains(tag, new TagComparer()) && tag.Parent != adoptingParent)
                {
                    tag.Parent.Children.Remove(tag);
                    tag.Parent = adoptingParent;
                    adoptingParent.Children.Add(tag);
                    Reorganize();
                }
            }
        }

        internal bool IsChildless(string tag)
        {
            AttachedTag Tag = FindByName(tag);
            if (Tag != null && Tag.Children.Count == 0) return true;
            else return false;
        }

        internal bool InUse(string tagName)
        {
            return FindByName(tagName)?.Videos?.Count == null ? false : 0 > 0;
        }
    }
}