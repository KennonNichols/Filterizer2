namespace Filterizer2
{
	public interface IMediaDisplayItem
	{
		string DisplayTitle { get; }
		string? DisplayThumbnailPath { get; }
		IEnumerable<TagItem> TagsForFiltering { get; }
		
		string? GetMediaPath { get; }
	}
}