using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;

namespace MyTube.Model
{
    public class Thumbnail
    {
        public string Id { get; }
        public Task<StorageItemThumbnail> ItemThumbnail { get; }
        public Task<ImageStream> Stream { get; }

        public Thumbnail(string id, Task<StorageItemThumbnail> itemThumbnail)
        {
            Id = id;
            ItemThumbnail = itemThumbnail;
        }
        public Thumbnail(string id, Task<ImageStream> stream)
        {
            Id = id;
            Stream = stream;
        }

        public Thumbnail(Task<StorageItemThumbnail> itemThumbnail)
        {
            ItemThumbnail = itemThumbnail;
        }

        public Thumbnail(Task<ImageStream> stream)
        {
            Stream = stream;
        }
    }
}