using System.Windows;

namespace Filterizer2.Windows
{
	/// <summary>
	/// False is overwrite, true is append
	/// </summary>
	public partial class SelectImportOperationWindow : Window
	{
		public SelectImportOperationWindow()
		{
			InitializeComponent();
		}

		private void AppendButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
		}

		private void OverwriteButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
		}
	}
}