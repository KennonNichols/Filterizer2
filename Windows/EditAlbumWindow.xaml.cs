using System.Windows;
using System.Windows.Controls;
using Filterizer2.Repositories;

namespace Filterizer2.Windows
{
	public partial class EditAlbumWindow : IHasFilter
	{
        private AlbumItem _albumItem;
        private bool _isEditMode;
        private MediaSorter Sorter = new UnsortedSorter();
        public MediaSearchFilter Filter { get; private set; } = new SearchFilterOpen(new List<TagFilter>());
        public void SetFilter(MediaSearchFilter filter)
        {
            Filter = filter;
        }
        

        public EditAlbumWindow(AlbumItem? albumItem = null)
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
                _albumItem = new AlbumItem();
                _isEditMode = false;
            }
            
            ReloadAllMediaItems();
        }
        
        public void ReloadAllMediaItems()
        {
            List<MediaItem> displayItems = MediaRepository.GetAllMediaItems();
            
            
            displayItems = displayItems.Where(mediaItem => Filter.TestMedia(mediaItem)).ToList();
            
            displayItems.Sort(Sorter);
            
            MediaListBox.Items.Clear();
            
            foreach (MediaItem mediaItem in displayItems)
            {
                MediaListBox.Items.Add(mediaItem);
            }
        }

        private void MediaListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (MediaListBox.SelectedItem is not MediaItem selectedMediaItem) return;
            if (_albumItem.MediaItems.Contains(selectedMediaItem)) return;
            _albumItem.MediaItems.Add(selectedMediaItem);
            AlbumContentsListBox.Items.Add(selectedMediaItem);
        }

        private void RemoveMediaItem_Click(object sender, RoutedEventArgs e)
        {
            MediaItem mediaItem = (MediaItem)((Button)sender).Tag;
            _albumItem.MediaItems.Remove(mediaItem);
            AlbumContentsListBox.Items.Remove(mediaItem);
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

        private void FilterMediaByTags_Click(object sender, RoutedEventArgs e)
        {
            FilterWindow filterWindow = new FilterWindow(this, Filter);
            filterWindow.Show();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            _albumItem.Name = TitleTextBox.Text;
            _albumItem.Description = DescriptionTextBox.Text;

            if (_isEditMode)
            {
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
    }
}