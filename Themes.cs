using System.Drawing;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace Filterizer2
{
	public class Theme
	{
		public Theme(string label)
		{
			Label = label;
		}

		public static IEnumerable<Theme> Themes
		{
			get
			{
				

				yield return new Theme("Dark");
				yield return new Theme("Light");
				yield return new Theme("Vantablack");
			}
		}

		public string Label { get; }
	}
}