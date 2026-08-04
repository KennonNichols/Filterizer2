using System.Drawing;
using System.Text;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace Filterizer2
{
    public class TagItem
    {
	    public TagItem()
	    {
		    
	    }
        public int Id { get; set; }
        public string Name { get; set; }
        public TagCategory Category { get; set; }
        public TagSubCategory SubCategory { get; set; }
        public string Description { get; set; }
        public List<string> Aliases { get; set; } = new List<string>();
        /// <summary>
        /// Temporary list of IDs. Before all tags are loaded, we populate this field, then find parent tags later so all tags are loaded successfully
        /// </summary>
        public List<int> ImmediateParentIDs { get; set; } = new List<int>();
        public List<int> ExcludedByIDs { get; set; } = new List<int>();
        
        public bool IsChildable => Category?.IsChildable ?? true;

        public bool MayBeTrueChild(Dictionary<int, TagItem> allTags)
        {
	        //If we belong to a category that does not child, we are not a child
	        if (!IsChildable) return false;
	        //If we have no parents, we are not a child
	        if (ImmediateParentIDs.Count == 0) return false;
	        //If our first parent is in a separate category, we are not a true child
	        if (allTags[ImmediateParentIDs[0]].Category != Category) return false;
	        //If none of the above are satisfied; we may be a true child
	        return true;
        }

        public bool IsTrueChildOf(TagItem tagItem)
        {
	        if (!IsChildable) return false;
	        if (ImmediateParentIDs.Count == 0) return false;
	        if (tagItem.Category != Category) return false;
	        if (tagItem.Id != ImmediateParentIDs[0]) return false;
	        return true;
        }
        
        public IEnumerable<TagItem> ImmediateParentTags
        {
	        get
	        {
		        List<int>? vanishedInts = null;
		        foreach (var parentId in ImmediateParentIDs)
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
				        ImmediateParentIDs.Remove(vanishedInt);
			        }

			        TagRepository.UpdateTag(this, TagUpdateMode.UpdateParents);
		        }
	        }
        }
        
        public IEnumerable<TagItem> ExcludedByTags
        {
	        get
	        {
		        List<int>? vanishedInts = null;
		        foreach (var parentId in ExcludedByIDs)
		        {
			        if (TagRepository.TryGetTagById(parentId, out TagItem foundTag))
			        {
				        yield return foundTag;
			        }
			        else
			        {
				        //An orphaned excluder was not found, we need to update the media.
				        vanishedInts ??= new List<int>();
				        vanishedInts.Add(parentId);
			        }
		        }

		        if (vanishedInts != null)
		        {
			        foreach (int vanishedInt in vanishedInts)
			        {
				        ExcludedByIDs.Remove(vanishedInt);
			        }

			        TagRepository.UpdateTag(this, TagUpdateMode.UpdateExclusions);
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
	        foreach (TagItem parentTag in ImmediateParentTags)
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
	        foreach (TagItem parentTag in ImmediateParentTags)
	        {
		        parentTag.GetTagHierarchyTags(ref tagSet);
	        }
        }
        
        

        // private HashSet<int>? _allParentIds;
        //
        // public IEnumerable<int> GetAllParentIdsRecursive()
        // {
	       //  return _allParentIds ??=
		      //   ComputeAllParentIdsRecursive().ToHashSet();
        // }
        //TODO consider optimizing this in a way that it doesn't cache dead parental relationships
        public IEnumerable<int> GetAllParentIdsRecursive()
        {
	        // HashSet<TagItem> tagItems = new ();
	        // GetTagHierarchyTags(ref tagItems);
	        //
	        // HashSet<int> tagIds = new ();
	        // foreach (TagItem parentTag in tagItems)
	        // {
		       //  tagIds.Add(parentTag.Id);
	        // }
	        //
	        // return tagIds;
	        return TagRepository.GetAllParentIDsRecursively(Id);
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
	        
	        if (SubCategory.Title != "" && !SubCategory.IsFallback)
	        {
		        builder.Append(" $" + SubCategory.TagString);
	        }

	        bool isFirstParent = true;
	        if (ImmediateParentIDs.Count > 0)
	        {
		        foreach (TagItem tagItem in ImmediateParentTags)
		        {
			        if (isFirstParent)
			        {
				        //First parent is only shown if it's of a different category than the tag, or the tag is not childable
				        if (!IsChildable || tagItem.Category != Category)
				        {
					        builder.Append(" >" + tagItem.Name);
				        }
				        isFirstParent = false;
				        continue;
			        }
			        builder.Append(" >" + tagItem.Name);
		        }
	        }
	        
	        if (ExcludedByIDs.Count > 0)
	        {
		        foreach (TagItem tagItem in ExcludedByTags)
		        {
			        builder.Append(" !" + tagItem.Name);
		        }
	        }

	        return builder.ToString();
        }
        
        public override string ToString()
        {
	        return Name;
        }

        public override bool Equals(object? obj)
        {
	        if (obj is not TagItem comparedTag) return false;
	        return comparedTag.Id == Id;
        }
    }
}