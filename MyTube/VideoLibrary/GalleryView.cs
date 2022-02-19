using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyTube.Model;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace MyTube.VideoLibrary
{
    class GalleryView
    {
        public static readonly int THUMBNAIL_LIMIT = 20;

        private StackPanel display;
        private VideoGallery videoGallery;
        private RoutedEventHandler onThumbnailClick;
        private DoubleTappedEventHandler onThumbnailDoubleTapped;
        private PointerEventHandler onPointerEnterThumbnail;
        private PointerEventHandler onPointerExitThumbnail;

        public VideoGallery VideoGallery { get { return videoGallery; } set { videoGallery = value; } }
        public List<string> videoIds;
        public bool ContinueLoadingThumbnails = true;

        public GalleryView(StackPanel display,
                            VideoGallery videoGallery,
                            RoutedEventHandler onThumbnailClick,
                            DoubleTappedEventHandler onThumbnailDoubleTapped,
                            PointerEventHandler onPointerEnterThumbnail,
                            PointerEventHandler onPointerExitThumbnail)
        {
            this.display = display;
            this.videoGallery = videoGallery;
            this.onThumbnailClick = onThumbnailClick;
            this.onThumbnailDoubleTapped = onThumbnailDoubleTapped;
            this.onPointerEnterThumbnail = onPointerEnterThumbnail;
            this.onPointerExitThumbnail = onPointerExitThumbnail;
        }

        public List<AttachedVideo> GetVideos()
        {
            return videoGallery.Videos;
        }

        public AttachedVideo GetVideo(int index)
        {
            return videoGallery.Videos.ElementAt(index);
        }

        public void RemoveVideo(AttachedVideo video)
        {
            videoGallery.Videos.Remove(video);
        }

        public void NullVideo(AttachedVideo video, Button btn)
        {
            videoGallery.Videos[videoGallery.Videos.FindIndex(x => x.Id == video.Id)] = new AttachedVideo();
            btn.IsEnabled = false;
            btn.DoubleTapped -= onThumbnailDoubleTapped;
            btn.PointerEntered -= onPointerEnterThumbnail;
            btn.PointerExited -= onPointerExitThumbnail;
        }

        public void ClearNulls()
        {
            videoGallery.Videos.RemoveAll(x => x.Id == null);
        }

        private bool AllThumbnailsPresent()
        {
            var vids = videoGallery.Videos.ToList();
            foreach (var v in vids) if (v.Thumbnails == null) return false;
            return true;
        }

        public async Task DisplayVideos(int videoImageScale, bool flyoutEnabled)
        {

            display.Children.Clear();
            videoIds = UpdateGridXML(videoImageScale, flyoutEnabled);

            if (AllThumbnailsPresent())
                for (int i = 0; i < videoGallery.Videos.Count; i++)
                    await SetVideoToInitialThumbnail(videoGallery.Videos.ElementAt(i), videoIds.ElementAt(i));
            else
            {
                IDisplayVideos(0);
            }

        }

        private async void IDisplayVideos(int index)
        {
            if (!ContinueLoadingThumbnails) return;

            try
            {
                int startIndex = index;
                int endIndex = index + THUMBNAIL_LIMIT < videoGallery.Videos.Count ? index + THUMBNAIL_LIMIT : videoGallery.Videos.Count;
                while (index < endIndex)
                {
                    if (videoGallery.Videos.ElementAt(index).Thumbnails == null) await Task.Delay(50).ContinueWith(async t => { await SetVideoToInitialThumbnail(videoGallery.Videos.ElementAt(index), videoIds.ElementAt(index)); });
                    else await SetVideoToInitialThumbnail(videoGallery.Videos.ElementAt(index), videoIds.ElementAt(index));
                    index++;
                }

                var tasks = videoGallery.Videos.GetRange(startIndex, endIndex - startIndex).Select(v => v.Thumbnails?.FirstOrDefault()).Where(t => t != null);
                await Task.WhenAll(tasks).ContinueWith(t => { if (index < videoGallery.Videos.Count) IDisplayVideos(index); });
            }
            catch (ArgumentOutOfRangeException) { }
            catch (ArgumentException) { }

        }

        private void GetGridDefXML(int size, ref Grid grid, int videoImageScale)
        {

            for (int i = 0; i < 10; i++)
            {
                ColumnDefinition col = new ColumnDefinition();
                col.Width = new GridLength(1, GridUnitType.Star);
                grid.ColumnDefinitions.Add(col);
            }

            double rowHeight = Window.Current.Bounds.Width / videoImageScale;


            for (int i = 0; i < (size / 10) + 1; i++)
            {
                RowDefinition row = new RowDefinition();
                row.Height = new GridLength(rowHeight);
                grid.RowDefinitions.Add(row);

            }

        }

        private List<string> UpdateGridXML(int videoImageScale, bool flyoutEnabled)
        {
            Grid grid = new Grid();

            GetGridDefXML(videoGallery.Videos.Count, ref grid, videoImageScale);

            List<string> videos = new List<string>();
            for (int i = 0; i < videoGallery.Videos.Count; i++)
            {
                Button video = new Button();
                video.HorizontalContentAlignment = HorizontalAlignment.Center;
                video.VerticalContentAlignment = VerticalAlignment.Center;
                string id = i.ToString();
                Grid.SetColumn(video, (i) % 10);
                Grid.SetRow(video, (i) / 10);
                video.Name = id;
                video.HorizontalAlignment = HorizontalAlignment.Stretch;
                video.VerticalAlignment = VerticalAlignment.Stretch;
                video.Margin = new Thickness(5);
                video.Click += onThumbnailClick;
                video.DoubleTapped += onThumbnailDoubleTapped;
                video.PointerEntered += onPointerEnterThumbnail;
                video.PointerExited += onPointerExitThumbnail;
                grid.Children.Add(video);
                Grid.SetColumn(video, i % 10);
                Grid.SetRow(video, i / 10);
                videos.Add(id);
            }

            display.Children.Add(grid);

            return videos;
        }

        private async Task SetVideoThumbnail(AttachedVideo video, Button btn)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (btn == null) return;
                try
                {
                    var brush = new ImageBrush();
                    if (video != null && video.Thumbnails != null && video.Thumbnails.Count > video.CurrentIndex && video.Thumbnails[video.CurrentIndex].IsCompletedSuccessfully)
                    {
                        btn.Content = "";
                        brush.ImageSource = video.Thumbnails[video.CurrentIndex].Result;
                        btn.Background = brush;
                        var resources = new ResourceDictionary();
                        resources["ButtonBackgroundPointerOver"] = brush;
                        resources["ButtonBackgroundPointerPressed"] = brush;
                        btn.Resources.ThemeDictionaries["Dark"] = resources;
                    }
                    else if (video?.Thumbnails?.First().Status == TaskStatus.WaitingForActivation ||
                                video?.Thumbnails?.First().Status == TaskStatus.Running ||
                                video?.Thumbnails?.First().Status == TaskStatus.WaitingToRun ||
                                video?.Thumbnails?.First().Status == TaskStatus.WaitingForChildrenToComplete)
                    {
                        await video.Thumbnails.First()
                            .ContinueWith(async t => { await SetVideoThumbnail(video, btn); });
                    }
                    else if (video?.Thumbnails == null || video?.Thumbnails?.First().Status == TaskStatus.Faulted || (btn?.Background?.GetType() != typeof(ImageBrush) && video?.Thumbnails?.First().Status != TaskStatus.WaitingForActivation))
                    {
                        VideoGallery.SetInitialThumbnail(video);
                        await SetVideoThumbnail(video, btn);
                    }
                }
                catch (InvalidOperationException) { }
            });


        }

        public async Task SetVideoToInitialThumbnail(AttachedVideo video, string btnId)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                Button btn = (Button)display.FindName(btnId);
                video.CurrentIndex = 0;
                await SetVideoThumbnail(video, btn);
            });
        }

        public async void SetVideoToNextThumbnail(AttachedVideo video, string btnId)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Button btn = (Button)display.FindName(btnId);
                var task = Task.WhenAny(video.Thumbnails);
                if (task.IsCompletedSuccessfully && video.Thumbnails.Count == 1)
                {
                    try { video.Thumbnails.AddRange(VideoGallery.SetThumbailImages(video)); }
                    catch (OutOfMemoryException)
                    {
                        _ = SetVideoToInitialThumbnail(video, btnId);
                        return;
                    }
                }
                video.CurrentIndex = video.CurrentIndex + 1 < video.Thumbnails.Count ? video.CurrentIndex + 1 : 0;
                _ = SetVideoThumbnail(video, btn);
            });
        }
    }
}