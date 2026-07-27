using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vlc.DotNet.Core;
using Vlc.DotNet.Core.Interops.Signatures;
using XamlAnimatedGif;
using static Filterizer2.MediaExtension;

namespace Filterizer2.Controls
{
	public partial class MediaMultiplayerControl : UserControl
	{
		public MediaMultiplayerControl()
		{
			InitializeComponent();
			
			
			// Set the VLC library path
			var currentDirectory = new FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location).DirectoryName;
			var libDirectory = Path.Combine(currentDirectory, "libvlc");
			VlcPlayer.SourceProvider.CreatePlayer(new DirectoryInfo(libDirectory));
		}
		
		
		
		
		private string _mediaFilePath = "";
		private DispatcherTimer? _tryLoadTimer = null;
		
		private void InitializeTimer()
		{
			_tryLoadTimer = new DispatcherTimer();
			_tryLoadTimer.Interval = TimeSpan.FromSeconds(1);
			_tryLoadTimer.Tick += TryLoadTimerTick;
			_tryLoadTimer.Start();
		}

		private void TryLoadTimerTick(object sender, EventArgs e)
		{
			ShowMedia(_mediaFilePath);
		}
		
		
		
		
		
		
		
		
		private MediaExtension _currentShownExtension = Unsupported;
		
        public void ShowMedia(string filePath)
        {
	        
            bool mustEndInit = false;
            if (!ImageView.IsInitialized)
            {
                ImageView.BeginInit();
                mustEndInit = true;
            }
            
            if (!VlcPlayer.IsInitialized || CurrentPlayer == null)
            {
	            if (_tryLoadTimer == null)
	            {
		            _mediaFilePath = filePath;
		            InitializeTimer();
	            }
	            return;
            }

            _tryLoadTimer = null;
        
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
	                PlayPlayer(new Uri(filePath));
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
		
		private void PlayPlayer(Uri? mediaFileUri = null)
		{
			if (mediaFileUri != null)
			{
				CurrentPlayer?.Play(mediaFileUri);
			}
			else
			{
				CurrentPlayer?.Play();
			}

			TogglePlayButton.Content = "Pause";
		}
		
		
		private bool _wasPlayingBeforeSeek;
		public void PausePlayer()
		{
			if (CurrentPlayer?.IsPlaying() == true)
			{
				CurrentPlayer.Pause();
				TogglePlayButton.Content = "Play";
			}
		}
		private VlcMediaPlayer? CurrentPlayer => VlcPlayer?.SourceProvider?.MediaPlayer;
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
				PlayPlayer();
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
				PlayPlayer();
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
			PlayPlayer();
		}
	}
}