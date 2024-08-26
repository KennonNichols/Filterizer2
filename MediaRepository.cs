using System.Data.SQLite;

namespace Filterizer2
{
    public static class MediaRepository
    {
        public static List<MediaItem> GetAllMediaItems()
        {
            var mediaItems = new List<MediaItem>();

            using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
            // First, get all media items
            string mediaQuery = "SELECT * FROM Media";
            using (var command = new SQLiteCommand(mediaQuery, connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var mediaItem = new MediaItem
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            LocalFilename = reader["LocalFilename"].ToString(),
                            Title = reader["Title"].ToString(),
                            Description = reader["Description"].ToString()
                        };
                        mediaItems.Add(mediaItem);
                    }
                }
            }

            // Then, get and associate tags for each media item
            foreach (var mediaItem in mediaItems)
            {
                mediaItem.Tags = GetTagsForMediaItem(mediaItem.Id, connection);
            }

            return mediaItems;
        }

        private static List<TagItem> GetTagsForMediaItem(int mediaId, SQLiteConnection connection)
        {
            var tags = new List<TagItem>();

            string tagQuery = @"
                SELECT Tags.Id, Tags.Name, Tags.Category, Tags.Description
                FROM Tags
                INNER JOIN MediaTags ON Tags.Id = MediaTags.TagId
                WHERE MediaTags.MediaId = @mediaId";

            using (var command = new SQLiteCommand(tagQuery, connection))
            {
                command.Parameters.AddWithValue("@mediaId", mediaId);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var tag = new TagItem()
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            Name = reader["Name"].ToString(),
                            Category = (TagCategory)Convert.ToInt32(reader["Category"]),
                            Description = reader["Description"].ToString()
                        };
                        tags.Add(tag);
                    }
                }
            }

            
            
            return tags;
        }
        
        public static void AddMedia(MediaItem mediaItem)
        {
            using (var connection = ManagementHelpers.GetAndOpenDatabaseConnection())
            {
                using (var transaction = connection.BeginTransaction())
                {
                    // Insert media item
                    string mediaInsertQuery = @"
                    INSERT INTO Media (LocalFilename, Title, Description)
                    VALUES (@localFilename, @title, @description);
                    SELECT last_insert_rowid();";

                    using (var command = new SQLiteCommand(mediaInsertQuery, connection))
                    {
                        command.Parameters.AddWithValue("@localFilename", mediaItem.LocalFilename);
                        command.Parameters.AddWithValue("@title", mediaItem.Title);
                        command.Parameters.AddWithValue("@description", mediaItem.Description);

                        // Get the inserted media item's ID
                        mediaItem.Id = Convert.ToInt32(command.ExecuteScalar());
                    }

                    // Insert associated tags into the MediaTags table
                    foreach (var tag in mediaItem.Tags)
                    {
                        string mediaTagInsertQuery = @"
                        INSERT INTO MediaTags (MediaId, TagId)
                        VALUES (@mediaId, @tagId);";

                        using (var command = new SQLiteCommand(mediaTagInsertQuery, connection))
                        {
                            command.Parameters.AddWithValue("@mediaId", mediaItem.Id);
                            command.Parameters.AddWithValue("@tagId", tag.Id);
                            command.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
            }
        }
    }
}