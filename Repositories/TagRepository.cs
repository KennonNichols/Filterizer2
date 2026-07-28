using System.Collections;
using System.Data.Entity.ModelConfiguration.Configuration;
using System.Data.SQLite;

namespace Filterizer2
{
    
    public static class TagRepository
    {
        private const string CreateTagQuery = "INSERT INTO Tags (Name, Category, Description, SubCategory) VALUES (@name, @category, @description, @subCategory);";
        private const string UpdateTagQuery = """
                                                              UPDATE Tags 
                                                              SET Name = @Name, 
                                                                  Description = @Description, 
                                                                  Category = @Category,
                                                                  SubCategory = @SubCategory
                                                              WHERE Id = @Id;
                                              """;
        private const string DeleteTagQuery = "DELETE FROM Tags WHERE Id = @Id;";
        private const string DeleteAllTagsQuery = "DELETE FROM Tags;";



        /// <summary>
        /// This should be the one and only place where TagItems are stored in memory.
        /// </summary>
        private static Dictionary<int, TagItem> _tagCache = new Dictionary<int, TagItem>();

        private static TagItem? _taggingInProgressTag = null;
        

        #region Getters and Search

        public static bool TryGetTaggingInProgressTag(out TagItem taggingInProgressTag)
        {
	        if (_taggingInProgressTag != null)
	        {
		        taggingInProgressTag = _taggingInProgressTag;
		        return true;
	        }
	        taggingInProgressTag = GetTagByNameDirectly("Tagging_In_Progress");
	        if (taggingInProgressTag == null)
	        {
		        return false;
	        }
	        _taggingInProgressTag = taggingInProgressTag;
	        return true;
        }
        private static TagItem? GetTagByNameDirectly(string name)
        {
	        using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
	        var command = connection.CreateCommand();
	        command.CommandText = "SELECT * FROM Tags WHERE Name = @name";

	        command.Parameters.AddWithValue("@name", name);
	        
	        using var reader = command.ExecuteReader();
	        return reader.Read() ? ReadRowAsTag(reader, connection, name) : null;
        }
        public static bool TryGetTagById(int tagId, out TagItem tagItem)
        {
	        //Try immediately grabbing from cache instead of requerying the database
	        if (_tagCache.TryGetValue(tagId, out tagItem!))
	        {
		        return true;
	        }
	        
	        using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
	        var command = connection.CreateCommand();
	        command.CommandText = "SELECT * FROM Tags WHERE Id = @tagId";
	        command.Parameters.AddWithValue("@tagId", tagId);
	        using var reader = command.ExecuteReader();
	        if (reader.Read())
	        {
		        tagItem = ReadRowAsTag(reader, connection, tagId.ToString());
		        _tagCache.Add(tagId, tagItem);
		        return true;
	        }

	        tagItem = null!;
	        return false;
        }

        public static IEnumerable<TagItem> GetTags()
        {
	        using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Tags";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                yield return ReadRowAsTag(reader, connection, "Iterating all tags");
            }
        }

