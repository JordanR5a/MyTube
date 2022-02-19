using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyTube.Model;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.System;

namespace MyTube.VideoLibrary
{
    public class DataManager
    {
        public static string DatabaseFileName = "Data.txt";
        public static string DatabaseBackupFileName = "Backup.txt";

        public static StorageFolder DatabaseFolder { get { return ApplicationData.Current.LocalCacheFolder; } }

        private DatabaseCore coreData;
        private List<StorageFile> storageFiles;
        private List<StorageFile> usedFiles;
        private TagManager tagManager;

        public DataManager(string database, bool forceReset)
        {
            coreData = new DatabaseCore();
            usedFiles = new List<StorageFile>();
            ParseData(database, forceReset);
        }

        public static void ViewDatabaseFolder()
        {
            Launcher.LaunchFolderAsync(DatabaseFolder).AsTask().GetAwaiter().GetResult();
        }

        public static void ViewDatabase()
        {

            var file = DatabaseFolder.TryGetItemAsync(DatabaseFileName).AsTask().GetAwaiter().GetResult();

            if (file.IsOfType(StorageItemTypes.File)) Launcher.LaunchFileAsync(file as IStorageFile).AsTask().GetAwaiter().GetResult();
        }

        public static void ResetBackup()
        {

            DatabaseFolder.CreateFileAsync(DatabaseBackupFileName, CreationCollisionOption.ReplaceExisting).AsTask().GetAwaiter().GetResult();
        }

        public static void ResetDatabase()
        {

            var files = DatabaseFolder.GetFilesAsync().AsTask().GetAwaiter().GetResult();

            for (int i = 0; i < files.Count; i++)
            {
                files.ElementAt(i).DeleteAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        private void ParseData(string databaseFileName, bool forceReset)
        {
            JsonSerializer serializer = new JsonSerializer();


            StorageFile database;
            if (DatabaseFolder.TryGetItemAsync(databaseFileName).AsTask().GetAwaiter().GetResult() == null || forceReset)
            {
                DatabaseFolder.CreateFileAsync(databaseFileName, CreationCollisionOption.ReplaceExisting).AsTask().GetAwaiter().GetResult();

                database = DatabaseFolder.GetFileAsync(databaseFileName).AsTask().GetAwaiter().GetResult();

                using (StreamWriter sw = new StreamWriter(database.OpenStreamForWriteAsync().GetAwaiter().GetResult()))
                {
                    sw.Write("{}");
                }


            }
            else database = DatabaseFolder.GetFileAsync(databaseFileName).AsTask().GetAwaiter().GetResult();


            var handle = database.CreateSafeFileHandle(options: FileOptions.RandomAccess);
            var stream = new FileStream(handle, FileAccess.ReadWrite);

            using (StreamReader sr = new StreamReader(stream))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                serializer.Populate(reader, coreData);
                if (coreData.Videos == null) coreData.Videos = new List<DetachedVideo>();
                if (coreData.Unknowns == null) coreData.Unknowns = new List<DetachedUnknown>();
            }
        }

        private void SaveData(string databaseFileName)
        {
            JsonSerializer serializer = new JsonSerializer();

            StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", DatabaseFolder);

            StorageFolder folder = StorageApplicationPermissions.FutureAccessList.GetFolderAsync("PickedFolderToken").AsTask().GetAwaiter().GetResult();
            StorageFile database = folder.CreateFileAsync(databaseFileName, CreationCollisionOption.ReplaceExisting).AsTask().GetAwaiter().GetResult();

            var handle = database.CreateSafeFileHandle(options: FileOptions.RandomAccess);
            var stream = new FileStream(handle, FileAccess.ReadWrite);

            using (StreamWriter sw = new StreamWriter(stream))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, coreData);
            }
        }

        private StorageFile FindMatchingId(string videoId)
        {
            StorageFile match = null;
            foreach (StorageFile file in this.storageFiles.ToArray())
            {
                if (file.DisplayName.Equals(videoId))
                {
                    match = file;
                    usedFiles.Add(file);
                }
            }
            if (match == null) throw new FileNotFoundException("No matching file can be found for video creation");
            return match;
        }

