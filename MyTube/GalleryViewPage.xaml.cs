using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MyTube.Model;
using MyTube.VideoLibrary;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


namespace MyTube
{
    public sealed partial class GalleryViewPage : Page
    {
        private GalleryView galleryView;
        private DispatcherTimer thumbnailTimer;
        private State state;

        private AttachedVideo highlightedVideo;
        private int highlightedVideoIndex;

        private DispatcherTimer timer;
        private object[] hoveredItem;

        public GalleryViewPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            thumbnailTimer.Stop();
            galleryView.ClearNulls();
            galleryView.ContinueLoadingThumbnails = false;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            WindowTransferPackage package = e.Parameter as WindowTransferPackage;
            state = package.State;

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += TimerTick;

            thumbnailTimer = new DispatcherTimer();
            thumbnailTimer.Interval = TimeSpan.FromSeconds(1);
            thumbnailTimer.Tick += CheckIfAllThumbnailsLoaded;

            VideoGallery gallery = null;
            if (state == State.DEFAULT)
            {
                gallery = package.Parameters["videoGallery"] as VideoGallery;
                GalleryViewPageChangeTags.Visibility = Visibility.Visible;
            }
            else if (state == State.SEARCH)
            {
                gallery = new VideoGallery(App.MainVideoGallery.TagManager, package.Parameters["videos"] as List<AttachedVideo>);
                List<string> tags = package.Parameters["tags"] as List<string>;

                StringBuilder tagsStr = new StringBuilder();

                int i = 0;
                while (tags.Count > i && tags[i].Length + tagsStr.Length < 115)
                {
                    tagsStr.Append(tags.ElementAt(i));
                    tagsStr.Append(" | ");
                    i++;
                }
                if (tagsStr.Length > 3) tagsStr.Remove(tagsStr.Length - 3, 3);

                GalleryViewAnnouncement.Text = tagsStr.ToString();
            }
            else throw new Exception("No acceptable state");

            galleryView = new GalleryView(display,
                                    gallery,
                                    InspectItem,
                                    FocusItem,
                                    PointerOverVideo,
                                    PointerExitedVideo);

            _ = galleryView.DisplayVideos(15, false);
            thumbnailTimer.Start();
        }


        private void CheckIfAllThumbnailsLoaded(object sender, object e)
        {
            bool allUpdated = true;
            foreach (string btnName in galleryView.videoIds)
            {
                Button btn = (Button)FindName(btnName);
                if (btn.Background == null || btn.Background.GetType() != typeof(ImageBrush)) allUpdated = false;
            }
            if (allUpdated) thumbnailTimer.Stop();
        }

        private void TimerTick(object o, Object e)
        {
            if (hoveredItem[0] is AttachedVideo) galleryView.SetVideoToNextThumbnail(hoveredItem[0] as AttachedVideo, hoveredItem[1] as string);
        }

        internal void PointerExitedVideo(object sender, PointerRoutedEventArgs e)
        {
            if (thumbnailTimer == null || thumbnailTimer.IsEnabled || galleryView.VideoGallery == null) return;

            Button videoBtn = (Button)sender;
            if (!videoBtn.IsPointerOver)
            {
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Stop();
                var index = int.Parse(videoBtn.Name);
                if (index < galleryView.VideoGallery.Videos.Count)
                {
                    _ = galleryView.SetVideoToInitialThumbnail(galleryView.VideoGallery.Videos[index], videoBtn.Name);
                }
            }
        }

        internal void PointerOverVideo(object sender, PointerRoutedEventArgs e)
        {
            if (thumbnailTimer == null || thumbnailTimer.IsEnabled || galleryView.VideoGallery == null) return;

            Button videoBtn = (Button)sender;
            if (videoBtn.IsPointerOver)
            {
                hoveredItem = new object[] { galleryView.VideoGallery.Videos[int.Parse(videoBtn.Name)], videoBtn.Name };
                timer.Interval = TimeSpan.FromMilliseconds(500);
                timer.Start();

            }
        }

        private void FocusVideo(int videoIndex, TimeSpan position)
        {
            var dict = new Dictionary<string, object>()
                {
                    { "videos", galleryView.GetVideos() },
                    { "index", videoIndex },
                    { "position", position }
                };

            App.PageNavigation.Navigate(Frame, typeof(VideoViewPage), new WindowTransferPackage(State.KNOWN, dict));
        }

        public void FocusItem(object sender, RoutedEventArgs e)
        {
            string itemIndex = ((Button)sender).Name;
            FocusVideo(int.Parse(itemIndex), TimeSpan.FromSeconds(-1));
        }

        private string GetVideoTags(AttachedVideo video, bool ignoreCategories)
        {
            StringBuilder tags = new StringBuilder();
            for (int i = 0; i < video.Tags.Length; i++)
            {
                if (!(ignoreCategories && !App.MainVideoGallery.TagManager.IsChildless(video.Tags[i])))
                {
                    tags.Append(video.Tags[i]);
                    tags.Append(" | ");
                }
            }
            if (tags.Length > 0) tags.Remove(tags.Length - 3, 3);
            return tags.ToString();
        }

        public void InspectVideo(int index)
        {
            AttachedVideo video = galleryView.GetVideo(index);
            if (highlightedVideo == null || !(highlightedVideo.Id == video.Id))
            {
                highlightedVideo = video;
                highlightedVideoIndex = index;
                if (state == State.DEFAULT) GalleryViewPageInspectTitle.Text = "Uncategorized " + (index + 1);
                else GalleryViewPageInspectTitle.Text = "Video " + (index + 1);
                GalleryViewPageInspectTags.Text = GetVideoTags(highlightedVideo, false);
                GalleryViewPageInspectDuration.Text = highlightedVideo.Duration.ToString().Substring(0, 8);
            }
        }

        public void InspectItem(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            string itemIndex = btn.Name;
            InspectVideo(int.Parse(itemIndex));
        }

        private void GalleryViewPageBackBtn_Click(object sender, RoutedEventArgs e) { App.PageNavigation.Back(Frame); }

        private void GalleryViewPageChangeTags_Click(object sender, RoutedEventArgs e)
        {
            App.PageNavigation.Navigate(Frame, typeof(VideoSavePage),
                new WindowTransferPackage(State.SEARCH, new Dictionary<string, object>()
                {
                    { "videoGallery", galleryView.VideoGallery },
                    { "tagCore", App.MainVideoGallery.TagManager.TagCore }
                }));
        }
    }
}