using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyTube.Model;
using MyTube.VideoLibrary;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;


namespace MyTube
{
    public sealed partial class VideoSavePage : Page
    {

        private static SolidColorBrush _Selected = new SolidColorBrush(Color.FromArgb(25, 200, 200, 200));
        private static SolidColorBrush _Unselected = new SolidColorBrush(Color.FromArgb(100, 200, 200, 200));

        private VideoGallery videoGallery;
        private State state;

        private List<string> availableTags;
        private List<string> selectedTags;
        private AttachedTag tagCore;

        private SearchProperties searchProperties;
        private AttachedVideo videoToSave;
        private WindowTransferPackage package;
        private AttachedTag adoptingParent;

        public VideoSavePage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (e.SourcePageType != typeof(VideoSavePage)) App.SelectedTags = null;
            else if (state != State.ADOPTION) App.SelectedTags = selectedTags;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            package = e.Parameter as WindowTransferPackage;
            state = package.State;
            tagCore = package.Parameters["tagCore"] as AttachedTag;
            videoGallery = App.MainVideoGallery;

            if (state == State.KNOWN || state == State.UNKNOWN || state == State.TRIM)
            {
                videoToSave = package.Parameters["video"] as AttachedVideo;

                if (videoToSave.File.Properties.GetVideoPropertiesAsync().AsTask().GetAwaiter().GetResult().Duration == TimeSpan.Zero)
                {
                    App.PageNavigation.Back(Frame);
                    return;
                }
                else if (state == State.UNKNOWN || state == State.TRIM)
                {
                    if (!int.TryParse(videoToSave.File.DisplayName, out int temp)) VideoSaveUnknownVideoInfo.Visibility = Visibility.Visible;
                    VideoSaveUnknownVideoInfoText.Text = videoToSave.File.DisplayName;
                }

                if (state == State.TRIM) VideoSavePagePeekBtn.Visibility = Visibility.Collapsed;
                selectedTags = videoToSave.Tags?.ToList() ?? new List<string>();

                if (state == State.UNKNOWN && !videoToSave.IsUnique)
                {
                    Task.Run(() => videoGallery.FindDuplicates(videoToSave)).ContinueWith((possibleDuplicates) =>
                    {
                        if (possibleDuplicates.Result.Count > 1) DisplayDuplicatesDetectedDialog(possibleDuplicates.Result);
                        videoToSave.IsUnique = true;
                        OpenPage();
                    }).ConfigureAwait(false);
                }
                else OpenPage();
            }
            else if (state == State.ADOPTION)
            {
                SetUpAdoptMenu();
                selectedTags = new List<string>();
                adoptingParent = package.Parameters["adoptingParent"] as AttachedTag;
                OpenPage();
            }
            else if (state == State.SEARCH)
            {
                SetUpSearchMenu();
                selectedTags = App.SelectedTags ?? new List<string>();
                selectedTags.AddRange(package.Parameters.GetValueOrDefault("selectedTags") as List<string> ?? new List<string>());
                selectedTags = selectedTags.Distinct().ToList();
                OpenPage();
            }
            else throw new Exception();

            PopulateTagsGrid(VideoSaveTagsSearch.Text);
            VideoSaveTagsSearch.TextChanged += VideoSaveTagsSearch_TextChanged;
        }

