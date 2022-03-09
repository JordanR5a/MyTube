using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;
using MyTube.VideoLibrary;
using MyTube.Model;
using System.Collections.Generic;
using Windows.ApplicationModel.Core;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace MyTube
{
    public sealed partial class MainPage : Page
    {

        private DataManager dataManager;
        private VideoGallery videoGallery;

        private bool initialStartup;

        public MainPage()
        {
            this.InitializeComponent();
            App.PageNavigation = new PageNavigation(typeof(MainPage));
            ApplicationView.GetForCurrentView().FullScreenSystemOverlayMode = FullScreenSystemOverlayMode.Minimal;
            ApplicationView.GetForCurrentView().TryEnterFullScreenMode();

            if (App.MainDataManager == null && App.MainVideoGallery == null)
            {
                initialStartup = true;
                LoadingMode();
                var task = FileStorage.SetVideoFolder();
                task.ContinueWith((folder) =>
                {
                    if (task.IsCompletedSuccessfully && FileStorage.VideosFolder != null) PopulateVideoGallery();
                    else CoreApplication.Exit();
                });
            }
            else
            {
                MainMenuMode();
                dataManager = App.MainDataManager;
                videoGallery = App.MainVideoGallery;
            }
        }

        private async void LoadingMode()
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                Window.Current.CoreWindow.PointerCursor = null;
                RootGrid.Visibility = Visibility.Collapsed;
                MainPageLoading.Visibility = Visibility.Visible;
            });
        }

        private async void MainMenuMode()
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
                MainPageLoading.Visibility = Visibility.Collapsed;
                RootGrid.Visibility = Visibility.Visible;
            });
        }

        private async void PopulateVideoGallery(string instanceCode, string password)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    var newData = new DataManager(instanceCode, password);
                    if (newData.IsEmpty()) return;
                    dataManager = new DataManager(instanceCode, password);
                    videoGallery = dataManager.CreateVideoGallery(FileStorage.GetAllVideos());
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                    DisplayDatabaseCurruptionDialog();
                    return;
                }


                App.MainDataManager = dataManager;
                App.MainVideoGallery = videoGallery;
                MainMenuMode();
            });
        }

        private async void PopulateVideoGallery()
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    dataManager = new DataManager(DataManager.DatabaseFileName, false);
                    videoGallery = dataManager.CreateVideoGallery(FileStorage.GetAllVideos());
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                    DisplayDatabaseCurruptionDialog();
                    return;
                }


                App.MainDataManager = dataManager;
                App.MainVideoGallery = videoGallery;
                MainMenuMode();
            });
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (initialStartup && dataManager != null && videoGallery != null) dataManager.UpdateCoreData(videoGallery, DataManager.DatabaseBackupFileName);
        }

        private void MainPageKnownVideos_Click(object sender, RoutedEventArgs e)
        {
            if (videoGallery.Videos.Count > 0)
            {
                var dict = new Dictionary<string, object>() { { "videoGallery", videoGallery } };
                App.PageNavigation.Navigate(Frame, typeof(GalleryViewPage), new WindowTransferPackage(State.DEFAULT, dict));
            }
        }

        private void MainPageUnknownVideos_Click(object sender, RoutedEventArgs e)
        {
            if (videoGallery.UnknownVideos.Count > 0)
            {
                var dict = new Dictionary<string, object>() { { "videoGallery", videoGallery } };
                App.PageNavigation.Navigate(Frame, typeof(VideoViewPage), new WindowTransferPackage(State.UNKNOWN, dict));
            }
        }

        private void MainPageImport_Click(object sender, RoutedEventArgs e)
        {
            FileStorage.SaveAllVideosWithinFolder();
            DisplayImportUpdateDialog();
        }

        private async void DisplayImportUpdateDialog()
        {
            ContentDialog ImportDialog = new ContentDialog
            {
                Title = "Importing Data",
                Content = "Close this message when your videos have been imported.",
                CloseButtonText = "Finished"
            };

            await ImportDialog.ShowAsync();

            await Task.Run(() => { LoadingMode(); }).ContinueWith(t =>
            {
                PopulateVideoGallery();
            });
            App.PageNavigation.Reset(Frame);
        }

        private void MainPageLoadBackup_Click(object sender, RoutedEventArgs e)
        {
            DisplayLoadBackupDialog();
        }

        private void LoadBackup()
        {
            DataManager backup;
            VideoGallery backupGallery;
            try
            {
                backup = new DataManager(DataManager.DatabaseBackupFileName, false);
                backupGallery = backup.CreateVideoGallery(FileStorage.GetAllVideos());
            }
            catch (Exception)
            {
                DisplayCriticalFailureDialog();
                return;
            }

            new DataManager(DataManager.DatabaseFileName, true).UpdateCoreData(backupGallery, DataManager.DatabaseFileName);
            App.PageNavigation.Reset(Frame);
        }

        private async void DisplayCriticalFailureDialog()
        {
            ContentDialog criticalFailureDialog = new ContentDialog
            {
                Title = "Critical Failure",
                Content = "A fatal error has occurred while attempting to load a backup, " +
                            "all data will be reset upon closing this message. " +
                            "Do you wish to view the available databases before all data is permanently erased?",
                PrimaryButtonText = "View Data",
                CloseButtonText = "Reset Data"
            };

            ContentDialogResult result = await criticalFailureDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                DataManager.ViewDatabaseFolder();
            }

            DisplayFinalWarningDialog();
        }

        private async void DisplayFinalWarningDialog()
        {
            ContentDialog finalWarningDialog = new ContentDialog
            {
                Title = "Final Warning",
                Content = "All current data will now be erased and systems reset.",
                CloseButtonText = "Ok"
            };

            await finalWarningDialog.ShowAsync();

            DataManager.ResetDatabase();
            App.PageNavigation.Reset(Frame);
        }

        private async void DisplayLoadBackupDialog()
        {
            ContentDialog loadBackupDialog = new ContentDialog
            {
                Title = "Load Backup Database?",
                Content = "If you choose to load from a backup, all changes you have made since initial startup will be lost. Do you still wish to continue?",
                PrimaryButtonText = "Load Backup",
                CloseButtonText = "Cancel"
            };

            ContentDialogResult result = await loadBackupDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                LoadBackup();
            }
        }

        private async void DisplayDatabaseCurruptionDialog()
        {
            ContentDialog databaseCurruptionDialog = new ContentDialog
            {
                Title = "Database Currupted",
                Content = "An error has occured within your database which has forced the application to load from a previous backup.",
                CloseButtonText = "Ok"
            };

            await databaseCurruptionDialog.ShowAsync();

            LoadBackup();
        }

        private void MainPageDatabase_Click(object sender, RoutedEventArgs e)
        {
            DataManager.ViewDatabase();
        }

        private async void DisplayLoginDialog()
        {
            TextBox inputTextBox = new TextBox();
            inputTextBox.AcceptsReturn = false;
            inputTextBox.Height = 32;
            inputTextBox.Focus(FocusState.Programmatic);

            ContentDialog loginDialog = new ContentDialog
            {
                Title = "Login",
                Content = inputTextBox,
                PrimaryButtonText = "Submit",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Primary
            };

            ContentDialogResult result = await loginDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                App.password = inputTextBox.Text.Trim();
                if (App.instanceCode == null) DisplayinstanceCodeDialog();
                else PopulateVideoGallery(App.instanceCode, App.password);
            }
        }

        private async void DisplayinstanceCodeDialog()
        {
            TextBox inputTextBox = new TextBox();
            inputTextBox.AcceptsReturn = false;
            inputTextBox.Height = 32;
            inputTextBox.Focus(FocusState.Programmatic);

            ContentDialog instanceCodeDialog = new ContentDialog
            {
                Title = "Instance Code",
                Content = inputTextBox,
                PrimaryButtonText = "Submit",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Primary
            };

            ContentDialogResult result = await instanceCodeDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                App.instanceCode = inputTextBox.Text.Trim();
                PopulateVideoGallery(App.instanceCode, App.password);
            }
        }

        private void MainPageLoadGlobalDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (App.password == null) DisplayLoginDialog();
            else DisplayinstanceCodeDialog();
        }
    }
}