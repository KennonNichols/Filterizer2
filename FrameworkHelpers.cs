using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Filterizer2
{
	public static class FrameworkHelpers
	{
		public static string GetFrameworkElementName(this FrameworkElement element)
		{
			return element switch
			{
				Control control => control.Name,
				Decorator decorator => decorator.Name,
				Panel panel => panel.Name,
				_ => ""
			};
		}
		
		public static void SetFrameworkElementBrushes(this FrameworkElement element, (Brush?, Brush?) brushes)
		{
			Brush? background = brushes.Item1;
			Brush? foreground = brushes.Item2;
			switch (element)
			{
				// case ComboBox comboBox:
				// 	foreach (DictionaryEntry comboBoxResource in comboBox.Resources)
				// 	{
				// 		//TODO this seems dangerous, consider another option
				// 		if (comboBoxResource.Key.ToString() == "HighlightBrush")
				// 		{
				// 			comboBox.Resources[comboBoxResource.Key] = background;
				// 		}
				// 		if (comboBoxResource.Key.ToString() == "WindowBrush")
				// 		{
				// 			comboBox.Resources[comboBoxResource.Key] = background;
				// 		}
				// 	}
				//
				// 	comboBox.Background = background;
				// 	comboBox.Foreground = foreground
				// 	break;
				case Control control:
					control.Background = background;
					control.Foreground = foreground;
					break;
				case Panel panel:
					panel.Background = background;
					break;
				case Border border:
					border.Background = background;
					border.BorderBrush = foreground;
					break;
			}
		}
	}
}