        public static IEnumerable<int> GetAllParentIDsRecursively(int id)
        {
	        using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
	        var command = connection.CreateCommand();

	        // string nonChildableCats = "'" + string.Join("','", Tags.NonChildableCats) + "'";

	        // SQL to search for all parent IDs
	        command.CommandText = """
	                                	WITH RECURSIVE ParentTags AS
	                                     (
	                                      --Direct parents
	                                     SELECT
	                                     i.TagId,
	                                     i.ParentTagId,
	                                     1 AS Depth
	                                     FROM Implications i
	                                      WHERE i.TagId = @TagId
	                              
	                                     UNION ALL
	                              
	                                     --Recursion
	                                      SELECT
	                                     p.TagId,
	                                     i.ParentTagId,
	                                     p.Depth + 1
	                                     FROM ParentTags p
	                                      JOIN Implications i
	                                     ON p.ParentTagId = i.TagId
	                                      )
	                                     SELECT DISTINCT ParentTagId
	                                      FROM ParentTags
	                                      ORDER BY ParentTagId;
	                              """;
	        command.Parameters.AddWithValue("@TagId", id);
	        
	        using var reader = command.ExecuteReader();
	        while (reader.Read())
	        {
		        yield return reader.GetInt32(0);
	        }
        }
        public static IEnumerable<TagItem> GetAllTagsChildOf(int id, HashSet<int> blacklist)
        {
	        foreach (int childId in GetAllTagIdsChildOf(id, blacklist))
	        {
		        if (TryGetTagById(childId, out TagItem tagItem))
		        {
			        yield return tagItem;
		        }
	        }
        }
        public static IEnumerable<int> GetAllTagIdsChildOf(int id, HashSet<int>? blacklist, bool recursive = false)
        {
	        using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
	        var command = connection.CreateCommand();
	        
	        List<string> catParameterNames = new List<string>();
	        int index = 0;
	        foreach (TagCategory tag in Tags.NonChildableCats)
	        {
		        string paramName = $"$p{index}";
		        catParameterNames.Add(paramName);
    
		        //Bind the value to the generated name
		        command.Parameters.AddWithValue(paramName, tag.Title);
		        index++;
	        }
	        string nonChildableCatParams = string.Join(", ", catParameterNames);

	        string blacklistParams = string.Empty;
	        if (blacklist != null)
	        {
		        List<string> blacklistParameterNames = new List<string>();
		        index = 0;
		        foreach (int blacklistedInt in blacklist)
		        {
			        string paramName = $"$bl{index}";
			        blacklistParameterNames.Add(paramName);
    
			        //Bind the value to the generated name
			        command.Parameters.AddWithValue(paramName, blacklistedInt);
			        index++;
		        }
		        blacklistParams = string.Join(", ", blacklistParameterNames);
	        }

	        if (recursive)
	        {
		        //Recursive case
		        command.CommandText = $"""
		                               WITH RECURSIVE Children(TagId) AS
		                               (
		                                   SELECT TagId
		                                   FROM Implications
		                                   WHERE ParentTagId = @id
		                               
		                                   UNION
		                               
		                                   SELECT I.TagId
		                                   FROM Implications I
		                                   INNER JOIN Children C
		                                       ON I.ParentTagId = C.TagId
		                               )
		                               SELECT DISTINCT T.*
		                               FROM Tags T
		                               INNER JOIN Children C
		                                   ON T.Id = C.TagId
		                               WHERE T.Id NOT IN ({blacklistParams})
		                                 AND T.Category NOT IN ({nonChildableCatParams})
		                               ORDER BY T.Name;
		                               """;
	        }
	        else
	        {
		        // SQL to search for all direct child IDs
		        command.CommandText = $"SELECT * FROM Tags T LEFT JOIN Implications I ON T.id = I.TagId WHERE I.ParentTagId = @id AND T.id NOT IN ({blacklistParams}) AND T.Category NOT IN ({nonChildableCatParams});";

	        }
            
	        command.Parameters.AddWithValue("@id", id);
	        
	        using var reader = command.ExecuteReader();
	        while (reader.Read())
	        {
		        yield return reader.GetInt32(0);
	        }
        }

