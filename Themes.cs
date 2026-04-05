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
		public Theme(string label, (Brush?, Brush?) defaultColor)
		{
			Label = label;
			DefaultColor = defaultColor;
		}

		public static IEnumerable<Theme> Themes
		{
			get
			{
				Theme dark = new Theme("Dark", (Brushes.DarkSlateGray, Brushes.Black));
					dark.TypeOverrides.Add(typeof(Button), (Brushes.DimGray, Brushes.Black));
				yield return dark;
				yield return new Theme("Light", (null, Brushes.Black));
				Theme vantablack = new Theme("Vantablack", (Brushes.Black, Brushes.DarkGray));
					vantablack.TypeOverrides.Add(typeof(Button), (new SolidColorBrush(Color.FromRgb(32, 0, 22)), Brushes.DarkGray));
				yield return vantablack;
			}
		}

		public string Label { get; }
		public (Brush?, Brush?) DefaultColor;
		public Dictionary<string, (Brush?, Brush?)> ControlNameOverrides = new Dictionary<string, (Brush?, Brush?)>();
		public Dictionary<Type, (Brush?, Brush?)> TypeOverrides = new Dictionary<Type, (Brush?, Brush?)>();

		public (Brush?, Brush?) GetBrushesForElement(FrameworkElement frameworkElement)
		{
			(Brush?, Brush?) brush;
			string name = frameworkElement.GetFrameworkElementName();
			if (ControlNameOverrides.TryGetValue(name, out brush))
			{
				return brush;
			}

			if (TypeOverrides.TryGetValue(frameworkElement.GetType(), out brush))
			{
				return brush;
			}

			return TypeOverrides.TryGetValue(frameworkElement.GetType(), out brush) ? brush : DefaultColor;
		}
	}
}