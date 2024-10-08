using System.IO;
using System.Net;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace Filterizer2
{
    public class MediaItem: IMediaDisplayItem
    {
        private bool triedToGenerateThumbThisInstance = false;
        
        public int Id { get; set; }
        public string LocalFilename { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<TagItem> Tags { get; set; }

        public string? ThumbnailPath => ThumbnailGenerator.GenerateOrGetThumbnail(MediaFilePath);

        public string? MediaFilePath => Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Media"), LocalFilename);


        public string DisplayTitle => Title;
        public string? DisplayThumbnailPath => ThumbnailPath;
        public IEnumerable<TagItem> TagsForFiltering => Tags;
        public string? GetMediaPath => MediaFilePath;
    }
}