        public static bool CheckAnyTagsOfSubcategoryExist(TagSubCategory? subCategory)
        {
	        using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
	        var command = connection.CreateCommand();

	        command.CommandText =
		        "SELECT EXISTS(SELECT 1 FROM Tags WHERE Category = @category AND SubCategory = @subCategory);";
		        
	        command.Parameters.AddWithValue("@category", subCategory?.Parent.Title ?? "%");
	        command.Parameters.AddWithValue("@subCategory", subCategory?.TagStringForDatabase ?? "%");
	        
	        return Convert.ToBoolean(command.ExecuteScalar());
        }
        public static IEnumerable<TagItem> SearchTags(string searchString)
        {
	        return SearchTags(searchString, new HashSet<int>());
        }
        public static IEnumerable<TagItem> SearchTags(string searchString, HashSet<int> blacklist)
        {
	        return SearchTags(searchString, null, blacklist);
        }
        public static IEnumerable<TagItem> SearchTags(string searchString, TagSubCategory? subCategory, HashSet<int> blacklist)
        {
            if (searchString == "" && subCategory == null && blacklist.Count == 0)
            {
	            foreach (TagItem tagItem in GetTags())
	            {
		            yield return tagItem;
	            }
	            
	            yield break;
            }

            using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
            var command = connection.CreateCommand();
            
            List<string> parameterNames = new List<string>();
            int index = 0;
            foreach (int id in blacklist)
            {
	            string paramName = $"$p{index}";
	            parameterNames.Add(paramName);
    
	            //Bind the value to the generated name
	            command.Parameters.AddWithValue(paramName, id);
	            index++;
            }
            string blacklistParams = string.Join(", ", parameterNames);

            //TODO search with miscellaneous substring
            
            if (searchString == "")
            {
	            //The no-name search command is much simpler
	            command.CommandText = $"""
	                                   SELECT 
	                                   		T.Id, 
	                                   		T.Name, 
	                                   		T.Category, 
	                                   		T.Description,
	                                   		T.SubCategory
	                                   FROM Tags T
	                                   WHERE T.SubCategory LIKE @subCategory AND T.Category LIKE @category AND T.Id NOT IN ({blacklistParams})
	                                   """;
            }
            else
            {
	            //SQL to search for matches in both Tags and TagAliases
				command.CommandText = $"""
									WITH RankedTags AS (
										WITH TagCandidates AS (
											SELECT 
												T.Id, 
												T.Name, 
												T.Category, 
												T.Description,
												T.SubCategory
											FROM Tags T
											WHERE T.SubCategory LIKE @subCategory AND T.Category LIKE @category AND T.Id NOT IN ({blacklistParams})
										)
									    SELECT 
									        T.Id, 
									        T.Name, 
									        T.Category, 
									        T.Description,
									        T.SubCategory,
									        COALESCE(Alias, T.Name) AS SearchField,
									        CASE 
									            WHEN T.Name = @search OR Alias = @search THEN 0
									            WHEN T.Name LIKE @startWith OR Alias LIKE @startWith THEN 1
									            ELSE 2
									        END AS SortOrder,
											ROW_NUMBER() OVER (
											    PARTITION BY T.Id 
											    ORDER BY 
											        CASE 
											            WHEN T.Name = @search OR A.Alias = @search THEN 0
											            WHEN T.Name LIKE @startWith OR A.Alias LIKE @startWith THEN 1
											            ELSE 2
											        END,
											        COALESCE(A.Alias, T.Name)
											) AS rn
									    FROM TagCandidates T
									    LEFT JOIN TagAliases A ON T.Id = A.TagId
									    WHERE T.Name LIKE @like OR Alias LIKE @like
									)
									SELECT 
									    Id,
									    Name,
									    Category,
									    Description,
									    SubCategory,
									    SearchField,
									    SortOrder
									FROM RankedTags
									WHERE rn = 1
									ORDER BY SortOrder, SearchField ASC;
									""";
            }
            
            command.Parameters.AddWithValue("@category", subCategory?.Parent.Title ?? "%");
            command.Parameters.AddWithValue("@subCategory", subCategory?.TagStringForDatabase ?? "%");
            command.Parameters.AddWithValue("@search", searchString);
            command.Parameters.AddWithValue("@startWith", searchString + "%");
            command.Parameters.AddWithValue("@like", "%" + searchString + "%");

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                yield return ReadRowAsTag(reader, connection, searchString);
            }
        }
        #endregion
        
        #region CRUD
        public static TagItem AddTag(TagItem tag)
        {
	        using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
	        using var transaction = connection.BeginTransaction();
	        var command = connection.CreateCommand();
	        command.CommandText = CreateTagQuery;
	        command.Parameters.AddWithValue("@name", tag.Name);
	        command.Parameters.AddWithValue("@category", tag.Category.Title);
	        command.Parameters.AddWithValue("@description", tag.Description);
	        command.Parameters.AddWithValue("@subCategory", tag.SubCategory.TagStringForDatabase);
	        command.ExecuteNonQuery();

	        // Get the last inserted Tag Id
	        long tagId = connection.LastInsertRowId;

	        // Insert the aliases
	        foreach (var alias in tag.Aliases)
	        {
		        var aliasCommand = connection.CreateCommand();
		        aliasCommand.CommandText = "INSERT INTO TagAliases (TagId, Alias) VALUES (@tagId, @alias);";
		        aliasCommand.Parameters.AddWithValue("@tagId", tagId);
		        aliasCommand.Parameters.AddWithValue("@alias", alias);
		        aliasCommand.ExecuteNonQuery();
	        }
            
	        // Insert the parents
	        foreach (var parentTagId in tag.ImmediateParentIDs)
	        {
		        var aliasCommand = connection.CreateCommand();
		        aliasCommand.CommandText = "INSERT INTO Implications (TagId, ParentTagId) VALUES (@tagId, @parentTagId);";
		        aliasCommand.Parameters.AddWithValue("@tagId", tagId);
		        aliasCommand.Parameters.AddWithValue("@parentTagId", parentTagId);
		        aliasCommand.ExecuteNonQuery();
	        }

	        tag.Id = (int)tagId;
	        transaction.Commit();

	        return tag;
        }

        private static TagItem ReadRowAsTag(SQLiteDataReader reader, SQLiteConnection connection, string accessDescription)
        {
	        try
	        {
		        TagCategory category = Tags.GetCategoryOfName(reader.GetString(2), true);
		        var tag = new TagItem
		        {
			        Id = reader.GetInt32(0),
			        Name = reader.GetString(1),
			        Category = category,
			        Description = reader.GetString(3),
			        SubCategory = category.GetSubCategoryByName(reader.GetString(4))
		        };

		        // Retrieve aliases
		        var aliasCommand = connection.CreateCommand();
		        aliasCommand.CommandText = "SELECT Alias FROM TagAliases WHERE TagId = @tagId";
		        aliasCommand.Parameters.AddWithValue("@tagId", tag.Id);

		        using (var aliasReader = aliasCommand.ExecuteReader())
		        {
			        while (aliasReader.Read())
			        {
				        tag.Aliases.Add(aliasReader.GetString(0));
			        }
		        }

		        // Retrieve parent IDs
		        var parentCommand = connection.CreateCommand();
		        parentCommand.CommandText = "SELECT ParentTagId FROM Implications WHERE TagId = @tagId";
		        parentCommand.Parameters.AddWithValue("@tagId", tag.Id);

		        using (var parentReader = parentCommand.ExecuteReader())
		        {
			        while (parentReader.Read())
			        {
				        tag.ImmediateParentIDs.Add(parentReader.GetInt32(0));
			        }
		        }

		        return tag;
	        }
	        catch (Exception e)
	        {
		        App.ShowExceptionWindow(e, "Error loading tag: " + accessDescription);
		        throw;
	        }
        }

        public static void UpdateTag(TagItem editingTag)
        {
            using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();

            using var transaction = connection.BeginTransaction();
            


            using (var updateTagCommand = new SQLiteCommand(UpdateTagQuery, connection))
            {
                updateTagCommand.Parameters.AddWithValue("@Name", editingTag.Name);
                updateTagCommand.Parameters.AddWithValue("@Description", editingTag.Description);
                updateTagCommand.Parameters.AddWithValue("@Category", editingTag.Category.Title);
                updateTagCommand.Parameters.AddWithValue("@SubCategory", editingTag.SubCategory.TagStringForDatabase);
                updateTagCommand.Parameters.AddWithValue("@Id", editingTag.Id);
                
                updateTagCommand.ExecuteNonQuery();
            }

            //Delete existing aliases from the TagAliases table for this tag
            const string deleteAliasesQuery = "DELETE FROM TagAliases WHERE TagId = @TagId;";

            using (var deleteAliasesCommand = new SQLiteCommand(deleteAliasesQuery, connection))
            {
                deleteAliasesCommand.Parameters.AddWithValue("@TagId", editingTag.Id);
                deleteAliasesCommand.ExecuteNonQuery();
            }

            //Insert new aliases into the TagAliases table
            const string insertAliasQuery = "INSERT INTO TagAliases (TagId, Alias) VALUES (@TagId, @Alias);";

            using (var insertAliasCommand = new SQLiteCommand(insertAliasQuery, connection))
            {
                insertAliasCommand.Parameters.AddWithValue("@TagId", editingTag.Id);
                
                foreach (var alias in editingTag.Aliases)
                {
                    insertAliasCommand.Parameters.AddWithValue("@Alias", alias);
                    insertAliasCommand.ExecuteNonQuery();
                    insertAliasCommand.Parameters.RemoveAt("@Alias"); //Clear parameter for the next loop iteration
                }
            }

            //Repeat for parental relationships
            RegisterParentsOfTag(editingTag, connection);
            
            
            transaction.Commit();

            _tagCache.Remove(editingTag.Id);
            _taggingInProgressTag = null;
        }

        public static void RegisterParentsOfTag(TagItem tag, SQLiteConnection connection)
        {
	        const string deleteImplicationsQuery = "DELETE FROM Implications WHERE TagId = @TagId;";

	        using (var deleteImplicationsCommand = new SQLiteCommand(deleteImplicationsQuery, connection))
	        {
		        deleteImplicationsCommand.Parameters.AddWithValue("@TagId", tag.Id);
		        deleteImplicationsCommand.ExecuteNonQuery();
	        }
            
	        const string insertImplicationQuery = "INSERT INTO Implications (TagId, ParentTagId) VALUES (@TagId, @ParentTagId);";

	        using (var insertImplicationCommand = new SQLiteCommand(insertImplicationQuery, connection))
	        {
		        insertImplicationCommand.Parameters.AddWithValue("@TagId", tag.Id);
                
		        foreach (int relationshipParentId in tag.ImmediateParentIDs)
		        {
			        insertImplicationCommand.Parameters.AddWithValue("@ParentTagId", relationshipParentId);
			        insertImplicationCommand.ExecuteNonQuery();
			        insertImplicationCommand.Parameters.RemoveAt("@ParentTagId"); //Clear parameter for the next loop iteration
		        }
	        }
        }

        public static void DeleteTag(TagItem tag)
        {
            using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
            using var transaction = connection.BeginTransaction();
            var command = connection.CreateCommand();
            command.CommandText = DeleteTagQuery;
            command.Parameters.AddWithValue("@Id", tag.Id);
            command.ExecuteNonQuery();
            transaction.Commit();

            _tagCache.Remove(tag.Id);
            _taggingInProgressTag = null;
        }

        public static void DeleteAllTags()
        {
	        using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
	        using var transaction = connection.BeginTransaction();
	        var command = connection.CreateCommand();
	        command.CommandText = DeleteAllTagsQuery;
	        command.ExecuteNonQuery();
	        transaction.Commit();
	        
	        _tagCache.Clear();
	        _taggingInProgressTag = null;
        }
        #endregion
        
    }
}