using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace MyTube.VideoLibrary
{
    public class FileStorage
    {
        public static StorageFolder VideosFolder;

        private static bool IsVideo(string fileType)
        {
            if (fileType == ".mp4" || fileType == ".avi" || fileType == ".mov" || fileType == ".avi" ||
                 fileType == ".wmv" || fileType == ".flv" || fileType == ".avchd" || fileType == ".webm" ||
                  fileType == ".mkv" || fileType == ".vob" || fileType == ".ogv" || fileType == ".mng" ||
                   fileType == ".yuv" || fileType == ".rm" || fileType == ".rmvb" || fileType == ".nsv") return true;

            return false;
        }

        public static List<StorageFile> GetAllVideos()
        {
            IReadOnlyList<StorageFile> fileList = VideosFolder.GetFilesAsync().AsTask().GetAwaiter().GetResult();

            ConcurrentBag<StorageFile> videos = new ConcurrentBag<StorageFile>();
            List<Task> tasks = new List<Task>();

            foreach (StorageFile file in fileList)
            {
                tasks.Add(Task.Run(async () =>
                {
                    if (IsVideo(file.FileType)) videos.Add(file);
                    else await file.DeleteAsync();
                }));
            }
            Task.WaitAll(tasks.ToArray());

            return videos.ToList();
        }

        public async static Task<StorageFolder> SetVideoFolder()
        {
            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");

            StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);
                VideosFolder = folder;
                return folder;
            }
            return null;
        }

        public async static void SaveAllVideosWithinFolder()
        {
            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");

            StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);
                foreach (StorageFile file in GetFolderVideos(folder))
                {
                    try
                    {
                        file.CopyAsync(VideosFolder, file.DisplayName + new Random().Next(int.MaxValue) + file.FileType).AsTask().GetAwaiter().GetResult();
                    }
                    catch (Exception) { }
                }
            }
        }

        private static List<StorageFile> GetFolderVideos(StorageFolder folder)
        {
            var files = folder.GetFilesAsync().AsTask().GetAwaiter().GetResult();
            List<StorageFile> videos = new List<StorageFile>();

            foreach (StorageFile file in files)
            {
                if (IsVideo(file.FileType)) videos.Add(file);
            }

            foreach (StorageFolder subFolder in folder.GetFoldersAsync().AsTask().GetAwaiter().GetResult())
            {
                videos.AddRange(GetFolderVideos(subFolder));
            }
            return videos;
        }

        public async static void SaveAllVideos()
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".mp4");

            var files = await picker.PickMultipleFilesAsync();
            if (files == null) return;

            foreach (StorageFile file in files)
            {
                try
                {
                    file.CopyAsync(VideosFolder, file.DisplayName + new Random().Next(int.MaxValue) + file.FileType).AsTask().GetAwaiter().GetResult();
                }
                catch (Exception) { }
            }
        }

        /*public static async Task SaveThumbnail(AttachedVideo video)
        {
            try
            {
                var image = video.Thumbnails[0];
                StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", ApplicationData.Current.TemporaryFolder);
                StorageFolder folder = StorageApplicationPermissions.FutureAccessList.GetFolderAsync("PickedFolderToken").AsTask().GetAwaiter().GetResult();
                var file = folder.CreateFileAsync(video.Id + ".jpg", CreationCollisionOption.ReplaceExisting).AsTask().GetAwaiter().GetResult();
            }
            catch (Exception e) { }
        }*/
    }
}