        private TimeSpan[] ConvertStringToTimespan(string[] sMarks)
        {
            List<TimeSpan> marks = new List<TimeSpan>();
            foreach (string timespan in sMarks) marks.Add(TimeSpan.Parse(timespan));
            marks.Sort();
            return marks.ToArray();
        }

        private AttachedVideo CreateVideo(DetachedVideo dVideo)
        {
            AttachedVideo aVideo;
            if (dVideo.DocumentedDate != 0) aVideo = new AttachedVideo(new DateTime(dVideo.DocumentedDate));
            else aVideo = new AttachedVideo();
            aVideo.Id = dVideo.Id;
            aVideo.IsUnique = true;
            aVideo.RawTags = new List<AttachedTag>();
            foreach (string tagName in dVideo.Tags) tagManager.Add(aVideo, tagName);
            aVideo.Parts = dVideo.Parts.Select(x => ConvertStringToTimespan(x)).ToList();
            aVideo.VideoId = dVideo.videoId;
            aVideo.File = FindMatchingId(aVideo.VideoId);
            if (dVideo.Height <= 0 || dVideo.Width <= 0)
            {
                try
                {
                    var props = aVideo.File.Properties.GetVideoPropertiesAsync().AsTask().GetAwaiter().GetResult();
                    if ((int)props.Width == 0) aVideo.Width = 200;
                    else aVideo.Width = (int)props.Width;
                    if ((int)props.Height == 0) aVideo.Height = 150;
                    else aVideo.Height = (int)props.Height;
                }
                catch (InvalidOperationException)
                {
                    aVideo.Width = 200;
                    aVideo.Height = 150;
                    Debug.WriteLine("Failed to get video properties failed, reverting to defaults");
                }

            }
            else
            {
                aVideo.Width = dVideo.Width;
                aVideo.Height = dVideo.Height;
            }
            return aVideo;
        }
        private AttachedTag CreateTag(DetachedTag dTag) { return CreateTag(dTag, null); }

        private AttachedTag CreateTag(DetachedTag dTag, AttachedTag parent)
        {
            AttachedTag aTag = new AttachedTag();
            aTag.Parent = parent;
            aTag.Name = dTag.Name;
            aTag.Videos = new List<AttachedVideo>();
            foreach (DetachedTag child in dTag.Children) aTag.Children.Add(CreateTag(child, aTag));
            return aTag;
        }

        public static AttachedVideo CreateNullVideo(StorageFile file)
        {
            AttachedVideo aVideo = new AttachedVideo();
            aVideo.File = file;
            aVideo.RawTags = new List<AttachedTag>();
            aVideo.Parts = new List<TimeSpan[]> { new TimeSpan[2] { TimeSpan.Zero, aVideo.File.Properties.GetVideoPropertiesAsync().AsTask().GetAwaiter().GetResult().Duration } };
            if (aVideo.EndTime <= TimeSpan.Zero) throw new NullReferenceException("Currupted video found");
            return aVideo;
        }

        private AttachedVideo CreatePsuedoNullVideo(DetachedUnknown unknown, StorageFile file)
        {
            AttachedVideo aVideo = new AttachedVideo();
            aVideo.File = file;
            aVideo.RawTags = new List<AttachedTag>();
            aVideo.Parts = new List<TimeSpan[]> { ConvertStringToTimespan(unknown.StartStopPoints) };
            return aVideo;
        }

        private AttachedVideo CreateUnknownVideo(StorageFile file)
        {
            DetachedUnknown unknown = coreData.Unknowns.Find(x => x.Name.Equals(file.DisplayName));

            if (unknown != null) return CreatePsuedoNullVideo(unknown, file);
            else return CreateNullVideo(file);
        }

