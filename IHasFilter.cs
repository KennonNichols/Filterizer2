namespace Filterizer2
{
	public interface IHasFilter
	{
		public void ReloadAllMediaItems();
		
		public MediaSearchFilter Filter { get; }

		public void SetFilter(MediaSearchFilter filter);
	}
}