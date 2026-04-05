using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Filterizer2.Repositories;
using Microsoft.Win32;
using Vlc.DotNet.Core;
using Vlc.DotNet.Core.Interops.Signatures;
using XamlAnimatedGif;
using Path = System.IO.Path;
using Rectangle = System.Drawing.Rectangle;

namespace Filterizer2.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow: IHasFilter
    {
        private readonly DispatcherTimer _timer;
        private bool _wasPlayingBeforeSeek = false;

        
        public MainWindow()
        {
            
            
            
            
            
            
            DeleteOrphans();
            InitializeComponent();
            ReloadAllMediaItems();

            SorterSelectorBox.ItemsSource = MediaSorter.Sorters;
            SorterSelectorBox.SelectedIndex = 0;
            
            ThemeSelectorBox.ItemsSource = Theme.Themes;
            ThemeSelectorBox.SelectedIndex = 0;
            
            //Initialize timer
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500) // Update every 500 ms
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        
            // Set the VLC library path
            string libDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libvlc");
            VlcPlayer.SourceProvider.CreatePlayer(new DirectoryInfo(libDirectory));
        }
        
        
        private IMediaDisplayItem? CurrentlySelectedItem => MediaListBox.SelectedItem as IMediaDisplayItem;

        private static void DeleteOrphans()
        {
        
            string mediaDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Media");
            string thumbsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Thumbs");

            if (!Directory.Exists(mediaDirectory))
            {
                Directory.CreateDirectory(mediaDirectory);
            }
            if (!Directory.Exists(thumbsDirectory))
            {
                Directory.CreateDirectory(thumbsDirectory);
            }

            
            string[] allRealMediaItems = MediaRepository.GetAllMediaItems().Select(item => Path.GetFileNameWithoutExtension(item.LocalFilename)).ToArray();
        
        
            DeleteOrphansIn(mediaDirectory);
            DeleteOrphansIn(thumbsDirectory);
        
        

            return;
        
            void DeleteOrphansIn(string directory)
            {
                string[] existingFiles = Directory.GetFiles(directory);
            
                foreach (string existingFile in existingFiles)
                {
                    if (allRealMediaItems.Contains(Path.GetFileNameWithoutExtension(existingFile))) continue;
                    try
                    {
                        File.Delete(existingFile);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to delete orphaned file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    
        private void LoadMediaButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Media Files|*.png;*.jpg;*.gif;*.webp;*.webm;*.mp4",
                Multiselect = true
            };
            
            

            if (openFileDialog.ShowDialog() != true) return;

            string[] fileNames = openFileDialog.FileNames;
            string destinationFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Media");
            string thumbsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Thumbs");

            // Ensure destination folder exists
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }
            if (!Directory.Exists(thumbsDirectory))
            {
                Directory.CreateDirectory(thumbsDirectory);
            }

            bool deleteMode = ManagementHelpers.ShowConfirmationDialog("Delete the original?");

            bool singleMode = fileNames.Length == 1;

            TagItem? WIPTag = null;

            if (!singleMode)
            {
	            WIPTag = TagRepository.SearchTags("Tagging_In_Progress").FirstOrDefault();
            }

            List<MediaItem> addedItems = new List<MediaItem>();
            
            foreach (string sourceFilePath in fileNames)
            {
	            // Copy the media file
	            string? localFilePath = ManagementHelpers.CopyMediaToLocalFolder(sourceFilePath, destinationFolder);

	            if (deleteMode)
	            {
		            try
		            {
			            File.Delete(sourceFilePath);
		            }
		            catch (Exception ex)
		            {
			            MessageBox.Show($"Failed to delete orphaned file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
		            }
	            }

	            if (localFilePath == null) return;
	            // Generate or retrieve the thumbnail
	            ThumbnailGenerator.GenerateOrGetThumbnail(localFilePath);

	            MediaItem item;
	            
	            if (singleMode)
	            {
		            // Open the NewMediaEntryWindow to get user input, only if it's a single item
		            EditMediaEntryWindow entryWindow = new EditMediaEntryWindow(localFilePath);
		            entryWindow.ShowDialog();
		            
		            // Create the MediaItem with user-provided details
		            item = entryWindow.GetMediaItem();
	            }
	            else
	            {
		            // Create an empty MediaItem, and add the "tagging in progress" tag
		            item = new MediaItem
		            {
			            Title = "",
			            Description = "",
			            LocalFilename = Path.GetFileName(localFilePath)
		            };
		            if (WIPTag != null) item.AddTag(WIPTag);
	            }

	            addedItems.Add(item);

	            // Store media in database
	            MediaRepository.AddMedia(item);

	            // Add to MediaListBox
	            MediaListBox.Items.Add(item);
            }
            
            
            if (!singleMode)
            {
	            new EditAlbumWindow(null, addedItems).ShowDialog();
            
	            ReloadAllMediaItems();
            }
        }

        private Brush MediaBorderBrush = Brushes.LightGray;
        private Brush AlbumBorderBrush = Brushes.LightBlue;
    
        private void MediaListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (CurrentlySelectedItem)
            {
                case MediaItem mediaItem:
                    ShowMedia(mediaItem.LocalFilename);
                    SetMediaTray();
                    SetHiddenAlbumTray();
                    
                    MediaBorder.BorderThickness = new Thickness(1);
                    MediaBorder.BorderBrush = MediaBorderBrush;
                    break;
                case AlbumItem albumItem:
                    ShowAlbum(albumItem);
                    SetHiddenMediaTray();
                    SetAlbumTray();
                    
                    MediaBorder.BorderThickness = new Thickness(4);
                    MediaBorder.BorderBrush = AlbumBorderBrush;
                    break;
                default:
                    StopShowingMedia();
                    SetHiddenMediaTray();
                    break;
            }
        }

        private void ShowAlbum(AlbumItem albumItem)
        {
            MediaItem? sel = albumItem.GetCurrentMediaItem();
            if (sel != null)
            {
                ShowMedia(sel.LocalFilename);
                SyncAlbumButtons(albumItem);
            }
        }

        private void SetAlbumTray()
        {
            AlbumTray.Visibility = Visibility.Visible;
            DeleteAlbumButton.IsEnabled = true;
            EditAlbumButton.IsEnabled = true;
        }

        private void SetMediaTray()
        {
	        MediaTray.Visibility = Visibility.Visible;
            DeleteMediaButton.IsEnabled = true;
            EditMediaButton.IsEnabled = true;
            FileExplorerButton.IsEnabled = true;
        }
        
        private void SetHiddenAlbumTray()
        {
	        AlbumTray.Visibility = Visibility.Collapsed;
            DeleteAlbumButton.IsEnabled = false;
            EditAlbumButton.IsEnabled = false;
        }

        private void SetHiddenMediaTray()
        {
	        MediaTray.Visibility = Visibility.Collapsed;
            DeleteMediaButton.IsEnabled = false;
            EditMediaButton.IsEnabled = false;
            FileExplorerButton.IsEnabled = false;
        }

        public void ReloadAllMediaItems()
        {
	        //TODO make this lazy evaled?
	        //TODO How to do that with a sorter?
            List<IMediaDisplayItem> displayItems = new List<IMediaDisplayItem>();

            if (ShowMediaCheckbox.IsChecked == true)
            {
                displayItems.AddRange(MediaRepository.GetAllMediaItems());
            }
            if (ShowAlbumsCheckbox.IsChecked == true)
            {
                displayItems.AddRange(AlbumRepository.GetAlbums());
            }

            displayItems = displayItems.Where(mediaItem => Filter.TestMedia(mediaItem)).ToList();
            
            displayItems.Sort(Sorter);
            
            MediaListBox.Items.Clear();
            
            foreach (IMediaDisplayItem mediaItem in displayItems)
            {
                MediaListBox.Items.Add(mediaItem);
            }
        }

        private VlcMediaPlayer CurrentPlayer => VlcPlayer.SourceProvider.MediaPlayer;

        private MediaSorter Sorter = new UnsortedSorter();
        
        public MediaSearchFilter Filter { get; private set; } = new SearchFilterOpen(new List<TagFilter>());
        public void SetFilter(MediaSearchFilter filter)
        {
            Filter = filter;
        }

        private void StopShowingMedia()
        {
            HideMedia();
            ImageView.Source = null;
            VlcPlayer.SourceProvider.Dispose();
            var libDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libvlc");
            VlcPlayer.SourceProvider.CreatePlayer(new DirectoryInfo(libDirectory));
        }

        private void HideMedia()
        {
            ImageView.Visibility = Visibility.Collapsed;
            VlcPlayer.Visibility = Visibility.Collapsed;
            ControlTray.Visibility = Visibility.Collapsed;
            VlcPlayer.SourceProvider?.MediaPlayer?.Pause();
            CurrentPlayer.Audio.IsMute = true;
        }
    
        private void ShowMedia(string filePath)
        {
	        
            bool mustEndInit = false;
            if (!ImageView.IsInitialized)
            {
                ImageView.BeginInit();
                mustEndInit = true;
            }
        
            HideMedia();
        
        
        
            filePath = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Media"), filePath);

            var extension = Path.GetExtension(filePath).ToLower();
            switch (extension)
            {
                case ".png" or ".jpg":
                {
                    var image = new BitmapImage(new Uri(filePath));
                    ImageView.Source = image;
                    ImageView.Visibility = Visibility.Visible;
                    break;
                }
                case ".webp":
                    ImageView.Source = ImageHelpers.ConvertBitmapToBitmapImage(new FileInfo(filePath).NewBitmap());
                    ImageView.Visibility = Visibility.Visible;
                    break;
                case ".gif":
                {
                    AnimationBehavior.SetSourceUri(ImageView, new Uri(filePath));
                    AnimationBehavior.SetRepeatBehavior(ImageView, System.Windows.Media.Animation.RepeatBehavior.Forever);
                    ImageView.Visibility = Visibility.Visible;
                    break;
                }
                case ".webm" or ".mp4":
                    CurrentPlayer.Play(new Uri(filePath));
                    VlcPlayer.Visibility = Visibility.Visible;
                    ControlTray.Visibility = Visibility.Visible;
                    CurrentPlayer.Audio.IsMute = false;
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

        private void CreateAlbum(List<MediaItem>? startingItems = null)
        {
	        new EditAlbumWindow(null, startingItems).ShowDialog();
            
	        ReloadAllMediaItems();
        }
    
        private void OpenTagDictionaryButton_Click(object sender, RoutedEventArgs e)
        {
            TagDictionaryWindow tagDictionaryWindow = new TagDictionaryWindow();
            tagDictionaryWindow.Show();
        }
    
    
        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            new FilterWindow(this, Filter).ShowDialog();
        }
        
        private void VisibilityButtonChecked(object sender, RoutedEventArgs e)
        {
            ReloadAllMediaItems();
        }

        private void SorterSelectorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SorterSelectorBox.SelectedItem is not MediaSorter mediaSorter) return;
            Sorter = mediaSorter;
            if (Sorter is RandomSorter randomSorter)
            {
	            randomSorter.Randomize();
            } 
            ReloadAllMediaItems();
        }

        private void DeleteMediaButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentlySelectedItem is not MediaItem mediaItem) return;
            if (!ManagementHelpers.ShowConfirmationDialog($"Are you sure you want to delete '{mediaItem.Title}'"))
                return;
            //Remove from listItem
            MediaListBox.SelectedItem = null;
            MediaListBox.Items.Remove(mediaItem);
            //Remove from database
            MediaRepository.DeleteMedia(mediaItem);
            SetHiddenMediaTray();
            //Deleting the actual file is done on launch, before they are locked by application.
        }

        private void EditMediaButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentlySelectedItem is not MediaItem mediaItem) return;
            EditMediaEntryWindow entryWindow = new EditMediaEntryWindow(mediaItem);
            entryWindow.ShowDialog();

            MediaRepository.UpdateMedia(mediaItem);

            ReloadAllMediaItems();
        }

        private void CreateAlbumButton_Click(object sender, RoutedEventArgs e)
        {
	        CreateAlbum();
        }

        private void DeleteAlbumButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentlySelectedItem is not AlbumItem albumItem) return;
            if (!ManagementHelpers.ShowConfirmationDialog($"Are you sure you want to delete '{albumItem.Name}'")) return;
            //Remove from listItem
            MediaListBox.SelectedItem = null;
            MediaListBox.Items.Remove(albumItem);
            //Remove from database
            AlbumRepository.DeleteAlbum(albumItem);
            SetHiddenMediaTray();
        }

        private void EditAlbumButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentlySelectedItem is not AlbumItem albumItem) return;
            new EditAlbumWindow(albumItem).ShowDialog();
            
            ReloadAllMediaItems();
        }

        private void RightButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentlySelectedItem is not AlbumItem albumItem) return;
            albumItem.NavigateRight();
            ShowAlbum(albumItem);
        }

        private void LeftButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentlySelectedItem is not AlbumItem albumItem) return;
            albumItem.NavigateLeft();
            ShowAlbum(albumItem);
        }

        private void SyncAlbumButtons(AlbumItem albumItem)
        {
            RightButton.IsEnabled = albumItem.CanNavigateRight;
            LeftButton.IsEnabled = albumItem.CanNavigateLeft;
        }
        
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentPlayer.Play();
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentPlayer.Pause();
        }

        private void RewindButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentPlayer.Length <= 0) return;
            var currentTime = CurrentPlayer.Time;
            
            EnsureMediaKeepsPlaying();
            
            CurrentPlayer.Time = Math.Max(currentTime - 10000, 0); // Rewind 10 seconds
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentPlayer.Length <= 0) return;
            var currentTime = CurrentPlayer.Time;
            CurrentPlayer.Time = Math.Min(currentTime + 10000, CurrentPlayer.Length); // Forward 10 seconds
        }
        
        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (CurrentPlayer.Length <= 0) return;
            
            // Update the slider
            VideoSeekBar.Maximum = CurrentPlayer.Length;
            VideoSeekBar.Value = CurrentPlayer.Time;

            // Update the timer text
            TimeSpan currentTime = TimeSpan.FromMilliseconds(CurrentPlayer.Time);
            TimeSpan totalTime = TimeSpan.FromMilliseconds(CurrentPlayer.Length);
            TimerText.Text = $@"{currentTime:mm\:ss} / {totalTime:mm\:ss}";
        }

        private void VideoSeekBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            EnsureMediaKeepsPlaying();
            
            // Only seek if the user is interacting with the slider
            if (Math.Abs(CurrentPlayer.Time - VideoSeekBar.Value) > 1000)
            {
                CurrentPlayer.Time = (long)VideoSeekBar.Value;
            }
        }
        
        private void SeekBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Check if the video is currently playing
            if (CurrentPlayer.IsPlaying())
            {
                CurrentPlayer.Pause();
                _wasPlayingBeforeSeek = true;  // Remember that it was playing
            }
            else
            {
                _wasPlayingBeforeSeek = false;  // Remember that it was paused
            }
        }

        private void SeekBar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_wasPlayingBeforeSeek)
            {
                CurrentPlayer.Play();
            }
        }

        private void EnsureMediaKeepsPlaying()
        {
            if (CurrentPlayer.State != MediaStates.Ended) return;
            CurrentPlayer.SetMedia(CurrentPlayer.GetMedia().Mrl);
            CurrentPlayer.Play();
        }

        private void MediaFullscreenButton_OnClick(object sender, RoutedEventArgs e)
        {
	        SetFullscreen(true);
        }

        
        private void FileExplorerButton_OnClick(object sender, RoutedEventArgs e)
        {
	        if (MediaListBox.SelectedItem is MediaItem mediaItem)
	        {
		        OpenFileExplorerAndSelectFile(mediaItem.MediaFilePath);
	        }
        }
        
        private void OpenFileExplorerAndSelectFile(string filePath)
        {
	        if (File.Exists(filePath) || Directory.Exists(filePath))
	        {
		        Process.Start("explorer.exe", "/select,\"" + filePath + "\"");
	        }
	        else
	        {
		        MessageBox.Show("The specified file or directory could not be found.");
	        }
        }
        
        private bool _isFullscreen;
        private WindowState _nonFullscreenWindowState;

        private int? _savedSorterValue;
        private int? _savedThemeValue;
        private int? _savedMediaValue;
        
        private void SetFullscreen(bool fullscreen)
        {
	        if (fullscreen == _isFullscreen) return;
	        _isFullscreen = fullscreen;

	        _savedSorterValue = SorterSelectorBox.SelectedIndex;
	        _savedThemeValue = ThemeSelectorBox.SelectedIndex;
	        _savedMediaValue = MediaListBox.SelectedIndex;
	        
	        
	        Visibility vis;
	        if (fullscreen)
	        {
		        _nonFullscreenWindowState = WindowState;
		        
		        vis = Visibility.Collapsed;
		        WindowStyle = WindowStyle.None;
		        WindowState = WindowState.Maximized;
	        }
	        else
	        {
		        vis = Visibility.Visible;
		        WindowState = _nonFullscreenWindowState;
		        WindowStyle = WindowStyle.SingleBorderWindow;
	        
		        if (WindowState == WindowState.Normal)
		        {
			        Width = RestoreBounds.Width;
			        Height = RestoreBounds.Height;
			        Left = RestoreBounds.Left;
			        Top = RestoreBounds.Top;
		        }

	        }

	        MediaListBoxColumnDefinition.Width = new GridLength(fullscreen ? 0 : 200);

	        MediaControlPanel.Visibility = vis;
	        MediaListBox.Visibility = vis;
	        MetaToolbar.Visibility = vis;

        }

        private void AnyBearingPanel_OnSizeChanged(object? sender, EventArgs eventArgs)
        {
	        if (_savedSorterValue != null)
	        {
		        SorterSelectorBox.SelectedIndex = (int)_savedSorterValue;
	        }
	        if (_savedMediaValue != null)
	        {
		        MediaListBox.SelectedIndex = (int)_savedMediaValue;
	        }
	        if (_savedThemeValue != null)
	        {
		        ThemeSelectorBox.SelectedIndex = (int)_savedThemeValue;
	        }
        }

        private void OnUserRetakingControl()
        {
	        _savedSorterValue = null;
	        _savedThemeValue = null;
	        _savedMediaValue = null;
        }
        
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
	        OnUserRetakingControl();
	        
	        switch (e.Key)
	        {
		        case Key.Escape:
			        SetFullscreen(false);
			        return;
		        case Key.F:
			        SetFullscreen(true);
			        break;
		        case Key.Up or Key.PageUp:
		        {
			        if (MediaListBox.SelectedIndex > 0) MediaListBox.SelectedIndex -= 1;
			        break;
		        }
		        case Key.Down or Key.PageDown:
		        {
			        if (MediaListBox.SelectedIndex < MediaListBox.Items.Count - 1)
			        {
				        MediaListBox.SelectedIndex++;
        
				        MediaListBox.ScrollIntoView(MediaListBox.SelectedItem);
			        }

			        break;
		        }
		        case Key.Left:
		        {
			        if (CurrentlySelectedItem is not AlbumItem albumItem) return;
			        albumItem.NavigateLeft();
			        ShowAlbum(albumItem);
			        break;
		        }
		        case Key.Right:
		        {
			        if (CurrentlySelectedItem is not AlbumItem albumItem) return;
			        albumItem.NavigateRight();
			        ShowAlbum(albumItem);
			        break;
		        }
	        }
        }
        

        private void ThemeSelectorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
	        if (ThemeSelectorBox.SelectedItem is not Theme theme) return;
	        
	        List<FrameworkElement> frameworkElements = new List<FrameworkElement>();
	        GetLogicalChildCollection(Window, frameworkElements);
	        foreach (FrameworkElement frameworkElement in frameworkElements)
	        {
		        frameworkElement.SetFrameworkElementBrushes(theme.GetBrushesForElement(frameworkElement));
	        }
        }
        
        private static void GetLogicalChildCollection<T>(DependencyObject parent, List<T> logicalCollection) where T : DependencyObject
        {
	        IEnumerable children = LogicalTreeHelper.GetChildren(parent);
	        foreach (object child in children)
	        {
		        if (child is not DependencyObject depChild) continue;
		        if (depChild is T dependencyObject)
		        {
			        logicalCollection.Add(dependencyObject);
		        }
		        GetLogicalChildCollection(depChild, logicalCollection);
	        }
        }

        private void MainWindow_OnMouseMove(object sender, MouseEventArgs e)
        {
	        OnUserRetakingControl();
	        
	        if (_isFullscreen)
	        {
		        double x = e.GetPosition(Window).X;
		        double y = e.GetPosition(Window).Y;
		        bool shouldShowTray = (x < Window.ActualWidth / 6) | y > Window.ActualHeight * .9;
		        
		        MediaListBoxColumnDefinition.Width = new GridLength(shouldShowTray ? 200 : 0);
		        MediaListBox.Visibility = shouldShowTray ? Visibility.Visible : Visibility.Collapsed;
		        MediaControlPanel.Visibility = shouldShowTray ? Visibility.Visible : Visibility.Collapsed;
	        }
        }
    }
}