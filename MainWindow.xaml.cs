using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ImageMagick;
using Microsoft.Win32;
using XamlAnimatedGif;
using Path = System.IO.Path;

namespace Filterizer2;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        LoadAllMediaItems();
        
        // Set the VLC library path
        var currentDirectory = new FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location).DirectoryName;
        var libDirectory = Path.Combine(currentDirectory, "libvlc");
        VlcPlayer.SourceProvider.CreatePlayer(new DirectoryInfo(libDirectory));
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

        if (openFileDialog.ShowDialog() == true)
        {
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

            if (localFilePath != null)
            {
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
        }
    }
    
    private void MediaListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MediaListBox.SelectedItem is MediaItem mediaItem)
        {
            ShowMedia(mediaItem.LocalFilename);

            DeleteMediaButton.IsEnabled = true;
            EditMediaButton.IsEnabled = true;
        }
        else
        {
            StopShowingMedia();
            
            DeleteMediaButton.IsEnabled = false;
            EditMediaButton.IsEnabled = false;
        }
    }

    private void LoadAllMediaItems()
    {
        var mediaItems = MediaRepository.GetAllMediaItems();
        foreach (var mediaItem in mediaItems)
        {
            MediaListBox.Items.Add(mediaItem);
        }
    }

    private void StopShowingMedia()
    {
        HideMedia();
        ImageView.Source = null;
        VideoPlayer.Source = null;
        VlcPlayer.SourceProvider.Dispose();
        var currentDirectory = new FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location).DirectoryName;
        var libDirectory = Path.Combine(currentDirectory, "libvlc");
        VlcPlayer.SourceProvider.CreatePlayer(new DirectoryInfo(libDirectory));
    }

    private void HideMedia()
    {
        ImageView.Visibility = Visibility.Collapsed;
        VideoPlayer.Visibility = Visibility.Collapsed;
        VlcPlayer.Visibility = Visibility.Collapsed;
        VlcPlayer.SourceProvider?.MediaPlayer?.Pause();
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
                VlcPlayer.SourceProvider.MediaPlayer.Play(new Uri(filePath));
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
    
    
    private void OpenTagDictionaryButton_Click(object sender, RoutedEventArgs e)
    {
        TagDictionaryWindow tagDictionaryWindow = new TagDictionaryWindow();
        tagDictionaryWindow.Show();
    }
    
    
    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        var searchText = SearchTextBox.Text;
        MediaListBox.Items.Clear();

        throw new NotImplementedException();
    }

    private void DeleteMediaButton_Click(object sender, RoutedEventArgs e)
    {
        if (MediaListBox.SelectedItem is MediaItem mediaItem)
        {
            if (ManagementHelpers.ShowConfirmationDialog($"Are you sure you want to delete '{mediaItem.Title}'"))
            {
                //Remove from listItem
                MediaListBox.SelectedItem = null;
                MediaListBox.Items.Remove(mediaItem);
                //Remove from database
                MediaRepository.DeleteMedia(mediaItem);
                //Stop showing media
                StopShowingMedia();
                //Disable buttons
                DeleteMediaButton.IsEnabled = false;
                EditMediaButton.IsEnabled = false;
                //Delete the files
                try
                {
                    File.Delete(mediaItem.MediaFilePath);
                    File.Delete(mediaItem.ThumbnailPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void EditMediaButton_Click(object sender, RoutedEventArgs e)
    {
        if (MediaListBox.SelectedItem is MediaItem mediaItem)
        {
            EditMediaEntryWindow entryWindow = new EditMediaEntryWindow(mediaItem);
            entryWindow.ShowDialog();
            
            //TODO update database entry

            MediaRepository.UpdateMedia(mediaItem);

            //TODO reload media list
        }
    }
}