using System.Windows;
using System.Windows.Interop;
using Button = System.Windows.Controls.Button;

namespace Filterizer2.Windows
{
	public partial class MediaMessageBox : Window
	{
		public MediaMessageBox(
			string message,
			string? filePath,
			string caption,
			MediaMessageBoxButton buttons)
		{
			InitializeComponent();

			Title = caption;

			MessageText.Text = message;

			if (filePath != null)
			{
				MediaPlayer.ShowMedia(filePath);
			}

			CreateButtons(buttons);
		}

		protected override void OnClosed(EventArgs e)
		{
			MediaPlayer.PausePlayer();
			base.OnClosed(e);
		}
		
		public static MediaMessageBoxResult Show(
			string text,
			string? filePath,
			string caption = "",
			MediaMessageBoxButton buttons = MediaMessageBoxButton.OK,
			Window? owner = null)
		{
			var window = new MediaMessageBox(
				text,
				filePath,
				caption,
				buttons);

			if (owner != null)
				window.Owner = owner;

			window.ShowDialog();

			return window.Result;
		}
		
		private void CreateButtons(MediaMessageBoxButton buttons)
		{
			switch (buttons)
			{
				case MediaMessageBoxButton.OK:
					AddButton("OK", MediaMessageBoxResult.OK, true);
					break;

				case MediaMessageBoxButton.OKCancel:
					AddButton("OK", MediaMessageBoxResult.OK, true);
					AddButton("Cancel", MediaMessageBoxResult.Cancel);
					break;

				case MediaMessageBoxButton.YesNo:
					AddButton("Yes", MediaMessageBoxResult.Yes, true);
					AddButton("No", MediaMessageBoxResult.No);
					break;

				case MediaMessageBoxButton.YesNoCancel:
					AddButton("Yes", MediaMessageBoxResult.Yes, true);
					AddButton("No", MediaMessageBoxResult.No);
					AddButton("Cancel", MediaMessageBoxResult.Cancel);
					break;
				case MediaMessageBoxButton.SingleCompilation:
					AddButton("Single", MediaMessageBoxResult.Yes);
					AddButton("Compilation", MediaMessageBoxResult.No, true);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(buttons), buttons, null);
			}
		}
		
		private void AddButton(
			string text,
			MediaMessageBoxResult result,
			bool isDefault = false)
		{
			var button = new Button
			{
				Content = text,
				Width = 80,
				Margin = new Thickness(5),
				IsDefault = isDefault,
				IsCancel = result == MediaMessageBoxResult.Cancel
			};

			button.Click += (_, _) =>
			{
				Result = result;
				DialogResult = true;
			};

			ButtonPanel.Children.Add(button);
		}

		public MediaMessageBoxResult Result { get; set; }

		public enum MediaMessageBoxResult
		{
			None,
			OK,
			Cancel,
			Yes,
			No
		}
		
		public enum MediaMessageBoxButton
		{
			/// <summary>The message box displays an OK button.</summary>
			OK = 0,
			/// <summary>The message box displays OK and Cancel buttons.</summary>
			OKCancel = 1,
			/// <summary>The message box displays Yes, No, and Cancel buttons.</summary>
			YesNoCancel = 3,
			/// <summary>The message box displays Yes and No buttons.</summary>
			YesNo = 4,
			/// <summary>The message box displays Single (yes) and Compilation buttons.</summary>
			SingleCompilation = 5,
		}
	}
}