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
                string thumbnailPath = ThumbnailGenerator.GenerateThumbnail(localFilePath, thumbsDirectory);

                
                
                
                
                
                // Open the NewMediaEntryWindow to get user input
                NewMediaEntryWindow entryWindow = new NewMediaEntryWindow(localFilePath);
                bool? dialogResult = entryWindow.ShowDialog();

                if (dialogResult == true)
                {
                    // Create the MediaItem with user-provided details
                    MediaItem item = new MediaItem
                    {
                        Title = entryWindow.MediaTitle,
                        Description = entryWindow.MediaDescription,
                        LocalFilename = localFilePath,
                        Tags = new List<TagItem>()
                    };

                    // Store media in database
                    MediaRepository.AddMedia(item);

                    // Add to MediaListBox
                    MediaListBox.Items.Add(item);
                }
                else
                {
                    // Delete the local copy if the operation is canceled
                    try
                    {
                        File.Delete(localFilePath);
                        File.Delete(thumbnailPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to delete file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            
            
            
            
            

        }
    }
    
    private void MediaListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MediaListBox.SelectedItem is MediaItem mediaItem)
        {
            ShowMedia(mediaItem.LocalFilename);
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
    
    private void ShowMedia(string filePath)
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
    
    
    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        var searchText = SearchTextBox.Text;
        MediaListBox.Items.Clear();

        using (var connection = ManagementHelpers.GetAndOpenDatabaseConnection())
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT FilePath FROM Media WHERE Tags LIKE @tags";
            command.Parameters.AddWithValue("@tags", "%" + searchText + "%");

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    MediaListBox.Items.Add(reader["FilePath"].ToString());
                }
            }
        }
    }

}