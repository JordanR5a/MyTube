using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyTube.Model;
using MyTube.VideoLibrary;
using Windows.Media.Playback;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace MyTube
{
    public sealed partial class VideoViewPage : Page
    {
        private VideoView videoView;
        private State state;

        private TimeSpan position;

        private DispatcherTimer announcementTimer;
        private bool menuEnabled;

        public VideoViewPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
            mediaPlayerElement.MediaPlayer.PlaybackSession.PositionChanged -= UpdateVideoTimer;
            mediaPlayerElement.MediaPlayer.MediaEnded -= VideoEnd;
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
        }

        private void UpdateStartStopBtns()
        {
            if (videoView.Position >= videoView.StartStopPoints[1]) VideoViewSetStartBtn.IsEnabled = false;
            else VideoViewSetStartBtn.IsEnabled = true;

            if (videoView.Position <= videoView.StartStopPoints[0]) VideoViewSetEndBtn.IsEnabled = false;
            else VideoViewSetEndBtn.IsEnabled = true;
        }

        private void UpdateAutoPlayText()
        {
            if (videoView.LoopingEnabled) VideoViewLoopAutoToggleBtn.Content = "Auto-Play";
            else VideoViewLoopAutoToggleBtn.Content = "Loop";
        }

        private void ToggleAutoPlay()
        {
            videoView.ToggleCurrentVideoAutoPlay();
            UpdateAutoPlayText();
        }

        private void NextVideo(bool skipDuplicates)
        {
            menuEnabled = false;
            if (skipDuplicates) videoView.SkipToNextVideo();
            else videoView.PlayNextVideoAsync(null, null);

            if (videoView.CurrentVideo.StartTime != TimeSpan.Zero && !videoView.LoopingEnabled) ResetPlaySession();
            else PlayVideo();
        }

        private void PreviousVideo()
        {
            videoView.PlayPreviousVideoAsync(null, null);
            if (videoView.CurrentVideo.Id != null && videoView.CurrentVideo.StartTime != TimeSpan.Zero) ResetPlaySession();
            else PlayVideo();
        }

        private void PreviousEvent(MediaPlaybackSession session)
        {
            if (videoView.CurrentVideo.Id != null && session.Position > videoView.CurrentVideo.StartTime + TimeSpan.FromSeconds(2)) videoView.SetCurrentPosition(videoView.CurrentVideo.StartTime);
            else if (videoView.CurrentVideo.Id == null && session.Position > TimeSpan.FromSeconds(2)) videoView.SetCurrentPosition(TimeSpan.Zero);
            else PreviousVideo();
        }

        private void FlickVideoFullScreen()
        {
            videoView.FlickCurrentVideoFullScreen();
        }

        private void ToggleVideoMute()
        {
            if (videoView.ToggleCurrentVideoMute()) SendAnnouncement("Video Muted");
            SendAnnouncement("Video Unmuted");
        }

        private void RaiseVideoVolume(double num)
        {
            videoView.RaiseCurrentVideoVolume(num);
            SendAnnouncement((int)Math.Round(videoView.Volume * 100) + "%");
        }

        private void LowerVideoVolume(double num)
        {
            videoView.LowerCurrentVideoVolume(num);
            SendAnnouncement((int)Math.Round(videoView.Volume * 100) + "%");
        }

        private void SaveVideo()
        {
            videoView.CloseVideoPlayer();
            var currPage = App.PageNavigation.CurrentPage["args"] as WindowTransferPackage;
            if (currPage.Parameters.ContainsKey("index")) currPage.Parameters["index"] = videoView.CurrentVideoIndex;
            else currPage.Parameters.Add("index", videoView.CurrentVideoIndex);

            if (state == State.UNKNOWN)
            {
                videoView.CurrentVideo.StartTime = videoView.StartStopPoints[0];
                videoView.CurrentVideo.EndTime = videoView.StartStopPoints[1];
                var dict = new Dictionary<string, object>()
                {
                    { "video", videoView.CurrentVideo },
                    { "tagCore", App.MainVideoGallery.TagManager.TagCore }
                };
                App.PageNavigation.Navigate(Frame, typeof(VideoSavePage), new WindowTransferPackage(state, dict));
            }
            else if (state == State.KNOWN)
            {
                var dict = new Dictionary<string, object>()
                {
                    { "video", videoView.CurrentVideo },
                    { "tagCore", App.MainVideoGallery.TagManager.TagCore }
                };
                App.PageNavigation.Navigate(Frame, typeof(VideoSavePage), new WindowTransferPackage(state, dict));
            }
        }

        private void TrimVideo()
        {
            videoView.CloseVideoPlayer();
            var currPage = App.PageNavigation.CurrentPage["args"] as WindowTransferPackage;
            if (currPage.Parameters.ContainsKey("index")) currPage.Parameters["index"] = videoView.CurrentVideoIndex;
            else currPage.Parameters.Add("index", videoView.CurrentVideoIndex);

            AttachedVideo newVideo = DataManager.CreateNullVideo(videoView.CurrentVideo.File);
            newVideo.Id = VideoGallery.AvailableId(App.MainVideoGallery);
            newVideo.Parts = new List<TimeSpan[]>() { new TimeSpan[] { new TimeSpan(videoView.StartStopPoints[0].Ticks), new TimeSpan(videoView.StartStopPoints[1].Ticks) } };
            if (state == State.UNKNOWN) videoView.ResetCurrentVideoProperties();
            App.PageNavigation.Navigate(Frame, typeof(VideoSavePage), new WindowTransferPackage(State.TRIM,
                new Dictionary<string, object> { { "video", newVideo }, { "tagCore", App.MainVideoGallery.TagManager.TagCore } }));
        }

        private void PartitionVideo()
        {
            if (videoView.CurrentVideo.StartTime >= videoView.Position && videoView.CurrentVideo.EndTime <= videoView.Position)
            {
                SendAnnouncement("Partition Failed");
                return;
            }

            AttachedVideo video = videoView.CurrentVideo;

            if (video.Parts.Count == 1)
            {
                video.Parts = new List<TimeSpan[]>()
                {
                    new TimeSpan[] { video.StartTime, videoView.Position },
                    new TimeSpan[] { videoView.Position, video.EndTime }
                };
                UpdateEventViewer();
                SendAnnouncement("Partition Successful");
            }
            else if (video.Parts[video.Parts.Count - 2][1] < videoView.Position)
            {
                video.Parts[video.Parts.Count - 1] = new TimeSpan[] { videoView.Position, video.EndTime };
                UpdateEventViewer();
                SendAnnouncement("Partition Successful");
            }
            else SendAnnouncement("Partition Failed");

        }

        private void ResetPartitions()
        {
            videoView.PartitionReset();
            UpdateEventViewer();
            SendAnnouncement("Partitions Reset");
        }

        private void DeleteVideo()
        {
            videoView.CloseVideoPlayer();
            AttachedVideo videoToDelete = videoView.CurrentVideo;
            menuEnabled = false;

            bool videoNotAlreadyInUse = App.MainVideoGallery.GetAllKnownVideos(new List<AttachedVideo>() { videoToDelete }).TrueForAll(x => x.VideoId != videoToDelete.File.DisplayName);
            if (videoNotAlreadyInUse) videoView.DeleteVideo();
            else videoView.RemoveVideo();

            if (state == State.UNKNOWN)
            {
                App.MainVideoGallery.UnknownVideos.RemoveAll(x => x.File.DisplayName == videoToDelete.File.DisplayName);
            }
            else if (state == State.KNOWN)
            {
                App.MainVideoGallery.Videos.Remove(videoToDelete);
                App.MainVideoGallery.UnknownVideos.Remove(videoToDelete);
                App.MainDataManager.UpdateCoreData(App.MainVideoGallery, DataManager.DatabaseFileName);
            }

            if (videoView.VideoCount <= 0)
            {
                App.PageNavigation.Back(Frame);
                return;
            }
            else
            {
                if (!videoView.FocusCurrentVideo())
                {
                    App.PageNavigation.Reset(Frame);
                    return;
                }
                else PlayVideo();
            }

            SendAnnouncement("Deletion Successful");
            VideoViewClearStartBtn.Visibility = Visibility.Collapsed;
            VideoViewClearEndBtn.Visibility = Visibility.Collapsed;
            UpdateStartStopBtns();
            UpdateEventViewer();
            menuEnabled = true;
        }

        private void SetMenuState(Visibility visibility)
        {
            VideoViewBackBtn.Visibility = visibility;
            VideoViewEventViewer.Visibility = visibility;

            switch (state)
            {
                case State.UNKNOWN:
                case State.KNOWN:

                    VideoViewRandomizeBtn.Visibility = visibility;
                    VideoViewLoopAutoToggleBtn.Visibility = visibility;
                    VideoViewDeleteBtn.Visibility = visibility;

                    setSaveBtnState(visibility);
                    setStartBtnState(visibility);
                    setEndBtnState(visibility);

                    break;
                case State.PLAYLIST:

                    VideoViewRandomizeBtn.Visibility = visibility;
                    VideoViewLoopAutoToggleBtn.Visibility = visibility;

                    break;
                case State.PARTITION:

                    VideoViewTrimBtn.Visibility = visibility;
                    VideoViewPartitionBtn.Visibility = visibility;
                    VideoViewResetPartitionBtn.Visibility = visibility;

                    setStartBtnState(visibility);
                    setEndBtnState(visibility);

                    break;
            }
        }

        private void setSaveBtnState(Visibility visibility)
        {
            if (videoView.CurrentVideo.Id != null) VideoViewSaveBtn.Content = "Update";
            else VideoViewSaveBtn.Content = "Save";
            VideoViewSaveBtn.Visibility = visibility;
        }

        private void setStartBtnState(Visibility visibility)
        {
            VideoViewSetStartBtn.Visibility = visibility;
            if (videoView.StartStopPoints[0] != TimeSpan.Zero) VideoViewClearStartBtn.Visibility = visibility;
            else VideoViewClearStartBtn.Visibility = Visibility.Collapsed;
        }

        private void setEndBtnState(Visibility visibility)
        {
            VideoViewSetEndBtn.Visibility = visibility;
            if (videoView.StartStopPoints[1] != mediaPlayerElement.MediaPlayer.PlaybackSession.NaturalDuration) VideoViewClearEndBtn.Visibility = visibility;
            else VideoViewClearEndBtn.Visibility = Visibility.Collapsed;
        }

        private void FlickMenuVisibility()
        {
            if (!menuEnabled) return;
            if (VideoViewBackBtn.Visibility == Visibility.Collapsed)
            {
                UpdateEventViewer();
                UpdateAutoPlayText();
                SetMenuState(Visibility.Visible);
                mediaPlayerElement.AreTransportControlsEnabled = true;
                Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
                PauseVideo();
            }
            else
            {
                SetMenuState(Visibility.Collapsed);
                mediaPlayerElement.AreTransportControlsEnabled = false;
                Window.Current.CoreWindow.PointerCursor = null;
                PlayVideo();
            }
        }

        private void UpdateEventViewer()
        {
            VideoViewEventViewer.Children.Clear();

            var windowWidth = VideoViewEventViewer.ColumnDefinitions[1].ActualWidth;
            var fileWidth = mediaPlayerElement.MediaPlayer.PlaybackSession.NaturalDuration;

            if (videoView.CurrentVideo.Parts.Count > 1)
            {
                foreach (var part in videoView.CurrentVideo.Parts)
                {
                    if (videoView.StartStopPoints[0] != TimeSpan.Zero || videoView.StartStopPoints[1] != fileWidth)
                    {
                        var startEventRelativePosition = part[0].TotalMilliseconds / fileWidth.TotalMilliseconds;
                        var endEventRelativePosition = part[1].TotalMilliseconds / fileWidth.TotalMilliseconds;

                        CreateNewEvent(windowWidth * startEventRelativePosition, Color.FromArgb(255, 255, 0, 0));

                        CreateNewEvent(windowWidth * endEventRelativePosition, Color.FromArgb(255, 255, 0, 0));
                    }
                }
            }
            else
            {
                if (videoView.StartStopPoints[0] != TimeSpan.Zero)
                {
                    var startEventRelativePosition = videoView.StartStopPoints[0].TotalMilliseconds / fileWidth.TotalMilliseconds;

                    CreateNewEvent(windowWidth * startEventRelativePosition, Color.FromArgb(255, 255, 0, 0));
                }

                if (videoView.StartStopPoints[1] != fileWidth)
                {
                    var endEventRelativePosition = videoView.StartStopPoints[1].TotalMilliseconds / fileWidth.TotalMilliseconds;

                    CreateNewEvent(windowWidth * endEventRelativePosition, Color.FromArgb(255, 255, 0, 0));
                }
            }
        }

        private void CreateNewEvent(double position, Color color)
        {
            var newEvent = new Border();
            newEvent.Width = 10;
            newEvent.HorizontalAlignment = HorizontalAlignment.Left;
            newEvent.Background = new SolidColorBrush(color);
            newEvent.Opacity = .5;
            newEvent.Margin = new Thickness(position, 0, 0, 0);

            VideoViewEventViewer.Children.Add(newEvent);
            Grid.SetColumn(newEvent, 1);
        }

        private TimeSpan FindNextPartStart(MediaPlaybackSession session, AttachedVideo video)
        {
            TimeSpan nextPartStart = video.StartTime;
            foreach (TimeSpan[] times in video.Parts.ToArray().Reverse())
            {
                if (session.Position < times[0]) nextPartStart = times[0];
            }
            return nextPartStart;
        }

        private async void UpdateVideoTimer(MediaPlaybackSession session, Object obj)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {

                AttachedVideo video;
                if (videoView != null && videoView.VideoCount != 0) video = videoView.CurrentVideo;
                else return;

                if (VideoViewBackBtn.Visibility == Visibility.Visible) UpdateStartStopBtns();

                if (!(videoView.IsLastVideo && videoView.StartStopPoints[1] == mediaPlayerElement.MediaPlayer.PlaybackSession.NaturalDuration))
                {
                    if (state == State.UNKNOWN && session.Position >= videoView.StartStopPoints[1] && menuEnabled)
                    {
                        if (!AnyContentDialogOpen()) DisplayUnknownVideoDialog();
                        return;
                    }
                    else if (state == State.UNKNOWN) return;

                    if (session.Position >= videoView.StartStopPoints[1] && menuEnabled)
                    {
                        NextVideo(false);
                        return;
                    }
                }

                if (!video.WithinAnyParts(session.Position))
                {
                    PauseVideo();
                    videoView.SetCurrentPosition(FindNextPartStart(session, video));
                    PlayVideo();
                }

                if (mediaPlayerElement.Visibility == Visibility.Collapsed) return;
            });

        }

        private async void VideoEnd(MediaPlayer player, Object o)
        {
            if (!videoView.IsLastVideo) return;

            AttachedVideo video;
            if (videoView != null) video = videoView.CurrentVideo;
            else return;

            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (state == State.UNKNOWN && videoView.Position >= videoView.StartStopPoints[1] && menuEnabled)
                {
                    if (!AnyContentDialogOpen()) DisplayUnknownVideoDialog();
                    return;
                }
                else if (state == State.UNKNOWN) return;

                if (videoView.Position >= videoView.StartStopPoints[1] && menuEnabled)
                {
                    NextVideo(false);
                    return;
                }
            });
        }

        private async void DisplayUnknownVideoDialog()
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            menuEnabled = false;
            ContentDialog unknownVIdeoDialog = new ContentDialog
            {
                Title = "Make The Unknown Known?",
                Content = "Decide whether to save or delete this Unknown Video.",
                PrimaryButtonText = "Save",
                SecondaryButtonText = "Delete",
                DefaultButton = ContentDialogButton.Primary
            };

            ContentDialogResult result = await unknownVIdeoDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                ResetPlaySession();
                SaveVideo();
            }
            else if (result == ContentDialogResult.Secondary) DeleteVideo();
            else StartVideo();
            menuEnabled = true;
        }

        private bool AnyContentDialogOpen()
        {
            var openedpopups = VisualTreeHelper.GetOpenPopups(Window.Current);
            foreach (var popup in openedpopups)
            {
                if (popup.Child is ContentDialog) return true;
            }
            return false;
        }

        private void PauseVideo()
        {
            videoView.PauseCurrentVideo();
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
        }

        private void PlayVideo()
        {
            videoView.PlayCurrentVideo();
            Window.Current.CoreWindow.PointerCursor = null;
            menuEnabled = true;
            if (VideoViewBackBtn.Visibility == Visibility.Visible) FlickMenuVisibility();
        }

        private async void StartVideo()
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                mediaPlayerElement.MediaPlayer.PlaybackSession.SeekCompleted += PlaybackSession_SeekCompleted;
                VideoViewEventViewer.Visibility = Visibility.Collapsed;
                if (position == TimeSpan.MaxValue) position = videoView.CurrentVideo.StartTime;
                videoView.SetCurrentPosition(position);
            });
        }

        private async void PlaybackSession_SeekCompleted(MediaPlaybackSession sender, object args)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                mediaPlayerElement.MediaPlayer.PlaybackSession.SeekCompleted -= PlaybackSession_SeekCompleted;
                mediaPlayerElement.Visibility = Visibility.Visible;
                PlayVideo();
            });
        }

        private void ToggleRandomization()
        {
            if (videoView.ToggleRandomVideos()) VideoViewRandomizeBtn.Content = "Organize";
            else VideoViewRandomizeBtn.Content = "Randomize";
            videoView.FocusCurrentVideo();
            ResetPlaySession();
        }

        private void SetCurrentVideoStart()
        {
            if (videoView.SetCurrentVideoStart())
            {
                if (state != State.UNKNOWN && state != State.PARTITION)
                {
                    videoView.CurrentVideo.StartTime = videoView.Position;
                    App.MainDataManager.UpdateCoreData(App.MainVideoGallery, DataManager.DatabaseFileName);
                }
                if (VideoViewBackBtn.Visibility == Visibility.Visible) VideoViewClearStartBtn.Visibility = Visibility.Visible;
                UpdateStartStopBtns();
                UpdateEventViewer();
                SendAnnouncement("Video Start Updated");
            }
            else SendAnnouncement("Action Failed");
        }

        private void ClearVideoStart()
        {
            videoView.StartStopPoints[0] = TimeSpan.Zero;
            VideoViewClearStartBtn.Visibility = Visibility.Collapsed;
            if (state != State.UNKNOWN && state != State.PARTITION)
            {
                videoView.CurrentVideo.StartTime = TimeSpan.Zero;
                App.MainDataManager.UpdateCoreData(App.MainVideoGallery, DataManager.DatabaseFileName);
            }
            UpdateEventViewer();
            SendAnnouncement("Video Start Cleared");
        }

        private void SetCurrentVideoEnd()
        {
            if (videoView.SetCurrentVideoEnd())
            {
                if (state != State.UNKNOWN && state != State.PARTITION)
                {
                    videoView.CurrentVideo.EndTime = videoView.Position;
                    App.MainDataManager.UpdateCoreData(App.MainVideoGallery, DataManager.DatabaseFileName);
                }
                if (VideoViewBackBtn.Visibility == Visibility.Visible) VideoViewClearEndBtn.Visibility = Visibility.Visible;
                UpdateStartStopBtns();
                UpdateEventViewer();
                SendAnnouncement("Video End Updated");
            }
            else SendAnnouncement("Action Failed");
        }

        private void ClearVideoEnd()
        {
            videoView.StartStopPoints[1] = mediaPlayerElement.MediaPlayer.PlaybackSession.NaturalDuration;
            VideoViewClearEndBtn.Visibility = Visibility.Collapsed;
            if (state != State.UNKNOWN && state != State.PARTITION)
            {
                videoView.CurrentVideo.EndTime = mediaPlayerElement.MediaPlayer.PlaybackSession.NaturalDuration;
                App.MainDataManager.UpdateCoreData(App.MainVideoGallery, DataManager.DatabaseFileName);
            }
            UpdateEventViewer();
            SendAnnouncement("Video End Cleared");
        }

        private void FastForwardVideo(TimeSpan amount, bool shadowCurrState)
        {
            videoView.FastForwardCurrentVideo(amount);
            if (!shadowCurrState && VideoViewBackBtn.Visibility == Visibility.Collapsed)
            {
                PauseVideo();
                FlickMenuVisibility();
            }
        }

        private void RewindVideo(TimeSpan amount, bool shadowCurrState)
        {
            videoView.RewindCurrentVideo(amount);
            if (!shadowCurrState && VideoViewBackBtn.Visibility == Visibility.Collapsed)
            {
                PauseVideo();
                FlickMenuVisibility();
            }
        }

        private void ResetPlaySession()
        {
            Window.Current.CoreWindow.PointerCursor = null;
            menuEnabled = false;
            mediaPlayerElement.Visibility = Visibility.Collapsed;
            PauseVideo();
            position = videoView.CurrentVideo.StartTime;
            Task.Delay(500).ContinueWith(t => TimerTick(null, null));
        }

        private void SendAnnouncement(string message)
        {
            announcementTimer.Interval = TimeSpan.FromSeconds(.5);
            announcementTimer.Tick += AnnouncementTimer_Tick;

            VideoViewAnnouncementText.Text = message;
            VideoViewAnnouncementBorder.Opacity = 1;
            VideoViewAnnouncementBorder.Visibility = Visibility.Visible;
            announcementTimer.Start();
        }

        private void AnnouncementTimer_Tick(object sender, object e)
        {
            if (announcementTimer.Interval == TimeSpan.FromSeconds(.5))
            {
                announcementTimer.Interval = TimeSpan.FromMilliseconds(50);
            }
            else if (VideoViewAnnouncementBorder.Opacity > 0)
            {
                VideoViewAnnouncementBorder.Opacity -= .1;
            }
            else
            {
                VideoViewAnnouncementBorder.Visibility = Visibility.Collapsed;
                announcementTimer.Stop();
            }
        }

        private async void TimerTick(object o, Object e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!AnyContentDialogOpen() &&
                videoView.CurrentVideo.EndTime != TimeSpan.Zero &&
                videoView.Position != mediaPlayerElement.MediaPlayer.PlaybackSession.NaturalDuration)
                {
                    if (mediaPlayerElement.Visibility == Visibility.Visible) PlayVideo();
                    else if (mediaPlayerElement.Visibility == Visibility.Collapsed || !menuEnabled) StartVideo();
                }
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            WindowTransferPackage package = e.Parameter as WindowTransferPackage;
            state = package.State;

            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.CoreWindow.PointerCursor = null;

            menuEnabled = false;
            announcementTimer = new DispatcherTimer();

            if (state == State.UNKNOWN)
            {
                VideoGallery videoGallery = package.Parameters["videoGallery"] as VideoGallery;
                List<AttachedVideo> allVideos = videoGallery.GetAllKnownVideos();
                //int startIndex = videoGallery.UnknownVideos.FindIndex(x => allVideos.Any(j => x.File.DisplayName == j.VideoId));
                int startIndex = package.Parameters.ContainsKey("index") ? (int)package.Parameters["index"] : 0;
                if (startIndex == 0) startIndex = new Random().Next(videoGallery.UnknownVideos.Count - 1);
                videoView = new VideoView(mediaPlayerElement, videoGallery.UnknownVideos.ToList(), startIndex);
                position = package.Parameters.ContainsKey("position") ? (TimeSpan)package.Parameters["position"] : TimeSpan.Zero;
            }
            else if (state == State.KNOWN || state == State.PARTITION || state == State.PLAYLIST)
            {

                List<AttachedVideo> videoList;
                if (state == State.PARTITION) videoList = new List<AttachedVideo>() { package.Parameters["video"] as AttachedVideo };
                else videoList = package.Parameters["videos"] as List<AttachedVideo>;

                videoView = new VideoView(mediaPlayerElement, videoList.ToList(), package.Parameters.ContainsKey("index") ? (int)package.Parameters["index"] : 0);
                if (state == State.PARTITION) ToggleAutoPlay();

                if (package.Parameters.ContainsKey("position") && (TimeSpan)package.Parameters["position"] >= TimeSpan.Zero)
                {
                    position = (TimeSpan)package.Parameters["position"];
                }
                else position = videoView.CurrentVideo.StartTime;
            }

            if (!videoView.FocusCurrentVideo())
            {
                App.PageNavigation.Reset(Frame);
                return;
            }

            mediaPlayerElement.MediaPlayer.PlaybackSession.PositionChanged += UpdateVideoTimer;
            mediaPlayerElement.MediaPlayer.MediaEnded += VideoEnd;
            Task.Delay(500).ContinueWith(t => TimerTick(null, null));
        }

        private void CoreWindow_KeyDown(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.KeyEventArgs e)
        {
            if (Window.Current.CoreWindow.GetKeyState(VirtualKey.LeftControl).HasFlag(CoreVirtualKeyStates.Down) && e.VirtualKey == VirtualKey.Right && menuEnabled) FastForwardVideo(TimeSpan.FromMilliseconds(250), false);
            else if (Window.Current.CoreWindow.GetKeyState(VirtualKey.LeftControl).HasFlag(CoreVirtualKeyStates.Down) && e.VirtualKey == VirtualKey.Left && menuEnabled) RewindVideo(TimeSpan.FromMilliseconds(250), false);
            else if (Window.Current.CoreWindow.GetKeyState(VirtualKey.LeftShift).HasFlag(CoreVirtualKeyStates.Down) && e.VirtualKey == VirtualKey.Right && menuEnabled && state == State.PLAYLIST) NextVideo(true);
            else if (Window.Current.CoreWindow.GetKeyState(VirtualKey.LeftShift).HasFlag(CoreVirtualKeyStates.Down) && e.VirtualKey == VirtualKey.Right && menuEnabled) NextVideo(true);
            else if (Window.Current.CoreWindow.GetKeyState(VirtualKey.LeftShift).HasFlag(CoreVirtualKeyStates.Down) && e.VirtualKey == VirtualKey.Left && menuEnabled) PreviousEvent(mediaPlayerElement.MediaPlayer.PlaybackSession);
            else if (e.VirtualKey == VirtualKey.Right && menuEnabled) FastForwardVideo(TimeSpan.FromSeconds(5), true);
            else if (e.VirtualKey == VirtualKey.Left && menuEnabled) RewindVideo(TimeSpan.FromSeconds(5), true);
            else if (e.VirtualKey == VirtualKey.Up && menuEnabled) RaiseVideoVolume(.1);
            else if (e.VirtualKey == VirtualKey.Down && menuEnabled) LowerVideoVolume(.1);
            else if (e.VirtualKey == VirtualKey.F && menuEnabled) FlickVideoFullScreen();
            else if (e.VirtualKey == VirtualKey.S && menuEnabled && state != State.PARTITION && state != State.PLAYLIST) SaveVideo();
            else if (e.VirtualKey == VirtualKey.S && menuEnabled && state == State.PARTITION) TrimVideo();
            else if (e.VirtualKey == VirtualKey.R && menuEnabled && state != State.PARTITION) ToggleRandomization();
            else if (e.VirtualKey == VirtualKey.D && menuEnabled && state != State.PARTITION && state != State.PLAYLIST) DeleteVideo();
            else if (e.VirtualKey == VirtualKey.L && menuEnabled && state != State.PARTITION) ToggleAutoPlay();
            else if (e.VirtualKey == VirtualKey.M && menuEnabled) ToggleVideoMute();
            else if (e.VirtualKey == VirtualKey.Escape && menuEnabled) FlickMenuVisibility();
            else if (e.VirtualKey == VirtualKey.Escape && !menuEnabled) ResetPlaySession();
            else if (e.VirtualKey == VirtualKey.Space && state == State.PLAYLIST) NextVideo(true);
            else if (e.VirtualKey == VirtualKey.Space && menuEnabled) RewindVideo(TimeSpan.FromSeconds(15), true);
        }

        private void VideoViewBackBtn_Click(object sender, RoutedEventArgs e) { App.PageNavigation.Back(Frame); }

        private void VideoViewSaveVideo_Click(object sender, RoutedEventArgs e) { SaveVideo(); }

        private void VideoViewDeleteBtn_Click(object sender, RoutedEventArgs e) { DeleteVideo(); }

        private void VideoViewRandomizeBtn_Click(object sender, RoutedEventArgs e) { ToggleRandomization(); }

        private void VideoViewSetStartBtn_Click(object sender, RoutedEventArgs e) { SetCurrentVideoStart(); }

        private void VideoViewSetEndBtn_Click(object sender, RoutedEventArgs e) { SetCurrentVideoEnd(); }

        private void VideoViewLoopAutoToggleBtn_Click(object sender, RoutedEventArgs e) { ToggleAutoPlay(); }

        private void VideoViewPartitionBtn_Click(object sender, RoutedEventArgs e) { PartitionVideo(); }

        private void VideoViewTrimBtn_Click(object sender, RoutedEventArgs e) { TrimVideo(); }

        private void VideoViewClearStartBtn_Click(object sender, RoutedEventArgs e) { ClearVideoStart(); }

        private void VideoViewClearEndBtn_Click(object sender, RoutedEventArgs e) { ClearVideoEnd(); }

        private void VideoViewResetPartitionBtn_Click(object sender, RoutedEventArgs e) { ResetPartitions(); }
    }
}