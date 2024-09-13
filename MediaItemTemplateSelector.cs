using Filterizer2.Repositories;

namespace Filterizer2
{
	using System.Windows;
	using System.Windows.Controls;

	public class MediaItemTemplateSelector : DataTemplateSelector
	{
		public DataTemplate? MediaItemTemplate { get; set; }
		public DataTemplate? AlbumItemTemplate { get; set; }

		public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
		{
			return item switch
			{
				MediaItem => MediaItemTemplate,
				AlbumItem => AlbumItemTemplate,
				_ => base.SelectTemplate(item, container)
			};
		}
	}

}