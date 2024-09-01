using System.Diagnostics;
using System.Text;

namespace Filterizer2
{
    public class MediaSearchFilter
    {
        public readonly List<TagFilter> Filters;
        
        public MediaSearchFilter(List<TagFilter> filters)
        {
            Filters = filters;
        }
        
        public virtual bool TestMedia(MediaItem mediaItem)
        {
            //Returns true if every filter contains at least one tag that the filter wants
            return Filters.All(filter => mediaItem.Tags.Any(tag => filter.Tags.Any(item => item.Id == tag.Id)));
        }
    }

    public class SearchFilterOpen(List<TagFilter> filters) : MediaSearchFilter(filters)
    {
        public override bool TestMedia(MediaItem mediaItem) => true;
    }



    public class TagFilter(List<TagItem> tags)
    {
        public List<TagItem> Tags = tags;
        
        public string Summary 
        {
            get
            {
                if (Tags.Count == 0)
                {
                    return "Empty filter. Click this filter and then click the \"->\" button to add a tag to it.";
                }

                StringBuilder reportBuilder = new StringBuilder(Tags[0].Name);
                for (int i = 1; i < Tags.Count; i++)
                {
                    reportBuilder.AppendLine().Append($"   || {Tags[i].Name}");
                }

                return reportBuilder.ToString();
            }    
        }
    };
}