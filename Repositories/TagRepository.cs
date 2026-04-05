using System.Data.Entity.ModelConfiguration.Configuration;
using System.Data.SQLite;

namespace Filterizer2
{
    
    public static class TagRepository
    {
        private const string CreateTagQuery = "INSERT INTO Tags (Name, Category, Description) VALUES (@name, @category, @description);";
        private const string UpdateTagQuery = """
                                                              UPDATE Tags 
                                                              SET Name = @Name, 
                                                                  Description = @Description, 
                                                                  Category = @Category 
                                                              WHERE Id = @Id;
                                              """;
        private const string DeleteTagQuery = "DELETE FROM Tags WHERE Id = @Id;";
        private const string DeleteAllTagsQuery = "DELETE FROM Tags;";



        /// <summary>
        /// This should be the one and only place where TagItems are stored in memory.
        /// </summary>
        private static Dictionary<int, TagItem> _tagCache = new Dictionary<int, TagItem>();
        
        
        public static TagItem AddTag(TagItem tag)
        {
            using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
            using var transaction = connection.BeginTransaction();
            var command = connection.CreateCommand();
            command.CommandText = CreateTagQuery;
            command.Parameters.AddWithValue("@name", tag.Name);
            command.Parameters.AddWithValue("@category", tag.Category.Title);
            command.Parameters.AddWithValue("@description", tag.Description);
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
            foreach (var parentTagId in tag.ParentIDs)
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
		        tagItem = ReadRowAsTag(reader, connection);
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
                yield return ReadRowAsTag(reader, connection);
            }
        }

        private static TagItem ReadRowAsTag(SQLiteDataReader reader, SQLiteConnection connection)
        {
	        var tag = new TagItem
	        {
		        Id = reader.GetInt32(0),
		        Name = reader.GetString(1),
		        Category = Tags.GetCategoryOfName(reader.GetString(2), true),
		        Description = reader.GetString(3)
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
			        tag.ParentIDs.Add(parentReader.GetInt32(0));
		        }
	        }

	        return tag;
        }

        
        public static IEnumerable<TagItem> SearchTags(string searchString)
        {
	        return SearchTags(searchString, new HashSet<int>());
        }
        public static IEnumerable<TagItem> SearchTags(string searchString, HashSet<int> blacklist)
        {
	        return SearchTagsInternal(searchString).Where(tagItem => blacklist.All(i => i != tagItem.Id));
        }
        private static IEnumerable<TagItem> SearchTagsInternal(string searchString)
        {
            if (searchString == "")
            {
	            foreach (TagItem tagItem in GetTags())
	            {
		            yield return tagItem;
	            }
	            
	            yield break;
            }

            using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
            var command = connection.CreateCommand();

            // SQL to search for matches in both Tags and TagAliases
            command.CommandText = """
									WITH RankedTags AS (
									    SELECT 
									        T.Id, 
									        T.Name, 
									        T.Category, 
									        T.Description,
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
									    FROM Tags T
									    LEFT JOIN TagAliases A ON T.Id = A.TagId
									    WHERE T.Name LIKE @like OR Alias LIKE @like
									)
									SELECT 
									    Id,
									    Name,
									    Category,
									    Description,
									    SearchField,
									    SortOrder
									FROM RankedTags
									WHERE rn = 1
									ORDER BY SortOrder, SearchField ASC;
									""";

            command.Parameters.AddWithValue("@search", searchString);
            command.Parameters.AddWithValue("@startWith", searchString + "%");
            command.Parameters.AddWithValue("@like", "%" + searchString + "%");

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                yield return ReadRowAsTag(reader, connection);
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
                
		        foreach (int relationshipParentId in tag.ParentIDs)
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
        }
    }
}