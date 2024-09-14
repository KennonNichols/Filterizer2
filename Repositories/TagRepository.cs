using System.Data.Entity.ModelConfiguration.Configuration;
using System.Data.SQLite;

namespace Filterizer2
{
    
    public static class TagRepository
    {
        private const string CreateTagQuery = """
                                              INSERT INTO Tags (Name, Category, Description) VALUES (@name, @category, @description);
                                              """;
        private const string UpdateTagQuery = """
                                                              UPDATE Tags 
                                                              SET Name = @Name, 
                                                                  Description = @Description, 
                                                                  Category = @Category 
                                                              WHERE Id = @Id;
                                              """;
        private const string DeleteTagQuery = "DELETE FROM Tags WHERE Id = @Id;";
        
        
        
        
        
        
        public static void AddTag(TagItem tag)
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

            transaction.Commit();
        }

        public static List<TagItem> GetTags()
        {
            var tags = new List<TagItem>();

            using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Tags";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var tag = new TagItem
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Category = Tags.GetCategoryOfName(reader.GetString(2)),
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

                tags.Add(tag);
            }

            return tags;
        }
        
        
        public static List<TagItem> SearchTags(string searchString)
        {
            if (searchString == "")
            {
                return GetTags();
            }
            
            var matchingTags = new List<TagItem>();

            using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
            var command = connection.CreateCommand();

            // SQL to search for matches in both Tags and TagAliases
            command.CommandText = """
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
                                      END AS SortOrder
                                  FROM Tags T
                                  LEFT JOIN TagAliases A ON T.Id = A.TagId
                                  WHERE T.Name LIKE @like OR Alias LIKE @like
                                  ORDER BY SortOrder, SearchField ASC;
                                  """;

            command.Parameters.AddWithValue("@search", searchString);
            command.Parameters.AddWithValue("@startWith", searchString + "%");
            command.Parameters.AddWithValue("@like", "%" + searchString + "%");

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                int tagId = reader.GetInt32(0);

                // Check if this tag has already been added to the list (to avoid duplicates)
                var tagItem = matchingTags.FirstOrDefault(t => t.Id == tagId);
                if (tagItem == null)
                {
                    tagItem = new TagItem
                    {
                        Id = tagId,
                        Name = reader.GetString(1),
                        Category = Tags.GetCategoryOfName(reader.GetString(2)),
                        Description = reader.GetString(3),
                        Aliases = new List<string>()
                    };
                    matchingTags.Add(tagItem);
                }

                string alias = reader.IsDBNull(4) ? "" : reader.GetString(4);
                if (!string.IsNullOrEmpty(alias) && alias != tagItem.Name)
                {
                    tagItem.Aliases.Add(alias);
                }
            }

            return matchingTags;
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

            // 2. Delete existing aliases from the TagAliases table for this tag
            const string deleteAliasesQuery = "DELETE FROM TagAliases WHERE TagId = @TagId;";

            using (var deleteAliasesCommand = new SQLiteCommand(deleteAliasesQuery, connection))
            {
                deleteAliasesCommand.Parameters.AddWithValue("@TagId", editingTag.Id);
                deleteAliasesCommand.ExecuteNonQuery();
            }

            // 3. Insert new aliases into the TagAliases table
            const string insertAliasQuery = "INSERT INTO TagAliases (TagId, Alias) VALUES (@TagId, @Alias);";

            using (var insertAliasCommand = new SQLiteCommand(insertAliasQuery, connection))
            {
                insertAliasCommand.Parameters.AddWithValue("@TagId", editingTag.Id);
                
                foreach (var alias in editingTag.Aliases)
                {
                    insertAliasCommand.Parameters.AddWithValue("@Alias", alias);
                    insertAliasCommand.ExecuteNonQuery();
                    insertAliasCommand.Parameters.RemoveAt("@Alias"); // Clear parameter for the next loop iteration
                }
            }

            // Commit transaction after all operations are successful
            transaction.Commit();
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
        }
    }
}