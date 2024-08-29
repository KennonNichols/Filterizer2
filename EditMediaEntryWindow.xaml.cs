using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XamlAnimatedGif;

namespace Filterizer2
{
    public partial class EditMediaEntryWindow : Window
    {
        public string MediaTitle => TitleTextBox.Text;
        public string MediaDescription => DescriptionTextBox.Text;

        private readonly string _mediaFilePath;
        private readonly MediaItem _editingMediaItem;
        
        private bool _isEditMode;
        
        private List<TagItem> currentTags = new List<TagItem>();

        public EditMediaEntryWindow(MediaItem mediaItem)
        {
            _editingMediaItem = mediaItem;
            _mediaFilePath = mediaItem.MediaFilePath;
        
            SetUp();
        }
        
        public EditMediaEntryWindow(string mediaFilePath)
        {
            _editingMediaItem = new MediaItem()
            {
                Title = "",
                Description = "",
                LocalFilename = Path.GetFileName(mediaFilePath),
                Tags = new List<TagItem>()
            };
            _mediaFilePath = mediaFilePath;
            
            SetUp();
        }

        private void SetUp()
        {
            InitializeComponent();
        
            // Set the VLC library path
            var currentDirectory = new FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location).DirectoryName;
            var libDirectory = Path.Combine(currentDirectory, "libvlc");
            VlcPlayer.SourceProvider.CreatePlayer(new DirectoryInfo(libDirectory));
            
            LoadMediaPreview();
            TitleTextBox.Text = _editingMediaItem.Title;
            DescriptionTextBox.Text = _editingMediaItem.Description;
            currentTags.AddRange(_editingMediaItem.Tags);
            
            //Generate the thumbnail
            ThumbnailGenerator.GenerateOrGetThumbnail(_mediaFilePath);
        }
        
        public MediaItem GetMediaItem()
        {
            return _editingMediaItem;
        }
        
        private void LoadMediaPreview()
        {
            ShowMedia();
        }

        protected override void OnClosed(EventArgs e)
        {
            _editingMediaItem.Title = TitleTextBox.Text;
            _editingMediaItem.Description = DescriptionTextBox.Text;
            _editingMediaItem.Tags = currentTags;
            _editingMediaItem.LocalFilename = Path.GetFileName(_mediaFilePath);
            base.OnClosed(e);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (VideoPlayer != null)
            {
                VideoPlayer.Source = null;
            }
            if (VlcPlayer != null)
            {
                VlcPlayer.Content = null;
            }
            if (ImageView != null)
            {
                ImageView.Source = null;
            }
            
            DialogResult = false;
            Close();
        }
        
        private void ShowMedia()
        {
            bool mustEndInit = false;
            if (!ImageView.IsInitialized)
            {
                ImageView.BeginInit();
                mustEndInit = true;
            }
            
            // Clear previous media
            ImageView.Visibility = Visibility.Collapsed;
            VideoPlayer.Visibility = Visibility.Collapsed;
            VlcPlayer.Visibility = Visibility.Collapsed;
            VlcPlayer.SourceProvider.MediaPlayer.Pause();
            
            
    
            var extension = Path.GetExtension(_mediaFilePath).ToLower();
            switch (extension)
            {
                case ".png" or ".jpg":
                {
                    var image = new BitmapImage(new Uri(_mediaFilePath));
                    ImageView.Source = image;
                    ImageView.Visibility = Visibility.Visible;
                    break;
                }
                case ".webp":
                    ImageView.Source = ImageHelpers.ConvertBitmapToBitmapImage(new FileInfo(_mediaFilePath).NewBitmap());
                    ImageView.Visibility = Visibility.Visible;
                    break;
                case ".gif":
                {
                    AnimationBehavior.SetSourceUri(ImageView, new Uri(_mediaFilePath));
                    AnimationBehavior.SetRepeatBehavior(ImageView, System.Windows.Media.Animation.RepeatBehavior.Forever);
                    ImageView.Visibility = Visibility.Visible;
                    break;
                }
                case ".webm" or ".mp4":
                    VlcPlayer.SourceProvider.MediaPlayer.Play(new Uri(_mediaFilePath));
                    VlcPlayer.Visibility = Visibility.Visible;
                    break;
                default:
                    MessageBox.Show("Unsupported media format", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
            }
            
            
            if (mustEndInit)
            {
                ImageView.EndInit();
            }
        }
        
        
        // Handles the tag search when the text changes
        private void TagSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = TagSearchTextBox.Text;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                List<TagItem> searchResults = TagRepository.SearchTags(searchText);
                TagSearchResultsListBox.ItemsSource = searchResults.Take(10).ToList();
            }
            else
            {
                TagSearchResultsListBox.ItemsSource = null;
            }
        }

        // Handles adding a tag when a search result is clicked
        private void TagSearchResultsListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (TagSearchResultsListBox.SelectedItem is TagItem selectedTag)
            {
                if (!currentTags.Contains(selectedTag))
                {
                    currentTags.Add(selectedTag);
                    CurrentTagsItemsControl.ItemsSource = null;
                    CurrentTagsItemsControl.ItemsSource = currentTags;
                }

                TagSearchTextBox.Text = string.Empty;
                TagSearchResultsListBox.ItemsSource = null;
            }
        }

        // Handles removing a tag from the current tags list
        private void RemoveTagButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is TagItem tagToRemove)
            {
                currentTags.Remove(tagToRemove);
                CurrentTagsItemsControl.ItemsSource = null;
                CurrentTagsItemsControl.ItemsSource = currentTags;
            }
        }

        // Ensure currentTags are accessible when saving
        public List<TagItem> GetSelectedTags()
        {
            return currentTags;
        }
    }
}