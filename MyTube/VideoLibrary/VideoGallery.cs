using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MyTube.Model;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;

namespace MyTube.VideoLibrary
{
    public class VideoGallery
    {
        public static int Thumbnail_Limit { get { return 5; } }

        public List<AttachedVideo> Videos { get; set; }
        public List<AttachedVideo> UnknownVideos { get; set; }
        private TagManager _tagManager;
        public TagManager TagManager
        {
            get { return _tagManager; }
            set
            {
                _tagManager = value;
                TagManager.Reorganize();
            }
        }
        public string[] Tags { get { return TagManager.AllTags; } }
        public string[] Tags_Sorted { get { return Tags.OrderBy(x => x).ToArray(); } }
        public string[] Tags_Popularity { get { return TagManager.RawTags.OrderByDescending(x => x.Size).Select(x => x.Name).ToArray(); } }

        public VideoGallery(TagManager tagManager)
        {
            TagManager = tagManager;
            Videos = new List<AttachedVideo>();
            UnknownVideos = new List<AttachedVideo>();
        }

        public VideoGallery(TagManager tagManager, List<AttachedVideo> videos)
        {
            TagManager = tagManager;
            UnknownVideos = new List<AttachedVideo>();
            Videos = videos;
        }

        public VideoGallery(List<AttachedVideo> videos, List<AttachedVideo> unknownVideos, TagManager tagManager)
        {
            TagManager = tagManager;
            Videos = videos;
            UnknownVideos = unknownVideos;
            RemoveDuplicates();
            OrderLists();
        }

        private void RemoveDuplicates()
        {
            Debug.WriteLine("Known videos found within unknown list: " + UnknownVideos.RemoveAll(unknown => Videos.Any(video => video.VideoId == unknown.File.DisplayName)));

            Videos = Videos.Distinct(new VideoComparer()).ToList();
        }

        private void OrderLists()
        {
            Videos = Videos.OrderBy(x => x.DocumentedDate).ToList();
            UnknownVideos.OrderBy(x => x.File.DisplayName).ToList();
        }

        public void ClearVideoThumbnails()
        {
            Videos.ForEach(video =>
                video.Thumbnails = null);
        }

        public void CleanVideoThumbnails()
        {
            Videos.ForEach(video =>
                video.Thumbnails = new List<Task<WriteableBitmap>>() { video.Thumbnails.FirstOrDefault() });
        }

        public List<AttachedVideo> GetAllKnownVideos(List<AttachedVideo> exceptions)
        {
            return Videos.Except(exceptions, new VideoComparer()).ToList();
        }

        public AttachedVideo FindVideoById(string id)
        {
            for (int i = 0; i < Videos.Count; i++)
            {
                if (Videos[i].Id.Equals(id)) return Videos[i];
            }
            return null;
        }

        public void ConvertKnownToUnknownVideo(string id)
        {
            AttachedVideo video = FindVideoById(id);
            if (video == null) return;
            AttachedVideo strippedVideo = new AttachedVideo();
            strippedVideo.File = video.File;
            strippedVideo.Parts = video.Parts.Select(x => new TimeSpan[] { new TimeSpan(x[0].Ticks), new TimeSpan(x[1].Ticks) }).ToList();
            UnknownVideos.Add(strippedVideo);
            Videos.Remove(video);
        }

        public void ConvertUnknownToKnownVideo(AttachedVideo newVideo)
        {
            Videos.Add(newVideo);
            UnknownVideos.Remove(newVideo);
        }

        public int FindVideoIndex(AttachedVideo target)
        {
            for (int i = 0; i < Videos.Count; i++)
            {
                if (Videos[i].Id.Equals(target.Id)) return i;
            }
            throw new KeyNotFoundException();
        }

        private bool SearchTags(AttachedVideo video, List<string> tags, SearchProperties properties)
        {
            if (properties == SearchProperties.Inclusive)
            {
                if (video.Tags.Intersect(tags).Any()) return true;
            }
            else if (properties == SearchProperties.Exclusive)
            {
                if (video.Tags.Intersect(tags).Count() == tags.Count) return true;
            }
            return false;
        }

        public List<AttachedVideo> FindVideosByTags(List<string> tags, SearchProperties properties, List<AttachedVideo> exceptions)
        {
            if (tags.Count <= 0) return Videos;

            List<AttachedVideo> videos = new List<AttachedVideo>();
            if (Videos != null)
            {
                foreach (AttachedVideo video in Videos)
                    if (SearchTags(video, tags, properties)) videos.Add(video);
            }

            return videos.Except(exceptions, new VideoComparer()).OrderByDescending<AttachedVideo, int>(v1 => v1.Tags.Intersect(tags).Count()).ToList();
        }

