using System.IO;
using System.Net;
using System.Windows.Shapes;
using Filterizer2.Repositories;
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
        private List<TagItem> Tags = new List<TagItem>();

        public string? ThumbnailPath => ThumbnailGenerator.GenerateOrGetThumbnail(MediaFilePath);

        public string? MediaFilePath => Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Media"), LocalFilename);


        public string DisplayTitle => Title;
        public string? DisplayThumbnailPath => ThumbnailPath;

        public List<TagItem> GetTags()
        {
	        return Tags;
        }

        public bool AddTag(TagItem tagItem)
        {
	        if (Tags.Contains(tagItem)) return false;
	        Tags.Add(tagItem);
	        ClearTagCache();
	        return true;
        }
        
        public void SetTags(List<TagItem> tags)
        {
	        Tags = tags;
	        ClearTagCache();
        }
        
        public IEnumerable<TagItem> TagsForFiltering => _cachedTagsForFiltering ??= CalculateTagsForFiltering();
        private IEnumerable<TagItem>? _cachedTagsForFiltering;
        public void ClearTagCache()
        {
	        _cachedTagsForFiltering = null;
	        AlbumItem.RecentlyInvalidatedMedia.Add(this);
        }
        public string? GetMediaPath => MediaFilePath;

        private IEnumerable<TagItem> CalculateTagsForFiltering()
        {
	        HashSet<TagItem> tags = new HashSet<TagItem>();
	        foreach (TagItem tagItem in Tags)
	        {
		        tagItem.GetTagHierarchyTags(ref tags);
	        }
	        
	        foreach (TagItem tagItem in tags)
	        {
		        yield return tagItem;
	        }
        }
    }
}