

using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Xml;

namespace Filterizer2
{
	public static class Tags
	{
		private static readonly Dictionary<string, TagCategory> LoadedTags = new Dictionary<string, TagCategory>();
		private static bool _checkedTagsFile;
		
		public static TagCategory GetCategoryOfName(string name)
		{
			if (!_checkedTagsFile)
			{
				LoadTagsFromFile();
				_checkedTagsFile = true;
			}


			if (LoadedTags.TryGetValue(name, out TagCategory category)) return category;
			category = new TagCategory(name,
				"Auto-generated category. This likely occured because the TagCategoriesEditable.xml file has changed.",
				Colors.Crimson);
				
			LoadedTags.Add(name, category);

			return category;
		}

		private static void LoadTagsFromFile()
		{
			foreach (TagCategory tagCategory in GetFromFile())
			{
				LoadedTags.Add(tagCategory.Title, tagCategory);
			}
		}

		private static IEnumerable<TagCategory> GetFromFile()
		{
			XmlDocument doc = new XmlDocument();
			doc.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TagCategoriesEditable.xml"));
			
			XmlNodeList nodes = doc.SelectNodes("/TagCategories/li");
			foreach (XmlNode node in nodes)
			{
				string title = node.SelectSingleNode("Name")?.InnerText;
				string description = node.SelectSingleNode("Description")?.InnerText;
				string colorString = node.SelectSingleNode("Color")?.InnerText;

				Color color = ParseColor(colorString);

				yield return new TagCategory(title, description, color);
			}
		}
		
		private static Color ParseColor(string colorString)
		{
			if (string.IsNullOrWhiteSpace(colorString))
				return Colors.Black;

			var parts = colorString.Split(',');
			if (parts.Length != 3)
				throw new FormatException("Color format is invalid. Expected 'R, G, B'.");

			byte r = byte.Parse(parts[0].Trim());
			byte g = byte.Parse(parts[1].Trim());
			byte b = byte.Parse(parts[2].Trim());

			return Color.FromRgb(r, g, b);
		}

		public static IEnumerable GetAllValues()
		{
			if (!_checkedTagsFile)
			{
				LoadTagsFromFile();
				_checkedTagsFile = true;
			}
			
			foreach (TagCategory loadedTagsValue in LoadedTags.Values)
			{
				Debug.WriteLine(loadedTagsValue.Title);
			}

			return LoadedTags.Values;
		}
	}

	public class TagCategory(string title, string description, Color color)
	{
		public override string ToString() => Title;

		public string Title => title;

		public string Description => description;

		public Color Color => color;
	}
}