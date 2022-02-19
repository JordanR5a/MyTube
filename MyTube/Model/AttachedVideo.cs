using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace MyTube.Model
{

    public class AttachedVideo
    {
        public string Id { get; set; }
        public bool IsUnique { get; set; }
        public DateTime DocumentedDate { get; }
        public List<AttachedTag> RawTags { get; set; }
        public string[] Tags { get { return RawTags.Select(x => x.Name).ToArray(); } }
        public List<TimeSpan[]> Parts { get; set; }
        public TimeSpan StartTime { get { return Parts.First()[0]; } set { if (value < Parts.First()[1]) Parts.First()[0] = value; } }
        public TimeSpan EndTime { get { return Parts.Last()[1]; } set { if (value > Parts.Last()[0]) Parts.Last()[1] = value; } }
        public string VideoId { get; set; }
        public StorageFile File { get; set; }
        public TimeSpan Duration
        {
            get
            {
                if (Parts.Count <= 1) return EndTime - StartTime;

                TimeSpan totalTime = TimeSpan.Zero;
                foreach (TimeSpan[] part in Parts)
                {
                    totalTime += part[1] - part[0];
                }
                return totalTime;
            }
        }
        public List<Task<WriteableBitmap>> Thumbnails { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }

        public int CurrentIndex { get; set; } = 0;

        public AttachedVideo() { DocumentedDate = DateTime.UtcNow; }

        public AttachedVideo(DateTime time) { DocumentedDate = time; }
        public AttachedVideo Copy()
        {
            AttachedVideo copy = new AttachedVideo();
            copy.Id = Id;
            copy.RawTags = RawTags;
            copy.Parts = Parts.Select(x => new TimeSpan[2] { new TimeSpan(x[0].Ticks), new TimeSpan(x[1].Ticks) }).ToList();
            copy.VideoId = VideoId;
            copy.File = File;
            copy.Thumbnails = Thumbnails;
            copy.Height = Height;
            copy.Width = Width;
            return copy;
        }

        public bool WithinAnyParts(TimeSpan position)
        {
            if (Parts.Count <= 1) return true;
            foreach (TimeSpan[] part in Parts)
            {
                if (part[0] <= position && part[1] >= position) return true;
            }
            return false;
        }
        public override bool Equals(object obj)
        {
            return obj is AttachedVideo video &&
                   Id == video.Id;
        }
    }
}