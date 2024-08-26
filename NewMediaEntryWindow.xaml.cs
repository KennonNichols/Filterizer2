using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XamlAnimatedGif;

namespace Filterizer2
{
    public partial class NewMediaEntryWindow : Window
    {
        public string MediaTitle => TitleTextBox.Text;
        public string MediaDescription => DescriptionTextBox.Text;

        private readonly string _mediaFilePath;

        public NewMediaEntryWindow(string mediaFilePath)
        {
            InitializeComponent();
            _mediaFilePath = mediaFilePath;
        
            // Set the VLC library path
            var currentDirectory = new FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location).DirectoryName;
            var libDirectory = Path.Combine(currentDirectory, "libvlc");
            VlcPlayer.SourceProvider.CreatePlayer(new DirectoryInfo(libDirectory));
            
            
            LoadMediaPreview();
        }

        private void LoadMediaPreview()
        {
            ShowMedia();
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
    }

}