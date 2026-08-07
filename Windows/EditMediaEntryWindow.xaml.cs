using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using XamlAnimatedGif;
using MessageBox = System.Windows.MessageBox;

namespace Filterizer2.Windows
{
    public partial class EditMediaEntryWindow: INotifyPropertyChanged
    {
	    public event PropertyChangedEventHandler? PropertyChanged;

        private readonly string? _mediaFilePath;
        private readonly MediaItem _editingMediaItem;
        
        private bool _isEditMode;
        private bool _deleteMode;

        public bool IsDeleteMode => _deleteMode;
        
        private ObservableCollection<TagItem> _currentTags = new ObservableCollection<TagItem>();

        public ObservableCollection<TagItem> CurrentTags => _currentTags;
        
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
                LocalFilename = Path.GetFileName(mediaFilePath),
            };
            _mediaFilePath = mediaFilePath;
            
            SetUp(true);
        }

        private void SetUp(bool isMakingForFirstTime = false)
        {
            InitializeComponent();

            CurrentTagsItemsControl.ItemsSource = CurrentTags;
        
            
            LoadMediaPreview();
            TitleTextBox.Text = _editingMediaItem.Title;
            DescriptionTextBox.Text = _editingMediaItem.Description;
            
            foreach (TagItem currentTag in _editingMediaItem.GetTags())
            {
	            _currentTags.Add(currentTag);
            }
             
            //Generate the thumbnail
            ThumbnailGenerator.GenerateOrGetThumbnail(_mediaFilePath);
            
            
            //When creating a new media item, ask if the user wants to use advanced tagger
            if (isMakingForFirstTime)
            {
	            if (MessageBox.Show(
		                "Do you want to use the advanced tagger?",
		                "Flow tagger",
		                MessageBoxButton.YesNo,
		                MessageBoxImage.Question
	                ) == MessageBoxResult.Yes)
	            {
		            OpenAdvancedTagger();
	            }
	            else
	            {
		            if (TagRepository.TryGetTaggingInProgressTag(out TagItem taggingInProgressTag))
		            {
			            _currentTags.Add(taggingInProgressTag);
		            }
	            }
            }
        }
        
        public MediaItem GetMediaItem()
        {
            return _editingMediaItem;
        }
        
        private void LoadMediaPreview()
        {
	        if (_mediaFilePath != null)
	        {
		        MediaPlayer.ShowMedia(_mediaFilePath);
	        }
        }

        protected override void OnClosed(EventArgs e)
        {
	        base.OnClosed(e);
	        DialogResult ??= false;
	        MediaPlayer.Dispose();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
	        _editingMediaItem.Title = TitleTextBox.Text;
	        _editingMediaItem.Description = DescriptionTextBox.Text;
	        _editingMediaItem.SetTags(_currentTags.ToList());
	        _editingMediaItem.LocalFilename = Path.GetFileName(_mediaFilePath);
	        // _editingMediaItem.IsSingle = IsSingleCheckbox.IsChecked ?? false;
	        
	        MediaRepository.UpdateMedia(_editingMediaItem);
            DialogResult = true;
            Close();
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
	        
	        if (_currentTags.All(tag => tag.Id != selectedTag.Id))
	        {
		        _currentTags.Add(selectedTag);
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
            _currentTags.Remove(tagToRemove);
        }

        // Ensure currentTags are accessible when saving
        // public List<TagItem> GetSelectedTags()
        // {
        //     return _currentTags;
        // }
        
        private void OpenTagDictionaryButton_Click(object sender, RoutedEventArgs e)
        {
	        TagDictionaryWindow tagDictionaryWindow = new TagDictionaryWindow();
	        MediaPlayer.PausePlayer();
	        tagDictionaryWindow.Show();
        }

        private void OpenAdvancedTaggerButton_Click(object sender, RoutedEventArgs e)
        {
	        OpenAdvancedTagger();
        }

        private void OpenAdvancedTagger()
        {
	        // var result = MediaMessageBox.Show(
		       //  "Is this a single or a compilation media? A media should be single if there is one scene where no characters enter or leave, and the action does not change significantly. This is used to track if tags should be mutually exclusive; for example normally marking a media as 'solo' would hide all sex-related tags. But if your media has somebody masturbating alone and somebody enters, it is possible to have both 'solo' and 'sex' in one media.",
		       //  _mediaFilePath,
		       //  "Single or Compilation?",
		       //  MediaMessageBox.MediaMessageBoxButton.SingleCompilation);
	        //
	        // bool isSingle;
	        // if (result == MediaMessageBox.MediaMessageBoxResult.Yes)
	        // {
		       //  //It is a single
		       //  isSingle = true;
	        // }
	        // else
	        // {
		       //  //It is a compilation
		       //  isSingle = false;
	        // }
	        
	        MediaTaggingHelperWindow mediaTaggerWindow = new MediaTaggingHelperWindow(_mediaFilePath, OnTagSelectionComplete, _currentTags);
	        MediaPlayer.PausePlayer();
	        mediaTaggerWindow.ShowDialog();
        }

        private void OnTagSelectionComplete(List<TagItem> tags)
        {
	        _currentTags.Clear();
	        foreach (TagItem tagItem in tags)
	        {
		        _currentTags.Add(tagItem);
	        }
        }
        
        
        // public void SetTagList(IEnumerable<TagItem> tags)
        // {
	       //  currentTags.Clear();
	       //  foreach (TagItem tagItem in tags)
	       //  {
		      //   currentTags.Add(tagItem)
	       //  }
        // }

        private void ToggleDelete_OnClick(object sender, RoutedEventArgs e)
        {
	        _deleteMode = !_deleteMode;
	        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDeleteMode)));
        }

        private void OpenHierarchyViewMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
	        if (sender is not MenuItem { CommandParameter: TagItem tagToView }) return;
	        var tagHierarchyWindow = new ViewMasterTagHierarchy(tagToView);
	        MediaPlayer.PausePlayer();
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