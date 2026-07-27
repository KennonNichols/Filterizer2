using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
		private readonly ScaleTransform _scale = new(1, 1);
		private readonly TranslateTransform _translate = new();
		
		public MediaMultiplayerControl()
		{
			InitializeComponent();
			
			var group = new TransformGroup();
			group.Children.Add(_scale);
			group.Children.Add(_translate);

			ImageView.RenderTransform = group;
			ImageView.RenderTransformOrigin = new Point(0, 0);
			
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
		
		
		
		private Point _lastPoint;
		private bool _dragging;

		private void Media_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_dragging = true;
			_lastPoint = e.GetPosition(this);
			ImageView.CaptureMouse();
		}

		private void Media_MouseMove(object sender, MouseEventArgs e)
		{
			if (!_dragging)
				return;

			Point current = e.GetPosition(this);

			_translate.X += current.X - _lastPoint.X;
			_translate.Y += current.Y - _lastPoint.Y;

			_lastPoint = current;
		}

		private void Media_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			_dragging = false;
			ImageView.ReleaseMouseCapture();
		}
		
		private void Media_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			const double zoomFactor = 1.2;
			const double minZoom = 1;
			const double maxZoom = 20.0;

			Point mouse = e.GetPosition(MediaContainer);

			double oldScale = _scale.ScaleX;

			double newScale = e.Delta > 0
				? oldScale * zoomFactor
				: oldScale / zoomFactor;

			newScale = Math.Clamp(newScale, minZoom, maxZoom);

			//Position of the mouse in image coordinates BEFORE zooming.
			double imageX = (mouse.X - _translate.X) / oldScale;
			double imageY = (mouse.Y - _translate.Y) / oldScale;

			_scale.ScaleX = newScale;
			_scale.ScaleY = newScale;

			_translate.X = mouse.X - imageX * newScale;
			_translate.Y = mouse.Y - imageY * newScale;

			e.Handled = true;
		}

		//TODO reset?
		// private void Media_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		// {
		// 	_scale.ScaleX = 1;
		// 	_scale.ScaleY = 1;
		//
		// 	_translate.X = 0;
		// 	_translate.Y = 0;
		// }
	}
}