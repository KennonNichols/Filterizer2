using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Filterizer2.Repositories;
using Microsoft.Win32;
using Vlc.DotNet.Core;
using Vlc.DotNet.Core.Interops.Signatures;
using XamlAnimatedGif;
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

        
        public MainWindow()
        {
            DeleteOrphans();
            InitializeComponent();
            ReloadAllMediaItems();
            
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

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            ViewingWindow.MaxHeight = sizeInfo.NewSize.Height - 150;
        }
    
        private void LoadMediaButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Media Files|*.png;*.jpg;*.gif;*.webp;*.webm;*.mp4",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() != true) return;
            string sourceFilePath = openFileDialog.FileName;
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

            
            // Copy the media file
            string? localFilePath = ManagementHelpers.CopyMediaToLocalFolder(sourceFilePath, destinationFolder);

            if (ManagementHelpers.ShowConfirmationDialog("Delete the original?"))
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
            
                
            // Open the NewMediaEntryWindow to get user input
            EditMediaEntryWindow entryWindow = new EditMediaEntryWindow(localFilePath);
            entryWindow.ShowDialog();

            // Create the MediaItem with user-provided details
            MediaItem item = entryWindow.GetMediaItem();

            // Store media in database
            MediaRepository.AddMedia(item);

            // Add to MediaListBox
            MediaListBox.Items.Add(item);
        }
    
        private void MediaListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (CurrentlySelectedItem)
            {
                case MediaItem mediaItem:
                    ShowMedia(mediaItem.LocalFilename);
                    SetMediaTray();
                    break;
                case AlbumItem albumItem:
                    ShowAlbum(albumItem);
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
                SetAlbumTray();
                SyncAlbumButtons(albumItem);
            }
            else
            {
                SetHiddenAlbumTray();
            }
        }

        private void SetAlbumTray()
        {
            MediaTray.Visibility = Visibility.Collapsed;
            AlbumTray.Visibility = Visibility.Visible;
            DeleteAlbumButton.IsEnabled = true;
            EditAlbumButton.IsEnabled = true;
        }

        private void SetMediaTray()
        {
            MediaTray.Visibility = Visibility.Visible;
            AlbumTray.Visibility = Visibility.Collapsed;
            DeleteMediaButton.IsEnabled = true;
            EditMediaButton.IsEnabled = true;
        }
        
        private void SetHiddenAlbumTray()
        {
            SetAlbumTray();
            DeleteAlbumButton.IsEnabled = false;
            EditAlbumButton.IsEnabled = false;
        }

        private void SetHiddenMediaTray()
        {
            SetMediaTray();
            DeleteMediaButton.IsEnabled = false;
            EditMediaButton.IsEnabled = false;
        }

        public void ReloadAllMediaItems()
        {
            List<IMediaDisplayItem> displayItems = new List<IMediaDisplayItem>();
            
            displayItems.AddRange(MediaRepository.GetAllMediaItems());
            displayItems.AddRange(AlbumRepository.GetAlbums());
            
            MediaListBox.Items.Clear();
            
            foreach (IMediaDisplayItem mediaItem in displayItems.Where(mediaItem => Filter.TestMedia(mediaItem)))
            {
                //TODO sort here
                MediaListBox.Items.Add(mediaItem);
            }
        }

        private VlcMediaPlayer CurrentPlayer => VlcPlayer.SourceProvider.MediaPlayer;

        public MediaSearchFilter Filter { get; private set; } = new SearchFilterOpen(new List<TagFilter>());
        public void SetFilter(MediaSearchFilter filter)
        {
            Filter = filter;
        }

        private void StopShowingMedia()
        {
            HideMedia();
            ImageView.Source = null;
            VideoPlayer.Source = null;
            VlcPlayer.SourceProvider.Dispose();
            var libDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libvlc");
            VlcPlayer.SourceProvider.CreatePlayer(new DirectoryInfo(libDirectory));
        }

        private void HideMedia()
        {
            ImageView.Visibility = Visibility.Collapsed;
            VideoPlayer.Visibility = Visibility.Collapsed;
            VlcPlayer.Visibility = Visibility.Collapsed;
            VlcPlayer.SourceProvider?.MediaPlayer?.Pause();
            ControlTray.Visibility = Visibility.Collapsed;
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
    
    
        private void OpenTagDictionaryButton_Click(object sender, RoutedEventArgs e)
        {
            TagDictionaryWindow tagDictionaryWindow = new TagDictionaryWindow();
            tagDictionaryWindow.Show();
        }
    
    
        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            new FilterWindow(this, Filter).ShowDialog();
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
            new EditAlbumWindow().ShowDialog();
            
            ReloadAllMediaItems();
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
    }
}