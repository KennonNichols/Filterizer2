using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using XamlAnimatedGif;

namespace Filterizer2.Windows
{
    public partial class EditMediaEntryWindow: INotifyPropertyChanged
    {
        public string MediaTitle => TitleTextBox.Text;
        public string MediaDescription => DescriptionTextBox.Text;
        

        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly string? _mediaFilePath;
        private readonly MediaItem _editingMediaItem;
        
        private bool _isEditMode;
        private bool _deleteMode;

        public bool IsDeleteMode => _deleteMode;
        
        private List<TagItem> currentTags = new List<TagItem>();

        public EditMediaEntryWindow(MediaItem mediaItem)
        {
            _editingMediaItem = mediaItem;
            _mediaFilePath = mediaItem.MediaFilePath;
        
            SetUp();
        }
        
        public EditMediaEntryWindow(string? mediaFilePath)
        {
            _editingMediaItem = new MediaItem
            {
                Title = "",
                Description = "",
                LocalFilename = Path.GetFileName(mediaFilePath)
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
            currentTags.AddRange(_editingMediaItem.GetTags());
            
            foreach (TagItem currentTag in currentTags)
            {
                CurrentTagsItemsControl.Items.Add(currentTag);
            }
             
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
            _editingMediaItem.SetTags(currentTags);
            _editingMediaItem.LocalFilename = Path.GetFileName(_mediaFilePath);
            base.OnClosed(e);
            
            
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
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
	        TagSearchResultsListBox.ItemsSource = !string.IsNullOrWhiteSpace(searchText) ? TagRepository.SearchTags(searchText).Take(10).ToList() : null;
        }

        // Handles adding a tag when a search result is clicked
        private void TagSearchResultsListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
	        if (TagSearchResultsListBox.SelectedItem is not TagItem selectedTag) return;
	        
	        if (currentTags.All(tag => tag.Id != selectedTag.Id))
	        {
		        currentTags.Add(selectedTag);
		        CurrentTagsItemsControl.Items.Add(selectedTag);
	        }
	        else
	        {
		        MessageBox.Show($"Image is already tagged '{selectedTag.Name}'.", "Error",
			        MessageBoxButton.OK, MessageBoxImage.Error);
	        }

	        TagSearchTextBox.Text = string.Empty;
	        TagSearchResultsListBox.ItemsSource = null;
        }

        // Handles removing a tag from the current tags list
        private void RemoveTagButton_Click(object sender, RoutedEventArgs e)
        {
	        if (!_deleteMode)
	        {
		        MessageBox.Show("Please toggle delete mode on to delete.", "Safe mode.",
			        MessageBoxButton.OK, MessageBoxImage.Error);
		        return;
	        }
            if (sender is not MenuItem { CommandParameter: TagItem tagToRemove }) return;
            currentTags.Remove(tagToRemove);
            CurrentTagsItemsControl.Items.Remove(tagToRemove);
        }

        // Ensure currentTags are accessible when saving
        public List<TagItem> GetSelectedTags()
        {
            return currentTags;
        }
        
        private void OpenTagDictionaryButton_Click(object sender, RoutedEventArgs e)
        {
	        TagDictionaryWindow tagDictionaryWindow = new TagDictionaryWindow();
	        tagDictionaryWindow.Show();
        }

        private void ToggleDelete_OnClick(object sender, RoutedEventArgs e)
        {
	        _deleteMode = !_deleteMode;
	        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDeleteMode)));
        }

        private void OpenHierarchyViewMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
	        if (sender is not MenuItem { CommandParameter: TagItem tagToView }) return;
	        var tagHierarchyWindow = new ViewMasterTagHierarchy(tagToView);
	        tagHierarchyWindow.ShowDialog();
        }
    }
        
    public class BindingProxy : Freezable
    {
	    protected override Freezable CreateInstanceCore() => new BindingProxy();

	    public object Data
	    {
		    get => GetValue(DataProperty);
		    set => SetValue(DataProperty, value);
	    }

	    public static readonly DependencyProperty DataProperty =
		    DependencyProperty.Register("Data", typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));
    }
}