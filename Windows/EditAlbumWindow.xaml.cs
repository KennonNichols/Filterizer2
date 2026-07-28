using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Filterizer2.Repositories;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace Filterizer2.Windows
{
	public partial class EditAlbumWindow : IHasFilter, ISelectsTags
	{
        private AlbumItem _albumItem;
        private bool _isEditMode;
        private MediaSorter Sorter = new UnsortedSorter();
        public MediaSearchFilter Filter { get; private set; } = new SearchFilterOpen(new List<TagFilter>());
        public void SetFilter(MediaSearchFilter filter)
        {
            Filter = filter;
        }


        public EditAlbumWindow(AlbumItem? albumItem = null, List<MediaItem>? startingItems = null)
        {
            InitializeComponent();

            SorterSelectorBox.ItemsSource = MediaSorter.Sorters;
            SorterSelectorBox.SelectedIndex = 0;

            if (albumItem != null)
            {
                _albumItem = albumItem;
                _isEditMode = true;
                // Populate fields with existing album data
                TitleTextBox.Text = _albumItem.Name;
                DescriptionTextBox.Text = _albumItem.Description;

                // Populate AlbumContentsListBox with current media items in the album
                foreach (var mediaItem in _albumItem.MediaItems)
                {
                    AlbumContentsListBox.Items.Add(mediaItem);
                }
            }
            else
            {
	            _isEditMode = false;
	            _albumItem = new AlbumItem();
	            if (startingItems != null)
	            {
		            foreach (MediaItem startingItem in startingItems)
		            {
			            AddMediaItem(startingItem);
		            }
	            }
            }
            
            
            
            ReloadAllMediaItems();
        }
        
        // List<MediaItem> displayItems = MediaRepository.GetAllMediaItems().Where(mediaItem => Filter.TestMedia(mediaItem)).ToList();
        //
        // displayItems.Sort(Sorter);
        //
        // MediaListBox.Items.Clear();
        //
        // foreach (MediaItem mediaItem in displayItems)
        // {
        //     MediaListBox.Items.Add(mediaItem);
        // }
        public void ReloadAllMediaItems()
        {
	        // All results that fit the filter
            MediaListBox.ItemsSource =
	            MediaRepository.GetAllMediaItems(Sorter).Where(mediaItem => Filter.TestMedia(mediaItem));
        }

        private void MediaListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (MediaListBox.SelectedItem is not MediaItem selectedMediaItem) return;
            if (_albumItem.MediaItems.Contains(selectedMediaItem)) return;
            AddMediaItem(selectedMediaItem);
        }

        private void AddMediaItem(MediaItem item)
        {
	        _albumItem.MediaItems.Add(item);
	        AlbumContentsListBox.Items.Add(item);
        }

        private void RemoveMediaItem_Click(object sender, RoutedEventArgs e)
        {
            MediaItem mediaItem = (MediaItem)((Button)sender).Tag;
            _albumItem.MediaItems.Remove(mediaItem);
            AlbumContentsListBox.Items.Remove(mediaItem);
        }
        
        private void EditMediaItem_Click(object sender, RoutedEventArgs e)
        {
	        MediaItem mediaItem = (MediaItem)((Button)sender).Tag;
	        EditMediaEntryWindow entryWindow = new EditMediaEntryWindow(mediaItem);
	        if (entryWindow.ShowDialog() ?? false)
	        {
		        ReloadAllMediaItems();
	        }
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            MediaItem mediaItem = (MediaItem)((Button)sender).Tag;
            int index = AlbumContentsListBox.Items.IndexOf(mediaItem);
            if (index <= 0) return;
            _albumItem.MediaItems.RemoveAt(index);
            _albumItem.MediaItems.Insert(index - 1, mediaItem);
            AlbumContentsListBox.Items.RemoveAt(index);
            AlbumContentsListBox.Items.Insert(index - 1, mediaItem);
            AlbumContentsListBox.SelectedItem = mediaItem;
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            MediaItem mediaItem = (MediaItem)((Button)sender).Tag;
            int index = AlbumContentsListBox.Items.IndexOf(mediaItem);
            if (index >= AlbumContentsListBox.Items.Count - 1) return;
            _albumItem.MediaItems.RemoveAt(index);
            _albumItem.MediaItems.Insert(index + 1, mediaItem);
            AlbumContentsListBox.Items.RemoveAt(index);
            AlbumContentsListBox.Items.Insert(index + 1, mediaItem);
            AlbumContentsListBox.SelectedItem = mediaItem;
        }
        
        
        private void InvertItems_Click(object sender, RoutedEventArgs e)
        {
	        _albumItem.MediaItems.Reverse();
	        AlbumContentsListBox.Items.Clear();
	        
	        foreach (MediaItem albumItemMediaItem in _albumItem.MediaItems)
	        {
		        AlbumContentsListBox.Items.Add(albumItemMediaItem);
	        }
        }

        private void FilterMediaByTags_Click(object sender, RoutedEventArgs e)
        {
            FilterWindow filterWindow = new FilterWindow(this, Filter);
            filterWindow.ShowDialog();
        }

        private void AddAllTags_Click(object sender, RoutedEventArgs e)
        {
	        SelectTagsWindow tagSelectWindow = new SelectTagsWindow(this, null);
	        tagSelectWindow.ShowDialog();
        }

        private void AddAllTagsAdvanced_Click(object sender, RoutedEventArgs e)
        {
	        string? filePath = null;
	        if (_albumItem.MediaItems.Count > 0)
	        {
		        filePath = _albumItem?.MediaItems[0]?.MediaFilePath;
	        }
	        MediaTaggingHelperWindow tagSelectWindow = new MediaTaggingHelperWindow(filePath, OnTagSelectComplete);
	        tagSelectWindow.ShowDialog();
        }
        
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            _albumItem.Name = TitleTextBox.Text;
            _albumItem.Description = DescriptionTextBox.Text;

            if (_isEditMode)
            {
	            if (_albumItem.MediaItems.Count == 0)
	            {
		            AlbumRepository.DeleteAlbum(_albumItem);
		            return;
	            }
	            
		        AlbumRepository.UpdateAlbum(_albumItem);
	            
            }
            else
            {
                if (_albumItem.MediaItems.Count != 0)
                {                
                    AlbumRepository.AddAlbum(_albumItem);
                }
            }
        }

        private void SorterSelectorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SorterSelectorBox.SelectedItem is not MediaSorter mediaSorter) return;
            Sorter = mediaSorter;
            ReloadAllMediaItems();
        }

        public void OnTagSelectComplete(List<TagItem> selectedTags)
        {
	        int count = selectedTags.Count;
	        if (count == 0)
	        {
		        return;
	        }
	        if (MessageBox.Show(
		            $"Do you want to add these {count} tags to EVERY piece of media in the album? Make sure these are only things (like a character) that are present in each piece of media, not just in one.",
		            "Confirm adding Items",
		            MessageBoxButton.YesNo,
		            MessageBoxImage.Question
	            ) == MessageBoxResult.No)
	        {
		        return;
	        }
	        foreach (MediaItem albumItemMediaItem in _albumItem.MediaItems)
	        {
		        bool anyChanged = false;
		        
		        foreach (TagItem tag in selectedTags)
		        {
			        if (albumItemMediaItem.AddTag(tag))
			        {
				        anyChanged = true;
			        }
		        }

		        if (anyChanged)
		        {
			        MediaRepository.UpdateMedia(albumItemMediaItem);
		        }
	        }
        }

        public List<TagItem> GetParents => new List<TagItem>();

	}
}