using Filterizer2.Repositories;

namespace Filterizer2
{
	public abstract class MediaSorter : IComparer<IMediaDisplayItem>
	{
		public static IEnumerable<MediaSorter> Sorters
		{
			get
			{
				yield return new UnsortedSorter();
				yield return new PostTimeAscendingSorter();
				yield return new PostTimeDescendingSorter();
				yield return new NumberOfTagsAscendingSorter();
				yield return new NumberOfTagsDescendingSorter();
			}
		}
		
		public abstract int Compare(IMediaDisplayItem? item1, IMediaDisplayItem? item2);

		protected static bool TryDoBaseComparison(IMediaDisplayItem? item1, IMediaDisplayItem? item2, out int result)
		{
			//Nulls go later
			switch (item1)
			{
				case null when item2 == null:
					result = 0;
					return true;
				case null:
					result = 1;
					return true;
			}
			if (item2 == null)
			{
				result = -1;
				return true;
			}

			switch (item1)
			{
				//Albums ALWAYS precede media
				case MediaItem when item2 is AlbumItem:
					result = 1;
					return true;
				case AlbumItem when item2 is MediaItem:
					result = -1;
					return true;
			}

			result = 0;
			return false;
		}

		public abstract string Label { get; }
	}
	
	
	public class UnsortedSorter : MediaSorter
	{
		public override int Compare(IMediaDisplayItem? item1, IMediaDisplayItem? item2)
		{
			return 0;
		}

		public override string Label => "Unsorted";
	}

	public class NumberOfTagsDescendingSorter : MediaSorter
	{
		public override int Compare(IMediaDisplayItem? item1, IMediaDisplayItem? item2)
		{
			if (TryDoBaseComparison(item1, item2, out int result))
			{
				return result;
			}
			
			return item1.TagsForFiltering.Count().CompareTo(item2.TagsForFiltering.Count());
		}

		public override string Label => "Tag count (desc)";
	}

	public class NumberOfTagsAscendingSorter : MediaSorter
	{
		public override int Compare(IMediaDisplayItem? item1, IMediaDisplayItem? item2)
		{
			if (TryDoBaseComparison(item1, item2, out int result))
			{
				return result;
			}
			
			return item2.TagsForFiltering.Count().CompareTo(item1.TagsForFiltering.Count());
		}

		public override string Label => "Tag count (asc)";
	}

	public class PostTimeDescendingSorter : MediaSorter
	{
		public override int Compare(IMediaDisplayItem? item1, IMediaDisplayItem? item2)
		{
			if (TryDoBaseComparison(item1, item2, out int result))
			{
				return result;
			}
			
			return item1.Id.CompareTo(item2.Id);
		}

		public override string Label => "Time added (desc)";
	}

	public class PostTimeAscendingSorter : MediaSorter
	{
		public override int Compare(IMediaDisplayItem? item1, IMediaDisplayItem? item2)
		{
			if (TryDoBaseComparison(item1, item2, out int result))
			{
				return result;
			}
			
			return item2.Id.CompareTo(item1.Id);
		}

		public override string Label => "Time added (asc)";
	}
}