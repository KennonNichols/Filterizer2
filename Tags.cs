

using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Xml;
using ImageMagick;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace Filterizer2
{
	public static class Tags
	{
		private static readonly Dictionary<string, TagCategory> LoadedTagCategories = new Dictionary<string, TagCategory>();
		private static bool _checkedTagsFile;
		public static readonly List<TagCategory> TagCategoriesInTaggingOrder = new List<TagCategory>();

		public static List<TagCategory> NonChildableCats => _nonChildableCats ??= GetAllValues().Where(cat => !cat.IsChildable).ToList();
		private static List<TagCategory>? _nonChildableCats;

		
		public static TagCategory GetCategoryOfName(string name, bool canGenerateFallback = false)
		{
			if (!_checkedTagsFile)
			{
				LoadTagsCategoriesFromFile();
				_checkedTagsFile = true;
			}

			if (LoadedTagCategories.TryGetValue(name, out TagCategory category)) return category;
			if (!canGenerateFallback) return null;
			
			category = new TagCategory(name,
				"Auto-generated category. This likely occured because the TagCategoriesEditable.xml file has changed.",
				Colors.Crimson, true, 9999, new List<TagSubCategory>());
				
			LoadedTagCategories.Add(name, category);
			TagCategoriesInTaggingOrder.Add(category);

			return category;
		}

		private static void LoadTagsCategoriesFromFile()
		{
			foreach (TagCategory tagCategory in GetTagCategoryFromFile())
			{
				LoadedTagCategories.Add(tagCategory.Title, tagCategory);
				TagCategoriesInTaggingOrder.Add(tagCategory);
			}
			TagCategoriesInTaggingOrder.Sort((cat1, cat2) => cat1.Order.CompareTo(cat2.Order));
		}

		private static IEnumerable<TagCategory> GetTagCategoryFromFile()
		{
			XmlDocument doc = new XmlDocument();
			doc.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TagCategoriesEditable.xml"));
			
			XmlNodeList nodes = doc.SelectNodes("/TagCategories/li");
			foreach (XmlNode node in nodes)
			{
				string? title = node.SelectSingleNode("Name")?.InnerText;
				string? description = node.SelectSingleNode("Description")?.InnerText;
				string? colorString = node.SelectSingleNode("Color")?.InnerText;
				string? isChildable = node.SelectSingleNode("IsChildable")?.InnerText;
				string? order = node.SelectSingleNode("Order")?.InnerText;
				
				XmlNodeList? subCategoryNodes = node.SelectNodes("./SubCategories/li");

				List<TagSubCategory> subCategories = new List<TagSubCategory>();
				
				if (subCategoryNodes != null)
				{
					foreach (XmlNode subCategoryNode in subCategoryNodes)
					{
						if (subCategoryNode?.InnerText != null)
						{
							subCategories.Add(LoadNodeAsSubcategory(subCategoryNode));
						}
					}
				}
				
				Color color = ParseColor(colorString);
				bool boolIsChildable = bool.Parse(isChildable ?? "true");
				int intOrder = Int32.Parse(order ?? "9999");
					
				yield return new TagCategory(title, description, color, boolIsChildable, intOrder, subCategories);
			}
		}

		private static TagSubCategory LoadNodeAsSubcategory(XmlNode node)
		{
			string? title = node.SelectSingleNode("Tag")?.InnerText;
			string? description = node.SelectSingleNode("Description")?.InnerText;
			return new TagSubCategory(title ?? "ERROR_No_title", description ?? "");
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

		public static IEnumerable<TagCategory> GetAllValues()
		{
			if (!_checkedTagsFile)
			{
				LoadTagsCategoriesFromFile();
				_checkedTagsFile = true;
			}
			
			// foreach (TagCategory loadedTagsValue in LoadedTags.Values)
			// {
			// 	Debug.WriteLine(loadedTagsValue.Title);
			// }

			return LoadedTagCategories.Values;
		}
	}

	public class TagCategory
	{
		private readonly string _title;
		private readonly string _description;
		private readonly Color _color;
		private readonly bool _isChildable;
		private readonly int _order;
		/// <summary>
		/// The categories that a tagger should look through in order.
		/// </summary>
		public List<TagSubCategory> SubcategoriesInOrder;
		private readonly TagSubCategory _defaultSubcategory;
		
		public TagCategory(string title, string description, Color color, bool isChildable, int order, List<TagSubCategory> subcategories)
		{
			_title = title;
			_description = description;
			_color = color;
			_isChildable = isChildable;
			_order = order;
			SubcategoriesInOrder = subcategories;
			foreach (TagSubCategory tagSubCategory in SubcategoriesInOrder)
			{
				tagSubCategory.Parent = this;
			}

			_defaultSubcategory = new TagSubCategory("Miscellaneous",
				"All tags that don't fit into the flow of tagging otherwise.", true);
			_defaultSubcategory.Parent = this;
			SubcategoriesInOrder.Add(_defaultSubcategory);
		}
		
		public override string ToString() => Title;

		public string Title => _title;

		public string Description => _description;

		public Color Color => _color;

		public bool IsChildable => _isChildable;

		public TagSubCategory DefaultSubCategory => _defaultSubcategory;
		
		public int Order => _order;
		
		public Brush Brush => _brush ??= new SolidColorBrush(Color);
		private Brush? _brush;

		public TagSubCategory GetSubCategoryByName(string name)
		{
			if (SubcategoriesInOrder != null)
			{
				foreach (TagSubCategory tagSubCategory in SubcategoriesInOrder)
				{
					if (tagSubCategory.TagString == name)
					{
						return tagSubCategory;
					}
				}
			}
			return _defaultSubcategory;
		}
	}

	public class TagSubCategory
	{
		public string TagString;
		public string Title;
		public string Description;
		public TagCategory Parent;
		public bool IsFallback;

		public override string ToString() => Title;

		//We don't store fallback names
		public string TagStringForDatabase => IsFallback ? "" : TagString;

		public TagSubCategory(string tagString, string description, bool isFallback = false)
		{
			TagString = tagString;
			Title = tagString.Replace('_', ' ');
			Description = description;
			IsFallback = isFallback;
		}

		public override bool Equals(object? obj)
		{
			if (obj is not TagSubCategory tagSub) return false;

			if (TagStringForDatabase == tagSub.TagStringForDatabase && Parent.Title == tagSub.Parent.Title)
			{
				return true;
			}
			
			return base.Equals(obj);
		}
	}
}