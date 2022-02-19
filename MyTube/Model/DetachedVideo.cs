using System.Collections.Generic;

namespace MyTube.Model
{
    public class DetachedVideo
    {
        public string Id { get; set; }
        public long DocumentedDate { get; set; }
        public string[] Tags { get; set; }
        public List<string[]> Parts { get; set; }
        public string videoId;
        public int Width { get; set; }
        public int Height { get; set; }
    }
}