        public VideoGallery CreateVideoGallery(List<StorageFile> storageFiles)
        {
            this.storageFiles = storageFiles;
            if (coreData.TagOrigin.Name != null) tagManager = new TagManager(CreateTag(coreData.TagOrigin));
            else tagManager = new TagManager();

            ConcurrentBag<AttachedVideo> videos = new ConcurrentBag<AttachedVideo>();
            List<Task> videoTasks = new List<Task>();
            foreach (DetachedVideo dVideo in coreData.Videos)
            {
                videoTasks.Add(Task.Run(() =>
                {
                    try { videos.Add(CreateVideo(dVideo)); }
                    catch (FileNotFoundException e) { Debug.WriteLine(e.Message); }
                }));
            }

            ConcurrentBag<AttachedVideo> unknownVideos = new ConcurrentBag<AttachedVideo>();
            List<Task> unknownTasks = new List<Task>();
            foreach (StorageFile file in this.storageFiles)
            {
                unknownTasks.Add(Task.Run(async () =>
                {
                    if (!usedFiles.Contains(file))
                    {
                        try { unknownVideos.Add(CreateUnknownVideo(file)); }
                        catch (NullReferenceException)
                        {
                            await file.DeleteAsync();
                            Debug.WriteLine("Currupted video found and deleted");
                        }
                    }
                }));
            }

            Task.WaitAll(videoTasks.Union(unknownTasks).ToArray());

            coreData = null;
            return new VideoGallery(videos.ToList(),
                                    unknownVideos.ToList(),
                                    tagManager);
        }

        private string[] StripTimespan(TimeSpan[] marks)
        {
            List<string> sMarks = new List<string>();
            foreach (TimeSpan mark in marks) sMarks.Add(mark.ToString());
            sMarks.Sort();
            return sMarks.ToArray();
        }

        private DetachedVideo StripVideo(AttachedVideo aVideo)
        {
            if (aVideo == null) return null;
            DetachedVideo dVideo = new DetachedVideo();
            dVideo.Id = aVideo.Id;
            dVideo.DocumentedDate = aVideo.DocumentedDate.Ticks;
            dVideo.Tags = aVideo.Tags.OrderBy(x => x).ToArray();
            dVideo.Parts = aVideo.Parts.Select(x => StripTimespan(x)).ToList();
            dVideo.videoId = aVideo.VideoId;
            dVideo.Width = aVideo.Width;
            dVideo.Height = aVideo.Height;
            return dVideo;
        }

        private DetachedUnknown StripUnknown(AttachedVideo aVideo)
        {
            if (aVideo == null) return null;
            DetachedUnknown dVideo = new DetachedUnknown();
            dVideo.Name = aVideo.File.DisplayName;
            dVideo.StartStopPoints = StripTimespan(new TimeSpan[2] { aVideo.StartTime, aVideo.EndTime });
            return dVideo;
        }

        private DetachedTag StripTag(AttachedTag aTag)
        {
            DetachedTag dTag = new DetachedTag();
            dTag.Name = aTag.Name;
            dTag.Parent = aTag.Parent?.Name;
            dTag.Children = new List<DetachedTag>();
            foreach (AttachedTag tag in aTag.Children) dTag.Children.Add(StripTag(tag));
            dTag.Children = dTag.Children.OrderBy(x => x.Name).ToList();
            return dTag;
        }

        public void UpdateCoreData(VideoGallery videoGallery, string databaseName)
        {
            DatabaseCore database = new DatabaseCore();
            foreach (AttachedVideo aVideo in videoGallery.Videos) database.Videos.Add(StripVideo(aVideo));
            database.Videos = database.Videos.OrderBy(x => x.DocumentedDate).ToList();
            foreach (AttachedVideo aVideo in videoGallery.UnknownVideos) database.Unknowns.Add(StripUnknown(aVideo));
            database.Unknowns = database.Unknowns.OrderByDescending(x => x.Name).ToList();
            database.TagOrigin = StripTag(videoGallery.TagManager.TagCore);
            coreData = database;

            SaveData(databaseName);
            coreData = null;
        }

    }
}