        public List<AttachedVideo> FindVideosByTags(List<string> tags, SearchProperties properties)
        {
            return FindVideosByTags(tags, properties, new List<AttachedVideo>());
        }

        public static string AvailableId(VideoGallery MainGallery)
        {
            bool distinct = false;
            string id = null;
            while (!distinct)
            {
                id = new Random().Next(int.MaxValue).ToString();
                distinct = true;
                foreach (AttachedVideo video in MainGallery.Videos) if (video.Id == id) distinct = false;
            }
            return id;
        }

        public static string AvailableVideoId(VideoGallery MainGallery)
        {
            bool distinct = false;
            string id = null;
            while (!distinct)
            {
                id = new Random().Next(int.MaxValue).ToString();
                distinct = true;
                foreach (AttachedVideo video in MainGallery.Videos) if (video.VideoId == id) distinct = false;
            }
            return id;
        }

        public List<AttachedVideo> FindDuplicates(AttachedVideo video)
        {
            List<AttachedVideo> possibleDuplicates = new List<AttachedVideo>() { video };
            var videoDuration = video.File.Properties.GetVideoPropertiesAsync().AsTask().GetAwaiter().GetResult().Duration;
            possibleDuplicates.AddRange(Videos.FindAll(x =>
            {
                var duration = x.File.Properties.GetVideoPropertiesAsync().AsTask().GetAwaiter().GetResult().Duration;
                return duration == videoDuration && x.VideoId != video.VideoId;
            }));
            return possibleDuplicates;
        }

        public static async void ProcessThumbnails(List<AttachedVideo> allVideos)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                for (int i = 0; i < allVideos.Count; i++)
                {
                    AttachedVideo video = allVideos[i];
                    video.Thumbnails = new List<Task<WriteableBitmap>>() { processThumbnail(new Thumbnail(video.File.GetThumbnailAsync(ThumbnailMode.VideosView).AsTask())) };
                }
            });

        }

        public static void SetInitialThumbnail(AttachedVideo video)
        {
            ProcessThumbnails(new List<AttachedVideo>() { video });
        }

        public static Task<ImageStream> CaptureAsync(AttachedVideo video, TimeSpan timeOfFrame)
        {
            if (video.File == null) return null;

            try
            {
                var clip = MediaClip.CreateFromFileAsync(video.File).AsTask().GetAwaiter().GetResult();
                var composition = new MediaComposition();
                composition.Clips.Add(clip);
                return composition.GetThumbnailAsync(timeOfFrame, video.Width, video.Height, VideoFramePrecision.NearestFrame).AsTask();
            }
            catch (Exception) { return null; }

        }

        public static List<Task<WriteableBitmap>> SetThumbailImages(AttachedVideo video)
        {
            List<Task<WriteableBitmap>> bitmaps = new List<Task<WriteableBitmap>>();

            TimeSpan pointInterval = video.Duration / Thumbnail_Limit;
            List<Task<ImageStream>> imageTasks = new List<Task<ImageStream>>();
            for (int i = 0; i < Thumbnail_Limit; i++)
            {
                imageTasks.Add(CaptureAsync(video, video.StartTime + (pointInterval * i)));
            }
            foreach (var task in imageTasks) bitmaps.Add(processThumbnail(new Thumbnail(task)));
            return bitmaps;
        }

        public static async Task<WriteableBitmap> processThumbnail(Thumbnail thumbnail)
        {
            WriteableBitmap bitmap = new WriteableBitmap(150, 150);
            if (thumbnail.ItemThumbnail != null) await bitmap.SetSourceAsync(await thumbnail.ItemThumbnail);
            else if (thumbnail.Stream != null) await bitmap.SetSourceAsync(await thumbnail.Stream);
            else throw new ArgumentException("Thumbnail image is unknown type");
            return bitmap;
        }

        public override bool Equals(object obj)
        {
            return obj is VideoGallery gallery &&
                   Videos.Count == gallery.Videos.Count && Videos.TrueForAll(v => gallery.FindVideoById(v.Id) != null) &&
                   UnknownVideos.Count == gallery.UnknownVideos.Count && UnknownVideos.Count == gallery.UnknownVideos.Count &&
                   ((TagManager != null && gallery.TagManager != null &&
                   Tags.Length == gallery.Tags.Length && Tags.ToList().TrueForAll(t1 => gallery.TagManager.FindByName(t1) != null)) ||
                   (TagManager == null && gallery.TagManager == null));
        }
    }
}