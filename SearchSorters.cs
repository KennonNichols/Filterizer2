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
				yield return new RandomSorter();
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
		public abstract string SQLClause { get; }
	}
	
	
	public class UnsortedSorter : MediaSorter
	{
		public override int Compare(IMediaDisplayItem? item1, IMediaDisplayItem? item2)
		{
			return 0;
		}

		public override string Label => "Unsorted";
		public override string SQLClause => "";
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
		public override string SQLClause => "TagCount DESC";
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
		public override string SQLClause => "TagCount ASC";
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
		public override string SQLClause => "m.Id ASC";
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
		public override string SQLClause => "m.Id DESC";
	}

	public class RandomSorter : MediaSorter
	{
		private Random _random = new Random();
		private Dictionary<int, float> _values = [];
		
		public override int Compare(IMediaDisplayItem? item1, IMediaDisplayItem? item2)
		{
			if (TryDoBaseComparison(item1, item2, out int result))
			{
				return result;
			}

			return GetSeededValue(item2.Id).CompareTo(GetSeededValue(item1.Id));
		}

		private float GetSeededValue(int id)
		{
			if (_values.TryGetValue(id, out float value)) return value;
			value = _random.NextSingle();
			_values.Add(id, value);
			return value;
		}

		public void Randomize()
		{
			_values.Clear();
		}
		
		public override string Label => "Random";
		public override string SQLClause => "RANDOM()";
	}
}