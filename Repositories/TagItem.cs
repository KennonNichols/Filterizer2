using System.Drawing;
using System.Text;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace Filterizer2
{
    public class TagItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public TagCategory Category { get; set; }
        public string Description { get; set; }
        public List<string> Aliases { get; set; } = new List<string>();
        /// <summary>
        /// Temporary list of IDs. Before all tags are loaded, we 
        /// </summary>
        public List<int> ParentIDs { get; set; } = new List<int>();

        public IEnumerable<TagItem> ParentTags
        {
	        get
	        {
		        List<int>? vanishedInts = null;
		        foreach (var parentId in ParentIDs)
		        {
			        if (TagRepository.TryGetTagById(parentId, out TagItem foundTag))
			        {
				        yield return foundTag;
			        }
			        else
			        {
				        //An orphaned parent was not found, we need to update the media.
				        vanishedInts ??= new List<int>();
				        vanishedInts.Add(parentId);
			        }
		        }

		        if (vanishedInts != null)
		        {
			        foreach (int vanishedInt in vanishedInts)
			        {
				        ParentIDs.Remove(vanishedInt);
			        }

			        TagRepository.UpdateTag(this);
		        }
	        }
        }

        public Brush DisplayColorBrush => Category.Brush;
        public HashSet<string> GetAllNamesAliasesAndParentNames()
        {
	        HashSet<string> allNamesAliasesAndParentNames = new HashSet<string>();
	        allNamesAliasesAndParentNames.Add(Name);
	        foreach (string alias in Aliases)
	        {
		        allNamesAliasesAndParentNames.Add(alias);
	        }
	        foreach (TagItem parentTag in ParentTags)
	        {
		        foreach (string nameOrAlias in parentTag.GetAllNamesAliasesAndParentNames())
		        {
			        allNamesAliasesAndParentNames.Add(nameOrAlias);
		        }
	        }
	        return allNamesAliasesAndParentNames;
        }

        public void GetTagHierarchyTags(ref HashSet<TagItem> tagSet)
        {
	        tagSet.Add(this);
	        foreach (TagItem parentTag in ParentTags)
	        {
		        parentTag.GetTagHierarchyTags(ref tagSet);
	        }
        }

        public HashSet<int> GetTagHierarchyIds()
        {
	        HashSet<TagItem> tagItems = new ();
	        GetTagHierarchyTags(ref tagItems);

	        HashSet<int> tagIds = new ();
	        foreach (TagItem parentTag in tagItems)
	        {
		        tagIds.Add(parentTag.Id);
	        }

	        return tagIds;
        }
        
        public string NamesAndAliasesAsString =>
	        Aliases.Count != 0
		        ? Name + " (" + string.Join(", ", Aliases) + ")"
		        : Name;

        public string GetParserWritable()
        {
	        StringBuilder builder = new StringBuilder();
	        builder.Append(Name);
	        foreach (string alias in Aliases)
	        {
		        builder.Append(" @" + alias);
	        }
	        if (Description != "")
	        {
		        builder.Append(" \"" + Description + "\"");
	        }

	        if (ParentIDs.Count > 1)
	        {
		        foreach (TagItem tagItem in ParentTags.Skip(1))
		        {
			        builder.Append(" >" + tagItem.Name);
		        }
	        }

	        return builder.ToString();
        }
        
        public override string ToString()
        {
	        return Name;
        }
    }
}