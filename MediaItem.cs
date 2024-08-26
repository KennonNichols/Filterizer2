using System.Windows.Shapes;
using Path = System.IO.Path;

namespace Filterizer2
{
    public class MediaItem
    {
        public int Id { get; set; }
        public string LocalFilename { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<TagItem> Tags { get; set; }

        public string ThumbnailPath => Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Thumbs"), LocalFilename);

        public override string ToString()
        {
            return Title; // Customize this to display what you want in the ListBox
        }
    }
}