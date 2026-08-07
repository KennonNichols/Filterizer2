using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace Filterizer2.Windows
{
	public partial class TagDictionaryIO
	{
		public TagDictionaryIO()
		{
			InitializeComponent();
		}

		private void ExportButton_OnClick(object sender, RoutedEventArgs e)
		{
			Dictionary<int, TagItem> unorderedTags = TagRepository.GetTags().ToDictionary(t => t.Id);
			List<TagItem> lastPassAddedTags = new List<TagItem>();
			HashSet<TagItem> addedTags = new HashSet<TagItem>();
			//Load it up first with the categories
			List<TransientDisplayItem> transientDisplayTags = (from object allValue in Tags.GetAllTagCategories() select new TransientDisplayCategoryItem((TagCategory)allValue)).Cast<TransientDisplayItem>().ToList();


			//Add all tags that have no true parent
			foreach (var (_, unorderedTag) in unorderedTags.Where(var =>
			         {
				         var (_, tagItem) = var;
				         return !tagItem.MayBeTrueChild(unorderedTags);
			         }))
			{
				//insert somewhere
				int categoryLocation = transientDisplayTags.FindIndex(item =>
					item is TransientDisplayCategoryItem catItem && catItem.Category == unorderedTag.Category);
				
				lastPassAddedTags.Add(unorderedTag);
				addedTags.Add(unorderedTag);
				transientDisplayTags.Insert(categoryLocation + 1, new TransientDisplayTagItem(unorderedTag, 1));
			}

			foreach (TagItem lastPassAddedTag in lastPassAddedTags)
			{
				unorderedTags.Remove(lastPassAddedTag.Id);
			}
			
			lastPassAddedTags.Clear();

			int indent = 1;
			while (unorderedTags.Count != 0)
			{
				indent++;
				//Get all tags where their first parent is loaded right now

				foreach (var  (_, unorderedTag) in unorderedTags)
				{
					TagItem primaryParent = unorderedTag.ImmediateParentTags.First();
					if (addedTags.All(item => item.Id != primaryParent.Id)) continue;
					int parentLocation = transientDisplayTags.FindIndex(item =>
						item is TransientDisplayTagItem tagItem && tagItem.Tag.Id == primaryParent.Id);
					
					lastPassAddedTags.Add(unorderedTag);
					transientDisplayTags.Insert(parentLocation + 1, new TransientDisplayTagItem(unorderedTag, indent));
				}
				
				foreach (TagItem lastPassAddedTag in lastPassAddedTags)
				{
					addedTags.Add(lastPassAddedTag);
					unorderedTags.Remove(lastPassAddedTag.Id);
				}
				
				lastPassAddedTags.Clear();
			}

			StringBuilder builder = new StringBuilder();
			foreach (TransientDisplayItem transientDisplayItem in transientDisplayTags)
			{
				builder.AppendLine(transientDisplayItem.WriteForParser);
			}

			Readout.Text = builder.ToString();
		}
		
		private void ImportButton_OnClick(object sender, RoutedEventArgs e)
		{
			string parsedInput = Readout.Text;
			// Readout.Text = "";
			// StringBuilder builder = new StringBuilder();
			//
			// Readout.Text = builder.ToString();
			
			if (!TryParse(parsedInput, out List<TransientTagItemForIO> parsedTags)) return;

			bool? isMaybeAppendOperation = new SelectImportOperationWindow().ShowDialog();

			if (isMaybeAppendOperation is { } isAppendOperation) { }
			else
			{
				MessageBox.Show($"You must select append and update or overwrite", "Pick one",
					MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			
			if (parsedTags.Count == 0 && !isAppendOperation)
			{
				if (MessageBox.Show(
					    "Your input parsed 0 tags. Are you sure you want to completely empty the database?",
					    "Empty confirmation",
					    MessageBoxButton.YesNo,
					    MessageBoxImage.Question
				    ) == MessageBoxResult.No)
				{
					return;
				}
			}


			if (!isAppendOperation)
			{
				if (MessageBox.Show(
					    "This is a destructive action. Are you sure that you want to replace the entire database with these tags?",
					    "Confirm import",
					    MessageBoxButton.YesNo,
					    MessageBoxImage.Question
				    ) == MessageBoxResult.No)
				{
					return;
				}
			}
			
			AddTransientTagListToDictionary(parsedTags, isAppendOperation);
		}

		private static void AddTransientTagListToDictionary(List<TransientTagItemForIO> transientTags, bool isAppendOperation)
		{
			Dictionary<string, TagItem> newlyAddedTagItemsByName = new Dictionary<string, TagItem>();
			Dictionary<TagItem, TransientTagItemForIO> relatedTransientTags =
				new Dictionary<TagItem, TransientTagItemForIO>();

			Dictionary<string, TagItem> existingTagsByName = TagRepository.GetTags().ToDictionary(allExistingTag => allExistingTag.Name);

			int deletedRows = 0;
			int appendedRows = 0;
			int updatedRows = 0;

			// HashSet<string> allNamesExistingOrProposed = new HashSet<string>();
			// if (isAppendOperation)
			// {
			// 	foreach (var (existingTagName, _) in existingTagsByName)
			// 	{
			// 		allNamesExistingOrProposed.Add(existingTagName);
			// 	}
			// }
			
			
			//Make a tag out of the transient tags
			foreach (TransientTagItemForIO transientTag in transientTags)
			{
				TagItem newTag = new TagItem
				{
					Id = -1,
					Name = transientTag.Name,
					Description = transientTag.Description,
					Aliases = transientTag.Aliases
				};
				
				TagCategory category = Tags.GetCategoryOfName(transientTag.CategoryName);
				// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
				if (category == null)
				{
					MessageBox.Show($"Invalid category name '{transientTag.CategoryName}' on tag at line {transientTag.LineNumber}.", "Category error",
						MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}
				newTag.Category = category;

				TagSubCategory subCategory = category.GetSubCategoryByName(transientTag.SubCategoryName);
				newTag.SubCategory = subCategory;
				
				//Record the tag for building relations
				newlyAddedTagItemsByName.Add(transientTag.Name, newTag);
				//Records the transient tag used to build it, so we can find the parents in the next loop.
				relatedTransientTags.Add(newTag, transientTag);
			}
			
			//Check to make sure every desired parent exists. Iterate over ever transient tag (to check their parents)
			foreach (var (_, transientTagItemForIO) in relatedTransientTags)
			{
				foreach (string parentName in transientTagItemForIO.ParentNames)
				{
					//If the transient tags defines that parent, set it as a transient parent
					if (newlyAddedTagItemsByName.TryGetValue(parentName, out TagItem? tag))
					{
						transientTagItemForIO.TransientParents.Add(tag);
					}
					//Otherwise, if we are appending, check for existing parents with that name
					else if (existingTagsByName.TryGetValue(parentName, out TagItem? existingTagAsParent))
					{
						transientTagItemForIO.TransientParents.Add(existingTagAsParent);
					}
					else
					{
						//We have found a missing parent
						MessageBox.Show($"Did not find parent '{parentName}' on tag at line {transientTagItemForIO.LineNumber}.", "Parenting error",
							MessageBoxButton.OK, MessageBoxImage.Error);
						return;
					}
				}
			}
			
			//Check to make sure every desired excluder exists. Iterate over ever transient tag (to check their excluders)
			foreach (var (_, transientTagItemForIO) in relatedTransientTags)
			{
				foreach (string excluderName in transientTagItemForIO.ExcluderNames)
				{
					//If the transient tags defines that excluder, set it as a transient excluder
					if (newlyAddedTagItemsByName.TryGetValue(excluderName, out TagItem? tag))
					{
						transientTagItemForIO.TransientExcluders.Add(tag);
					}
					//Otherwise, if we are appending, check for existing excluders with that name
					else if (existingTagsByName.TryGetValue(excluderName, out TagItem? existingTagAsExcluder))
					{
						transientTagItemForIO.TransientExcluders.Add(existingTagAsExcluder);
					}
					else
					{
						//We have found a missing excluder
						MessageBox.Show($"Did not find excluder '{excluderName}' on tag at line {transientTagItemForIO.LineNumber}.", "Excluding error",
							MessageBoxButton.OK, MessageBoxImage.Error);
						return;
					}
				}
			}
			
			//Delete ALL tags that aren't defined in this list if we are overwriting instead of appending
			if (!isAppendOperation)
			{
				foreach (TagItem tagItem in existingTagsByName.Values)
				{
					//If the tag is in existing tags, but doesn't match any of the newly defined names, delete it.
					if (!newlyAddedTagItemsByName.ContainsKey(tagItem.Name))
					{
						deletedRows++;
						TagRepository.DeleteTag(tagItem);
					}
				}
			}
			
			//Set IDs from existing IDs
			foreach (var (_, newlyAddedTag) in newlyAddedTagItemsByName)
			{
				if (existingTagsByName.TryGetValue(newlyAddedTag.Name, out TagItem oldTagWithMatchingName))
				{
					newlyAddedTag.Id = oldTagWithMatchingName.Id;
				}
			}
			
			//Add or update the (parentless) tags
			foreach (var (_, tagItem) in newlyAddedTagItemsByName)
			{
				//At this point, all tags that are updating have their ID set, while the remainder ar ID -1
				if (tagItem.Id == -1)
				{
					appendedRows++;
					TagRepository.AddTag(tagItem);
				}
				else
				{
					//This should destroy parental relationships, meaning the registering parents later is harmless
					updatedRows++;
					TagRepository.UpdateTag(tagItem);
				}
			}
			//All tags in newlyAddedTagItemsByName have no parental relationships, because of what happened in that last loop.
			
			using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
			using var transaction = connection.BeginTransaction();
			
			//Add parents and excluders
			foreach (var (_, tagItem) in newlyAddedTagItemsByName)
			{
				foreach (TagItem transientParent in relatedTransientTags[tagItem].TransientParents)
				{
					//In the last loop any tags with an invalid ID set their IDs with TagRepository.AddTag(), so it should work fine to access at this point
					tagItem.ImmediateParentIDs.Add(transientParent.Id);
				}
				
				foreach (TagItem excluder in relatedTransientTags[tagItem].TransientExcluders)
				{
					tagItem.ImmediateExcludedByIDs.Add(excluder.Id);
				}
				
				//This only works because every tag either had their parental relationships destroyed in the last loop, or never had parents to begin with
				TagRepository.RegisterParentsOfTag(tagItem, connection);
				TagRepository.RegisterExcludersOfTag(tagItem, connection);
			}
			
			transaction.Commit();

			StringBuilder successMessage = new StringBuilder();
			successMessage.AppendLine("Successfully imported dictionary");
			if (appendedRows > 0)
			{
				successMessage.AppendLine($" >Appended {appendedRows} rows.");
			}
			if (updatedRows > 0)
			{
				successMessage.AppendLine($" >Updated {updatedRows} rows.");
			}
			if (deletedRows > 0)
			{
				successMessage.AppendLine($" >Deleted {deletedRows} rows.");
			}

			MessageBox.Show(successMessage.ToString(), "Success" , MessageBoxButton.OK);
		}

		private static readonly string[]? LineBreaks = { "\r\n", "\n" };
		private static bool TryParse(string input, out List<TransientTagItemForIO> result)
		{
			result = new List<TransientTagItemForIO>();
	        var lastTagAtIndent = new Dictionary<int, string>();
	        HashSet<string> addedNames = new HashSet<string>();
	        var lines = input.Split(LineBreaks, StringSplitOptions.RemoveEmptyEntries);

	        int lineNumber = 0;
	        foreach (var rawLine in lines)
	        {
		        lineNumber++;
	            if (string.IsNullOrWhiteSpace(rawLine))
	                continue;

	            int indent = GetIndentLevel(rawLine);
	            string line = rawLine.Trim();
	            
	            //Extract description, removing it from the line completely first
	            string description = "";
	            var descMatch = DescriptionRegex().Match(line);
	            if (descMatch.Success)
	            {
	                description = descMatch.Groups[1].Value;
	                line = line.Remove(descMatch.Index, descMatch.Length);
	            }
	            
	            
	            //Extract subcategory name, removing it from the line completely first
	            string subCategoryName = "";
	            var subMatch = SubCategoryRegex().Match(line);
	            if (subMatch.Success)
	            {
		            subCategoryName = subMatch.Groups[0].Value;
		            line = line.Remove(subMatch.Index - 1, subMatch.Length + 1);
	            }

	            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

	            //If it's an empty line, we leave.
	            if (tokens.Length == 0)
	                continue;

	            //If the line has no indentation, it's a category, so we store it and leave.
	            if (indent == 0)
	            {
		            lastTagAtIndent[indent] = tokens[0];

		            //Remove indentation history
		            List<int> nameKeysToRemove = lastTagAtIndent.Keys.Where(k => k > indent).ToList();
		            foreach (var key in nameKeysToRemove)
			            lastTagAtIndent.Remove(key);
		            continue;
	            }
	            
	            var item = new TransientTagItemForIO
	            {
	                Name = tokens[0],
	                Aliases = new List<string>(),
	                ParentNames = new List<string>(),
	                ExcluderNames = new List<string>(),
	                Description = description,
	                SubCategoryName = subCategoryName,
	                LineNumber = lineNumber
	            };

	            if (!addedNames.Add(item.Name))
	            {
		            MessageBox.Show($"Duplicate tag name '{item.Name}' on line {lineNumber}.", "Parsing error",
			            MessageBoxButton.OK, MessageBoxImage.Error);
		            return false;
	            }


	            //Get the category from first indentation
	            item.CategoryName = lastTagAtIndent[0];
	            
	            //Primary parent derived from indentation
	            if (indent > 1 && lastTagAtIndent.TryGetValue(indent - 1, out var parent))
	            {
		            item.ParentNames.Add(parent);
	            }
	            
	            //Aliases and secondary parents. We skip the first token, since that is our name
	            foreach (var token in tokens.Skip(1))
	            {
	                if (token.StartsWith('@'))
	                    item.Aliases.Add(token[1..]);
	                else if (token.StartsWith('>'))
	                    item.ParentNames.Add(token[1..]);
	                else if (token.StartsWith('!'))
		                item.ExcluderNames.Add(token[1..]);
	                else
	                {
		                //We have found an invalid token.
		                MessageBox.Show($"Unknown token '{token}' on tag at line {lineNumber}.", "Parsing error",
			                MessageBoxButton.OK, MessageBoxImage.Error);
		                return false;
	                }
	            }

	            lastTagAtIndent[indent] = item.Name;

	            //Remove indentation history
	            var keysToRemove = lastTagAtIndent.Keys.Where(k => k > indent).ToList();
	            foreach (var key in keysToRemove)
	                lastTagAtIndent.Remove(key);

	            result.Add(item);
	        }

	        return true;
	    }

	    private static int GetIndentLevel(string line)
	    {
	        int spaces = 0;
	        foreach (char c in line)
	        {
	            if (c == ' ')
	                spaces++;
	            else if (c == '\t')
	                spaces += 4;
	            else
	                break;
	        }

	        return spaces / 4;
	    }

        [GeneratedRegex("\"([^\"]*)\"")]
        private static partial Regex DescriptionRegex();
        
        [GeneratedRegex(@"(?<=\$)[^\s]+")]
        private static partial Regex SubCategoryRegex();
    }

	public class TransientTagItemForIO
	{
		public int LineNumber;
		public string CategoryName;
		public string SubCategoryName;
		public string Name;
		public string Description;
		public List<string> Aliases;
		public List<string> ParentNames;
		public List<string> ExcluderNames;

		public List<TagItem> TransientParents = new List<TagItem>();
		public List<TagItem> TransientExcluders = new List<TagItem>();

		public override string ToString()
		{
			string desc = "";
			if (Description != "")
			{
				desc = $"           Description starts: {Description[..(int)MathF.Min(30, Description.Length - 1)]}";
			}
			string aliasRead = Aliases.Count > 0 ? $"           Aliases: {string.Join(", ", Aliases)}." : "";
			string parentsRead = ParentNames.Count > 0 ? $"           Parents: {string.Join(", ", ParentNames)}." : "";
			string excludersRead = ExcluderNames.Count > 0 ? $"           Parents: {string.Join(", ", ExcluderNames)}." : "";
			return Name + ", " + CategoryName + "("+ SubCategoryName + ")" + desc + aliasRead + parentsRead + excludersRead;
		}
	}

	public abstract record TransientDisplayItem
	{
		public abstract string WriteForParser { get; }
	}
	public record TransientDisplayTagItem(TagItem Tag, int IndentLevel) : TransientDisplayItem
	{
		public override string WriteForParser => new string(' ', IndentLevel * 4) + Tag.GetParserWritable();
	}
	public record TransientDisplayCategoryItem(TagCategory Category) : TransientDisplayItem
	{
		public override string WriteForParser => Category.Title;
	}
}