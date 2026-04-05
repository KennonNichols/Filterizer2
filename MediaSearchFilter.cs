using System.Diagnostics;
using System.Security.Policy;
using System.Text;
using System.Windows.Documents;
using System.Windows.Media;

namespace Filterizer2
{
    public class MediaSearchFilter
    {
        public readonly List<TagFilter> Filters;
        
        public MediaSearchFilter(List<TagFilter> filters)
        {
            Filters = filters;
        }
        
        public virtual bool TestMedia(IMediaDisplayItem mediaItem)
        {
            //Returns true if every filter contains at least one tag that the filter wants
            return Filters.All(filter => filter.Test(mediaItem.TagsForFiltering));
        }
    }

    public class SearchFilterOpen(List<TagFilter> filters) : MediaSearchFilter(filters)
    {
        public override bool TestMedia(IMediaDisplayItem mediaItem) => true;
    }



    public class TagFilter(HashSet<TagItem> tags)
    {
        public readonly HashSet<TagItem> Tags = tags;

        public bool Test(IEnumerable<TagItem> testedItemSignature)
        {
	        return Inverted ?
		        //If any of the tags in the filter are not present in the test item, return true
		        Tags.Any(tagItem => testedItemSignature.All(tag => tag.Id != tagItem.Id)) :
		        //If any of the tags in the filter are present in the test item, return true
		        testedItemSignature.Any(tag => Tags.Any(item => item.Id == tag.Id));
        }
        
		/// <summary>
		/// Whether this has been inverted. If it's true, this is a blacklist filter
		/// </summary>
		public bool Inverted = false;
		
        public void AddTag(TagItem tag)
        {
	        Tags.Add(tag);
        }

        public bool IsEmpty => Tags.Count == 0;
        
        public string Summary 
        {
            get
            {
                if (Tags.Count == 0)
                {
                    return "Empty filter. Click this filter and then click the \"->\" button to add a tag to it.";
                }

                StringBuilder reportBuilder = new StringBuilder();
                bool first = true;
                foreach (TagItem tagItem in Tags)
                {
	                if (first)
	                {
		                reportBuilder.Append(tagItem.Name);
	                }
	                else
	                {
		                reportBuilder.AppendLine().Append($"   || {tagItem.Name}");
	                }
	                first = false;
                }

                return reportBuilder.ToString();
            }    
        }

        public Brush FGColor => Inverted ? Brushes.White : Brushes.Black;
        public Brush BGColor => Inverted ? Brushes.Black : null;
    };
}