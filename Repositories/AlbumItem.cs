using System.Windows.Media.Converters;

namespace Filterizer2.Repositories
{
	public class AlbumItem: IMediaDisplayItem
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public string Description { get; set; }
		public List<MediaItem> MediaItems { get; } = new List<MediaItem>();
		private int _selectedIndex;

		public static HashSet<MediaItem> RecentlyInvalidatedMedia = new HashSet<MediaItem>();
		
		// Method to get all unique tags associated with the album's media items
		public HashSet<TagItem> GetAllTags()
		{
			HashSet<TagItem> allTags = new HashSet<TagItem>();

			foreach (var mediaItem in MediaItems.OfType<MediaItem>())
			{
				foreach (TagItem tag in mediaItem.GetTags())
				{
					allTags.Add(tag);
				}
			}

			return allTags;
		}
		
		public string DisplayTitle => Name;

		public string? DisplayThumbnailPath => MediaItems.Count > 0 ? MediaItems[0].ThumbnailPath : null;
		public string? GetMediaPath => MediaItems.Count > 0 ? MediaItems[0].MediaFilePath : null;
		public IEnumerable<TagItem> TagsForFiltering => GetCachedTagsForFiltering();
		private IEnumerable<TagItem>? _cachedTagsForFiltering;

		private IEnumerable<TagItem> GetCachedTagsForFiltering()
		{
			if (RecentlyInvalidatedMedia.Any(item => MediaItems.Contains(item)))
			{
				_cachedTagsForFiltering = null;
			}
			return _cachedTagsForFiltering ??= CalculateTagsForFiltering();
		}
		
		private IEnumerable<TagItem> CalculateTagsForFiltering()
		{
			HashSet<TagItem> tags = new HashSet<TagItem>();
			foreach (TagItem tagItem in GetAllTags())
			{
				tagItem.GetTagHierarchyTags(ref tags);
			}
	        
			foreach (TagItem tagItem in tags)
			{
				yield return tagItem;
			}
		}
		
		public MediaItem? GetCurrentMediaItem()
		{
			if (_selectedIndex >= MediaItems.Count || _selectedIndex < 0) return null;
			return MediaItems[_selectedIndex];
		}

		public bool CanNavigateLeft => _selectedIndex > 0;
		public bool CanNavigateRight => _selectedIndex < MediaItems.Count - 1;

		public void NavigateRight()
		{
			if (CanNavigateRight) _selectedIndex += 1;
		}
		public void NavigateLeft()
		{
			if (CanNavigateLeft) _selectedIndex -= 1;
		}
	}
}