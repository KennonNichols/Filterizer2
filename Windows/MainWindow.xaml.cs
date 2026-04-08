using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Filterizer2.Repositories;
using Microsoft.Win32;
using Vlc.DotNet.Core;
using Vlc.DotNet.Core.Interops.Signatures;
using XamlAnimatedGif;
using static Filterizer2.MediaExtension;
using Path = System.IO.Path;

namespace Filterizer2.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow: IHasFilter
    {
        private readonly DispatcherTimer _timer;
        private bool _wasPlayingBeforeSeek = false;
        private MediaExtension _currentShownExtension = Unsupported;
        
        public MainWindow()
        {
	        DeleteOrphans();
	        SettingsManager.Load();
	        InitializeComponent();
	        ReloadAllMediaItems();
	        
            SorterSelectorBox.ItemsSource = MediaSorter.Sorters;
            SorterSelectorBox.SelectedIndex = 0;
            
            ThemeSelectorBox.ItemsSource = Theme.Themes;
            ThemeSelectorBox.SelectedIndex = SettingsManager.ThemeIndex;

            Width = SettingsManager.WindowWidth;
            Height = SettingsManager.WindowHeight;
            
            Left = SettingsManager.WindowX;
            Top = SettingsManager.WindowY;
            
            //Initialize timer
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500) //Update every 500 ms
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        
            //Set the VLC library path
            string libDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libvlc");
            VlcPlayer.SourceProvider.CreatePlayer(new DirectoryInfo(libDirectory));
        }

        protected override void OnClosing(CancelEventArgs e)
        {
	        SettingsManager.SetTheme(ThemeSelectorBox.SelectedIndex);
	        SettingsManager.SetWindowHeight(Height);
	        SettingsManager.SetWindowWidth(Width);
	        SettingsManager.SetWindowX(Left);
	        SettingsManager.SetWindowY(Top);
	        SettingsManager.Save();
	        base.OnClosing(e);
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


            HashSet<string> allRealMediaNames = MediaRepository.GetAllMediaNames().ToHashSet();
        
        
            DeleteOrphansIn(mediaDirectory);
            DeleteOrphansIn(thumbsDirectory);
            
            return;
        
            void DeleteOrphansIn(string directory)
            {
                string[] existingFiles = Directory.GetFiles(directory);
            
                foreach (string existingFile in existingFiles)
                {
                    if (allRealMediaNames.Contains(Path.GetFileNameWithoutExtension(existingFile))) continue;
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
	            // if (addedItems.RemoveAll(item => item.LocalFilename.MediaExtension().IsSeekable()) > 0)
	            // {
		           //  MessageBox.Show("Some of the items added were videos. Videos cannot be placed in albums, and ", "Error playing media",
			          //   MessageBoxButton.OK, MessageBoxImage.Information);
	            // }
	            
	            new EditAlbumWindow(null, addedItems).ShowDialog();
            
	            ReloadAllMediaItems();
            }
        }

        private Brush MediaBorderBrush = Brushes.LightGray;
        private Brush AlbumBorderBrush = Brushes.LightBlue;
    
        private async void MediaListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (CurrentlySelectedItem)
            {
                case MediaItem mediaItem:
                    ShowMedia(mediaItem.LocalFilename);
                    SetMediaTray();
                    SetHiddenAlbumTray();
                    
                    MediaBorder.BorderThickness = new Thickness(0);
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
	        ignoreOneMove = true;
            AlbumTray.Visibility = Visibility.Visible;
            DeleteAlbumButton.IsEnabled = true;
            EditAlbumButton.IsEnabled = true;
        }

        private void SetMediaTray()
        {
	        ignoreOneMove = true;
	        ignoreOneMove = true;
	        MediaTray.Visibility = Visibility.Visible;
            DeleteMediaButton.IsEnabled = true;
            EditMediaButton.IsEnabled = true;
            FileExplorerButton.IsEnabled = true;
        }
        
        private void SetHiddenAlbumTray()
        {
	        ignoreOneMove = true;
	        AlbumTray.Visibility = Visibility.Collapsed;
            DeleteAlbumButton.IsEnabled = false;
            EditAlbumButton.IsEnabled = false;
        }

        private void SetHiddenMediaTray()
        {
	        ignoreOneMove = true;
	        MediaTray.Visibility = Visibility.Collapsed;
            DeleteMediaButton.IsEnabled = false;
            EditMediaButton.IsEnabled = false;
            FileExplorerButton.IsEnabled = false;
        }

        public void ReloadAllMediaItems()
        {


	        HashSet<int> loadedMediaIDs = new HashSet<int>();
	        List<IMediaDisplayItem> displayItems = new List<IMediaDisplayItem>();
	        
	        if (ShowMediaCheckbox.IsChecked == true)
	        {
		        foreach (MediaItem allMediaItem in MediaRepository.GetAllMediaItems())
		        {
			        displayItems.Add(allMediaItem);
			        loadedMediaIDs.Add(allMediaItem.Id);
		        }
	        }
	        
            if (ShowAlbumsCheckbox.IsChecked == true)
            {
	            
	            List<AlbumItem> albums = AlbumRepository.GetAlbums().ToList();
	            
                displayItems.AddRange(albums);
                
                AlbumDupePanel.Visibility = Visibility.Visible;
                if (PreventAlbumDuplicatesCheckbox.IsChecked == true)
                {
	                foreach (var albumItemMediaItem in albums.SelectMany(albumItem => albumItem.MediaItems))
	                {
		                loadedMediaIDs.Add(albumItemMediaItem.Id);
	                }

	                //Remove duplicates
	                displayItems.RemoveAll(item =>
		                item is MediaItem mediaItem && loadedMediaIDs.Contains(mediaItem.Id));
                }
                
                displayItems = displayItems.Where(mediaItem => Filter.TestMedia(mediaItem)).ToList();
            
                displayItems.Sort(Sorter);
            }
            else
            {
	            AlbumDupePanel.Visibility = Visibility.Collapsed;
            }

            MediaListBox.Items.Clear();
            
            foreach (IMediaDisplayItem mediaDisplayItem in displayItems)
            {
	            MediaListBox.Items.Add(mediaDisplayItem);
            }
        }
        

        
        private VlcMediaPlayer? CurrentPlayer => VlcPlayer?.SourceProvider?.MediaPlayer;

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
            ImageView.Visibility = Visibility.Hidden;
            VlcPlayer.Visibility = Visibility.Hidden;
            ControlTray.Visibility = Visibility.Collapsed;
            PausePlayer();
            if (CurrentPlayer != null)
            {
	            CurrentPlayer.Audio.IsMute = true;
            }

            _currentShownExtension = Unsupported;
        }
    
        private async void ShowMedia(string filePath)
        {
	        
            bool mustEndInit = false;
            if (!ImageView.IsInitialized)
            {
                ImageView.BeginInit();
                mustEndInit = true;
            }

            if (!VlcPlayer.IsInitialized)
            {
	            return;
            }
        
            HideMedia();
        
            // AnimationBehavior.SetSourceUri(ImageView, null);
        
        
            filePath = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Media"), filePath);

            _currentShownExtension = filePath.MediaExtension();
            switch (_currentShownExtension)
            {
                case Png or Jpg:
                {
                    var image = new BitmapImage(new Uri(filePath));
                    ImageView.Source = image;
                    ImageView.Visibility = Visibility.Visible;
                    break;
                }
                case Webp:
                    ImageView.Source = ImageHelpers.ConvertBitmapToBitmapImage(new FileInfo(filePath).NewBitmap());
                    ImageView.Visibility = Visibility.Visible;
                    break;
                case Gif:
                {
                    AnimationBehavior.SetSourceUri(ImageView, new Uri(filePath));
                    AnimationBehavior.SetRepeatBehavior(ImageView, System.Windows.Media.Animation.RepeatBehavior.Forever);
                    ImageView.Visibility = Visibility.Visible;
                    break;
                }
                case Webm or Mp4:
	                if (CurrentPlayer == null)
	                {
		                MessageBox.Show("VLC player did not load.", "Error playing media",
			                MessageBoxButton.OK, MessageBoxImage.Error);
		                break;
	                }
	                VlcPlayer.Visibility = Visibility.Visible;
	                ControlTray.Visibility = Visibility.Visible;
	                await PlayPlayer(new Uri(filePath));
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

        private void PausePlayer()
        {
	        CurrentPlayer?.Pause();
	        if(!isFullPaneMode) TogglePlayButton.Content = "Play";
        }
        private async Task PlayPlayer(Uri? mediaFileUri = null)
        {
	        if (mediaFileUri != null)
	        {
		        if (_selectionCts != null)
		        {
			        await _selectionCts.CancelAsync();
		        }
		        _selectionCts = new CancellationTokenSource();
		        var token = _selectionCts.Token;
		        
		        try
		        {
			        await Task.Delay(150, token);

			        if (token.IsCancellationRequested)
				        return;

			        await PlayAsync(mediaFileUri);
		        }
		        catch (TaskCanceledException)
		        {
			        //Expected exception when user scrolls fast
		        }
	        }
	        else
	        {
		        CurrentPlayer?.Play();
	        }
	        if(!isFullPaneMode) TogglePlayButton.Content = "Pause";
        }
        
        private CancellationTokenSource? _selectionCts;
        private readonly SemaphoreSlim _playLock = new SemaphoreSlim(1, 1);
        
        /// <summary>
        /// Unpauses the video or loads a new one. If mediaFileUri is not null, you should await this function.
        /// </summary>
        /// <param name="mediaFileUri"></param>
        public async Task PlayAsync(Uri mediaFileUri)
        {
	        await _playLock.WaitAsync();
	        try
	        {
		        var mediaPlayer = VlcPlayer?.SourceProvider?.MediaPlayer;
		        if (mediaPlayer == null)
			        return;

		        mediaPlayer.Play(mediaFileUri);
	        }
	        finally
	        {
		        _playLock.Release();
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
        
        private void TogglePlayButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePlay();
        }

        private void TogglePlay()
        {
	        if (CurrentPlayer == null) return;

	        if (CurrentPlayer.IsPlaying())
	        {
		        PausePlayer();
	        }
	        else
	        {
		        _ = PlayPlayer();
	        }
        }

        private void RewindButton_Click(object sender, RoutedEventArgs e)
        {
	        //Rewind 10 seconds
	        SeekVideo(-10000);
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
	        //Forward 10 seconds
	        SeekVideo(10000);
        }

        private void SeekVideo(int milliseconds)
        {
	        if (CurrentPlayer == null) return;
	        if (CurrentPlayer.Length <= 0) return;
	        var currentTime = CurrentPlayer.Time;
            
	        EnsureMediaKeepsPlaying();
            
	        CurrentPlayer.Time = Math.Clamp(currentTime + milliseconds, 0, CurrentPlayer.Length);
        }
        
        private void Timer_Tick(object? sender, EventArgs e)
        {
	        if (CurrentPlayer is { Length: > 0 } && !isFullPaneMode)
	        {
		        // Update the slider
		        VideoSeekBar.Maximum = CurrentPlayer.Length;
		        VideoSeekBar.Value = CurrentPlayer.Time;

		        // Update the timer text
		        TimeSpan currentTime = TimeSpan.FromMilliseconds(CurrentPlayer.Time);
		        TimeSpan totalTime = TimeSpan.FromMilliseconds(CurrentPlayer.Length);
		        TimerText.Text = $@"{currentTime:mm\:ss} / {totalTime:mm\:ss}";
	        }
            
            // Hide if fullscreen and not active
            if (_isFullscreen)
            {
	            // double x = e.GetPosition(Window).X;
	            // double y = e.GetPosition(Window).Y;
	            // bool shouldShowTray = (x < Window.ActualWidth / 6) | y > Window.ActualHeight * .9;

	            bool shouldBeFullPane = Environment.TickCount - lastMoveTime > 3000;
	            SetFullPaneMode(shouldBeFullPane);
            }
        }
        
        private bool SetFullPaneMode(bool fullPaneMode)
        {
	        if (fullPaneMode == isFullPaneMode)
	        {
		        return false;
	        }

	        MediaListBoxColumnDefinition.Width = new GridLength(!fullPaneMode ? 200 : 0);
	        MediaListBox.Visibility = !fullPaneMode ? Visibility.Visible : Visibility.Collapsed;
	        MediaControlPanel.Visibility = !fullPaneMode ? Visibility.Visible : Visibility.Collapsed;
	        MetaToolbar.Visibility = !fullPaneMode ? Visibility.Visible : Visibility.Collapsed;
	        ignoreOneMove = true;
	        isFullPaneMode = fullPaneMode;
	        return true;
        }

        private void VideoSeekBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
	        if (CurrentPlayer == null) return;
            EnsureMediaKeepsPlaying();
            
            // Only seek if the user is interacting with the slider
            if (Math.Abs(CurrentPlayer.Time - VideoSeekBar.Value) > 1000)
            {
                CurrentPlayer.Time = (long)VideoSeekBar.Value;
            }
        }
        
        private void SeekBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
	        if (CurrentPlayer == null) return;
            // Check if the video is currently playing
            if (CurrentPlayer.IsPlaying())
            {
                PausePlayer();
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
                _ = PlayPlayer();
            }
        }
        
        private void VolumeBar_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
	        if (CurrentPlayer == null) return;
		    CurrentPlayer.Audio.Volume = (int)VolumeBar.Value;
        }

        private void EnsureMediaKeepsPlaying()
        {
	        if (CurrentPlayer == null) return;
            if (CurrentPlayer.State != MediaStates.Ended) return;
            CurrentPlayer.SetMedia(CurrentPlayer.GetMedia().Mrl);
            _ = PlayPlayer();
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

	        SetFullPaneMode(false);
	        
	        _isFullscreen = fullscreen;

	        _savedSorterValue = SorterSelectorBox.SelectedIndex;
	        _savedThemeValue = ThemeSelectorBox.SelectedIndex;
	        _savedMediaValue = MediaListBox.SelectedIndex;

	        
	        
	        Visibility vis;
	        if (fullscreen)
	        {
		        _nonFullscreenWindowState = WindowState;
		        
		        // vis = Visibility.Collapsed;
		        WindowStyle = WindowStyle.None;
		        WindowState = WindowState.Maximized;
	        }
	        else
	        {
		        // vis = Visibility.Visible;
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
	        
	        // MediaListBoxColumnDefinition.Width = new GridLength(fullscreen ? 0 : 200);
	        //
	        // MediaControlPanel.Visibility = vis;
	        // MediaListBox.Visibility = vis;
	        // MetaToolbar.Visibility = vis;

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
        
        private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
	        OnUserRetakingControl();
	        
	        switch (e.Key)
	        {
		        case Key.Space:
		        {
			        TogglePlay();
			        e.Handled = true;
			        break;
		        }
		        case Key.Escape:
		        {
				        
			        SetFullscreen(false);
			        e.Handled = true;
			        break;
		        }
		        case Key.F:
		        {
			        SetFullscreen(true);
			        e.Handled = true;
			        break;
		        }
		        case Key.Up or Key.PageUp:
		        {
			        if (MediaListBox.SelectedIndex > 0)
			        {
				        MediaListBox.SelectedIndex -= 1;
				        
				        MediaListBox.ScrollIntoView(MediaListBox.SelectedItem);
			        }
			        e.Handled = true;
			        break;
		        }
		        case Key.Down or Key.PageDown:
		        {
			        if (MediaListBox.SelectedIndex < MediaListBox.Items.Count - 1)
			        {
				        MediaListBox.SelectedIndex++;
        
				        MediaListBox.ScrollIntoView(MediaListBox.SelectedItem);
			        }
			        e.Handled = true;
			        break;
		        }
		        case Key.Left:
		        {
			        if (CurrentlySelectedItem is AlbumItem albumItem)
			        {
				        albumItem.NavigateLeft();
				        ShowAlbum(albumItem);
				        e.Handled = true;
				        break;
			        }

			        if (_currentShownExtension.IsSeekable())
			        {
				        SeekVideo(-1000);
				        e.Handled = true;
				        break;
			        }
			        break;
		        }
		        case Key.Right:
		        {
			        if (CurrentlySelectedItem is AlbumItem albumItem)
			        {
				        albumItem.NavigateRight();
				        ShowAlbum(albumItem);
				        e.Handled = true;
				        break;
			        }

			        if (_currentShownExtension.IsSeekable())
			        {
				        SeekVideo(1000);
				        e.Handled = true;
				        break;
			        }
			        break;
		        }
	        }
        }
        

        private void ThemeSelectorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
	        if (ThemeSelectorBox.SelectedItem is not Theme theme) return;
	        
	        
	        Application.Current.Resources.MergedDictionaries.Clear();

	        Application.Current.Resources.MergedDictionaries.Add(
		        new ResourceDictionary
		        {
			        Source = new Uri($"Themes/{theme.Label}.xaml", UriKind.Relative)
		        });
        }

        private bool ignoreOneMove = false;
        private int lastMoveTime = -1;
        private bool isFullPaneMode = false;
        
        private void MainWindow_OnMouseMove(object sender, MouseEventArgs e)
        {
	        OnUserRetakingControl();

	        if (ignoreOneMove)
	        {
		        ignoreOneMove = false;
		        return;
	        }
	        lastMoveTime = Environment.TickCount;
        }

        private void MediaViews_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
	        //Sometimes gifs and videos take a while to load, meaning they resize unexpectedly late. It ignores the next move.
	        ignoreOneMove = true;
        }
    }
}