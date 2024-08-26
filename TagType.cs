

using System.Windows.Media;

namespace Filterizer2
{
    public enum TagCategory
    {
        Character,
        Sexuality,
        Feature,
        Ip,
        Meta,
        Artstyle,
        Artist,
        Act
    }

    public static class TagCategoryHelpers
    {
        public static Color GetColor(this TagCategory category)
        {
            return category switch
            {
                TagCategory.Character => Color.FromRgb(94, 182, 9),
                TagCategory.Sexuality => Color.FromRgb(194, 131, 59),
                TagCategory.Feature => Color.FromRgb(61, 78, 162),
                TagCategory.Ip => Color.FromRgb(162, 61, 113),
                TagCategory.Meta => Color.FromRgb(231, 212, 128),
                TagCategory.Artstyle => Color.FromRgb(120, 239, 213),
                TagCategory.Artist => Color.FromRgb(183, 11, 64),
                TagCategory.Act => Color.FromRgb(0, 0, 0),
                _ => Color.FromRgb(255, 255, 255)
            };
        }
        
        public static string GetTitle(this TagCategory category)
        {
            return category switch
            {
                TagCategory.Character => "char",
                TagCategory.Sexuality => "sex",
                TagCategory.Feature => "feat",
                TagCategory.Ip => "ip",
                TagCategory.Meta => "meta",
                TagCategory.Artstyle => "art",
                TagCategory.Artist => "artist",
                TagCategory.Act => "act",
                _ => "null"
            };
        }
        
        public static string GetDescription(this TagCategory category)
        {
            return category switch
            {
                TagCategory.Character => "Character.",
                TagCategory.Sexuality => "Gender identity of focus, or sexual identity of sex act (cis male, lesbian, et.c.).",
                TagCategory.Feature => "Feature in image, like a choker, or a motorcycle.",
                TagCategory.Ip => "Franchise, universe, or IP.",
                TagCategory.Meta => "Concepts that aren't actually in the media.",
                TagCategory.Artstyle => "Style of art.",
                TagCategory.Artist => "Artist.",
                TagCategory.Act => "A sex act or position.",
                _ => "Null."
            };
        }
    }
}