        private async void OpenPage()
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {

                VideoSavePageLoading.Visibility = Visibility.Collapsed;
                VideoSavePageCore.Visibility = Visibility.Visible;

            });
        }

        private void SetUpAdoptMenu()
        {
            VideoSaveNewTagName.Visibility = Visibility.Collapsed;
            VideoSaveNewTag.Visibility = Visibility.Collapsed;
            VideoSavePagePeekBtn.Visibility = Visibility.Collapsed;
            VideoSaveAdoptBtn.Visibility = Visibility.Collapsed;
        }

        private void SetUpSearchMenu()
        {
            VideoSaveAdoptBtn.Visibility = Visibility.Collapsed;
            VideoSaveNewTagName.Visibility = Visibility.Collapsed;
            VideoSaveNewTag.Visibility = Visibility.Collapsed;
            VideoSavePagePeekBtn.Visibility = Visibility.Collapsed;

            VideoSaveToggleTagInclusive.Visibility = Visibility.Visible;
            VideoSaveToggleTagInclusive.Scale = new System.Numerics.Vector3(2);

            searchProperties = SearchProperties.Inclusive;
            VideoSaveSubmitBtn.Content = "Search";
        }

        private async void DisplayDuplicatesDetectedDialog(List<AttachedVideo> possibleDuplicates)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                ContentDialog duplicatesDialog = new ContentDialog
                {
                    Title = "Possible Duplicates Detected",
                    PrimaryButtonText = "Check",
                    DefaultButton = ContentDialogButton.Primary,
                    CloseButtonText = "Ignore"
                };

                ContentDialogResult result = await duplicatesDialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    var dict = new Dictionary<string, object>()
                {
                    { "videoGallery", new VideoGallery(App.MainVideoGallery.TagManager, possibleDuplicates) }
                };
                    App.PageNavigation.Navigate(Frame, typeof(GalleryViewPage), new WindowTransferPackage(State.DEFAULT, dict));
                }
            });
        }

        private void PopulateTagsGrid(string searchContent)
        {
            VideoSaveTagsDisplay.Children.Clear();

            UpdateTagsList(!searchContent.Trim().Equals(""));
            foreach (string tagName in availableTags)
            {
                if (searchContent.Trim().Equals("") || tagName.ToLower().Contains(searchContent.Trim().ToLower()))
                {
                    Button tag = new Button();
                    tag.Name = tagName;
                    tag.CornerRadius = new CornerRadius(25);
                    tag.Margin = new Thickness(10);
                    if (selectedTags.Contains(tagName)) tag.Background = _Selected;
                    else tag.Background = _Unselected;
                    tag.AddHandler(PointerReleasedEvent, new PointerEventHandler(ToggleTag_Click), true);
                    tag.Content = tagName;
                    tag.FontSize = 25;
                    VideoSaveTagsDisplay.Children.Add(tag);
                }
            }
        }

        private void FocusTag(Button tagBtn, AttachedTag tag)
        {
            if (videoToSave != null && !selectedTags.Contains(tag.Name) && videoGallery.TagManager.InUse(tag.Name))
            {
                selectedTags.Add(tag.Name);
                videoGallery.TagManager.Add(videoToSave, tagCore, tag.Name);
                tagBtn.Background = _Selected;
            }

            var dict = new Dictionary<string, object>();
            foreach (var item in package.Parameters) dict.Add(item.Key, item.Value);
            dict["tagCore"] = tag;

            if (!dict.ContainsKey("selectedTags")) dict.Add("selectedTags", selectedTags);
            else dict["selectedTags"] = selectedTags;

            WindowTransferPackage transferPackage = new WindowTransferPackage(package.State, dict);
            App.PageNavigation.Navigate(Frame, typeof(VideoSavePage), transferPackage);
        }

        private void TagAltClick(Button tagBtn, string tagName)
        {
            var tag = videoGallery.TagManager.FindByName(tagName);
            if (tag.Children.Count == 0 && tag.Videos.Count != 0) DisplayFocusTagDialog(tagBtn, tag);
            else FocusTag(tagBtn, tag);
        }

        private async void DisplayFocusTagDialog(Button tagBtn, AttachedTag tag)
        {

            ContentDialog renamingTagDialog = new ContentDialog
            {
                Title = "Focus Tag",
                Content = "This tag has no focusable children, do you wish to view its related videos instead?",
                PrimaryButtonText = "Yes",
                SecondaryButtonText = "Continue Anyway",
                CloseButtonText = "Go Back",
                DefaultButton = ContentDialogButton.Primary
            };

            ContentDialogResult result = await renamingTagDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var dict = new Dictionary<string, object>()
                {
                    { "videoGallery", new VideoGallery(App.MainVideoGallery.TagManager, tag.Videos) }
                };
                App.PageNavigation.Navigate(Frame, typeof(GalleryViewPage), new WindowTransferPackage(State.DEFAULT, dict));
            }
            else if (result == ContentDialogResult.Secondary) FocusTag(tagBtn, tag);

        }

        private async void DisplayRenameTagDialog(string oldTag)
        {
            TextBox inputTextBox = new TextBox();
            inputTextBox.AcceptsReturn = false;
            inputTextBox.Height = 32;
            inputTextBox.Text = oldTag;
            inputTextBox.Focus(FocusState.Programmatic);

            ContentDialog renamingTagDialog = new ContentDialog
            {
                Title = "Rename Tag",
                Content = inputTextBox,
                PrimaryButtonText = "Submit",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Primary
            };

            ContentDialogResult result = await renamingTagDialog.ShowAsync();

            if (result == ContentDialogResult.Primary && videoGallery.TagManager.Rename(oldTag, inputTextBox.Text.Trim()))
            {
                int index = selectedTags.FindIndex(x => x.Equals(oldTag));
                if (index >= 0) selectedTags[index] = inputTextBox.Text.Trim();
                App.MainDataManager.UpdateCoreData(videoGallery, DataManager.DatabaseFileName);
                PopulateTagsGrid(VideoSaveTagsSearch.Text);
            }
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

        private void ToggleTag_Click(object sender, PointerRoutedEventArgs e)
        {
            Button tagBtn = (Button)sender;
            string tagName = tagBtn.Name;

            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                var p = e.GetCurrentPoint((UIElement)sender);
                if (p.Properties.PointerUpdateKind == Windows.UI.Input.PointerUpdateKind.LeftButtonReleased
                    && Window.Current.CoreWindow.GetKeyState(VirtualKey.LeftControl).HasFlag(CoreVirtualKeyStates.Down)) TagAltClick(tagBtn, tagName);
                else if (p.Properties.PointerUpdateKind == Windows.UI.Input.PointerUpdateKind.LeftButtonReleased) PrimaryClick(tagName);
                else if (p.Properties.PointerUpdateKind == Windows.UI.Input.PointerUpdateKind.RightButtonReleased) SecondaryClick(tagName);
            }

        }

        private void SecondaryClick(string tagName)
        {
            if (!AnyContentDialogOpen()) DisplayRenameTagDialog(tagName);
        }

        private void PrimaryClick(string tagName)
        {
            if ((state == State.ADOPTION || (state == State.SEARCH && Window.Current.CoreWindow.GetKeyState(VirtualKey.LeftShift).HasFlag(CoreVirtualKeyStates.Down))) && !selectedTags.Contains(tagName))
            {
                selectedTags.Add(tagName);
                var btn = FindName(tagName) as Button;
                if (btn != null) btn.Background = _Selected;
            }
            else if ((state == State.ADOPTION || (state == State.SEARCH && Window.Current.CoreWindow.GetKeyState(VirtualKey.LeftShift).HasFlag(CoreVirtualKeyStates.Down))) && selectedTags.Contains(tagName))
            {
                selectedTags.Remove(tagName);
                var btn = FindName(tagName) as Button;
                if (btn != null) btn.Background = _Unselected;
            }
            else if (state == State.SEARCH && !selectedTags.Contains(tagName))
            {
                selectedTags.AddRange(videoGallery.TagManager.GetDescendants(tagName).Where(x => !selectedTags.Contains(x)));
                foreach (var tag in selectedTags)
                {
                    var btn = FindName(tag) as Button;
                    if (btn != null) btn.Background = _Selected;
                }
            }
            else if (state == State.SEARCH && selectedTags.Contains(tagName))
            {
                var tags = videoGallery.TagManager.GetDescendants(tagName);
                foreach (var tag in tags) selectedTags.Remove(tag);
                foreach (var tag in availableTags)
                    if (!selectedTags.Contains(tag))
                    {
                        var btn = FindName(tag) as Button;
                        if (btn != null) btn.Background = _Unselected;
                    }
            }
            else if (videoToSave.Tags != null && videoToSave.Tags.Contains(tagName))
            {
                videoGallery.TagManager.Remove(videoToSave, tagName);
                var tags = videoGallery.TagManager.GetDescendants(tagName);
                foreach (var tag in tags) selectedTags.Remove(tag);
                foreach (var tag in availableTags)
                    if (!selectedTags.Contains(tag))
                    {
                        var btn = FindName(tag) as Button;
                        if (btn != null) btn.Background = _Unselected;
                    }
            }
            else if (videoToSave.Tags != null && !videoToSave.Tags.Contains(tagName))
            {
                selectedTags.Add(tagName);
                selectedTags.AddRange(videoGallery.TagManager.GetAncestors(tagName).Where(x => !selectedTags.Contains(x)));
                videoGallery.TagManager.Add(videoToSave, tagCore, tagName);
                foreach (var tag in selectedTags)
                {
                    var btn = FindName(tag) as Button;
                    if (btn != null) btn.Background = _Selected;
                }
            }
        }

        private void VideoSaveToggleTagInclusive_Toggled(object sender, RoutedEventArgs e)
        {
            if (((ToggleSwitch)sender).IsOn) searchProperties = SearchProperties.Exclusive;
            else searchProperties = SearchProperties.Inclusive;
        }

        private void VideoSaveTagsSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            PopulateTagsGrid(VideoSaveTagsSearch.Text);
        }

        private void VideoSavePageBackBtn_Click(object sender, RoutedEventArgs e)
        {
            App.PageNavigation.Back(Frame);
        }

        private void VideoSaveNewTag_Click(object sender, RoutedEventArgs e)
        {
            string tagName = VideoSaveNewTagName.Text;
            if (!availableTags.Contains(tagName) && tagName != null && tagName != "" && videoToSave.Tags != null)
            {
                videoGallery.TagManager.Add(videoToSave, tagCore, tagName);
                selectedTags.Add(tagName);
                VideoSaveNewTagName.Text = "";
                App.MainDataManager.UpdateCoreData(videoGallery, DataManager.DatabaseFileName);
                PopulateTagsGrid(VideoSaveTagsSearch.Text);
            }
        }

        private void UpdateTagsList(bool isSearch)
        {
            if (isSearch) availableTags = videoGallery.TagManager.GetDescendants(tagCore.Name).OrderBy(x => x).ToList();
            else
            {
                if (videoToSave != null) availableTags = videoToSave.Tags.Intersect(videoGallery.TagManager.DirectTags(tagCore)).ToList();
                else availableTags = new List<string>();
                availableTags.AddRange(videoGallery.TagManager.DirectTags(tagCore).Except(availableTags));
            }
            if (adoptingParent != null) availableTags.RemoveAll(x => x.Equals(adoptingParent.Name));
            availableTags.RemoveAll(x => x.Equals(tagCore.Name));
        }

        private void VideoSaveSubmitBtn_Click(object sender, RoutedEventArgs e)
        {
            var props = videoToSave?.File.Properties.GetVideoPropertiesAsync().AsTask().GetAwaiter().GetResult();
            if (state == State.SEARCH)
            {
                var dict = new Dictionary<string, object>()
                {
                    { "videos", videoGallery.FindVideosByTags(selectedTags, searchProperties) },
                    { "tags", selectedTags }
                };
                App.PageNavigation.Navigate(Frame, typeof(GalleryViewPage), new WindowTransferPackage(State.SEARCH, dict));
                return;
            }
            else if (state == State.ADOPTION)
            {
                foreach (var tag in selectedTags) videoGallery.TagManager.Adopt(tag, adoptingParent);
                while ((App.PageNavigation.Stack.Peek()["args"] as WindowTransferPackage).State == State.ADOPTION) App.PageNavigation.Back(Frame);
                if (!AnyContentDialogOpen()) DisplayAdoptionWarningDialog();
                else App.PageNavigation.Back(Frame);
                return;
            }
            else if (state == State.UNKNOWN)
            {
                videoToSave.Id = VideoGallery.AvailableId(videoGallery);
                string videoId = VideoGallery.AvailableVideoId(videoGallery);
                videoToSave.VideoId = videoId;
                videoToSave.File.RenameAsync(videoId + videoToSave.File.FileType).AsTask().GetAwaiter().GetResult();
                if ((int)props.Width == 0) videoToSave.Width = 200;
                else videoToSave.Width = (int)props.Width;
                if ((int)props.Height == 0) videoToSave.Height = 150;
                else videoToSave.Height = (int)props.Height;
                VideoGallery.SetInitialThumbnail(videoToSave);
                videoGallery.ConvertUnknownToKnownVideo(videoToSave);
            }
            else if (state == State.TRIM)
            {
                if ((int)props.Width == 0) videoToSave.Width = 200;
                else videoToSave.Width = (int)props.Width;
                if ((int)props.Height == 0) videoToSave.Height = 150;
                else videoToSave.Height = (int)props.Height;
                VideoGallery.SetInitialThumbnail(videoToSave);

                if (videoGallery.GetAllKnownVideos().TrueForAll(x => x.VideoId != videoToSave.File.DisplayName))
                {

                    string videoId = VideoGallery.AvailableVideoId(videoGallery);
                    videoToSave.VideoId = videoId;
                    videoToSave.File.RenameAsync(videoId + videoToSave.File.FileType).AsTask().GetAwaiter().GetResult();
                }
                else videoToSave.VideoId = videoToSave.File.DisplayName;

                videoGallery.Videos.Add(videoToSave);
            }
            else if (state == State.KNOWN)
            {
                VideoGallery.SetInitialThumbnail(videoToSave);
            }

            App.MainDataManager.UpdateCoreData(videoGallery, DataManager.DatabaseFileName);

            if (state == State.UNKNOWN && videoGallery.UnknownVideos.Count <= 0) App.PageNavigation.Reset(Frame);
            else if (state == State.UNKNOWN)
            {
                var transferPackage = App.PageNavigation.MostRecentArgs(typeof(VideoViewPage)) as WindowTransferPackage;
                transferPackage.Parameters.Remove("position");

                App.PageNavigation.BackUntilPageChange(Frame);
            }
            else if (state == State.TRIM && videoToSave.EndTime != props.Duration)
            {
                var transferPackage = App.PageNavigation.MostRecentArgs(typeof(VideoViewPage)) as WindowTransferPackage;
                if (transferPackage.Parameters.ContainsKey("position")) transferPackage.Parameters["position"] = videoToSave.EndTime;
                else transferPackage.Parameters.Add("position", videoToSave.EndTime);


                App.PageNavigation.BackUntilPageChange(Frame);
            }
            else App.PageNavigation.BackUntilPageChange(Frame);
        }

        private async void DisplayAdoptionWarningDialog()
        {
            ContentDialog CharacterNameDialog = new ContentDialog
            {
                Title = "Warning",
                Content = "This action requires an application restart to take full affect, not all tags will be correctly assigned until then.",
                CloseButtonText = "Ok"
            };

            try { await CharacterNameDialog.ShowAsync(); }
            catch { }

            App.PageNavigation.Back(Frame);
        }

        private void VideoSavePagePeekBtn_Click(object sender, RoutedEventArgs e)
        {
            var dict = new Dictionary<string, object>() { { "video", videoToSave } };
            App.PageNavigation.Navigate(Frame, typeof(VideoViewPage), new WindowTransferPackage(State.PARTITION, dict));
        }

        private void VideoSaveAdoptBtn_Click(object sender, RoutedEventArgs e)
        {
            var dict = new Dictionary<string, object>()
                {
                    { "tagCore", App.MainVideoGallery.TagManager.TagCore },
                    { "adoptingParent", tagCore }
                };
            App.PageNavigation.Navigate(Frame, typeof(VideoSavePage), new WindowTransferPackage(State.ADOPTION, dict));
        }
    }
}