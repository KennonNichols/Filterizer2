using System.Windows;

namespace Filterizer2
{
	public class FilterizerWindow: Window
	{
		protected override void OnContentRendered(EventArgs e)
		{
			base.OnContentRendered(e);
			ClampToScreen();
		}

		private void ClampToScreen()
		{
			var screen = Screen.FromPoint(
				new System.Drawing.Point((int)Left, (int)Top));

			var area = screen.WorkingArea;

			Left = Math.Clamp(Left, area.Left, area.Right - ActualWidth);
			Top = Math.Clamp(Top, area.Top, area.Bottom - ActualHeight);
		}
	}
}