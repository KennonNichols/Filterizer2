using System.Data.SQLite;
using System.IO;

namespace Filterizer2
{
    public static class MediaRepository
    {
        private const string UpdateMediaQuery = """
                                                              UPDATE Media
                                                              SET LocalFilename = @localFilename,
                                                                  Title = @title,
                                                                  Description = @description
                                                              WHERE Id = @Id;
                                              """;
        
        private const string MediaTagInsertQuery = """
                                           INSERT INTO MediaTags (MediaId, TagId)
                                           VALUES (@mediaId, @tagId);
                                           """;
        
        
        /// <summary>
        /// This should be the one and only place where MediaItems are stored in memory.
        /// </summary>
        private static Dictionary<int, MediaItem> _mediaCache = new Dictionary<int, MediaItem>();

        public static IEnumerable<string> GetAllMediaNames()
        {
	        using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
	        const string mediaQuery = "SELECT m.LocalFilename FROM Media m;";
	        using var command = new SQLiteCommand(mediaQuery, connection);
	        using var reader = command.ExecuteReader();
            
	        while (reader.Read())
	        {
		        yield return Path.GetFileNameWithoutExtension(reader["LocalFilename"].ToString()!);
	        }
        }
        
        public static IEnumerable<MediaItem> GetAllMediaItems(MediaSorter? sorter = null)
        {
            using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
            // Get sorting clause from sorter
            string orderByClause = "";
            if (sorter != null)
            {
	            if (sorter.SQLClause != "")
	            {
		            orderByClause = $"ORDER BY {sorter.SQLClause}";
	            }
            }
            string mediaQuery = $@"
		        SELECT 
		            m.*, COUNT(mt.TagId) AS TagCount
		        FROM Media m
		        LEFT JOIN MediaTags mt ON m.Id = mt.MediaId
		        GROUP BY m.Id
		        {orderByClause};
		    ";

            using var command = new SQLiteCommand(mediaQuery, connection);
            using var reader = command.ExecuteReader();
            
            while (reader.Read())
            {
	            yield return ReadRowAsMedia(reader, connection);
            }
        }
        
        public static bool TryGetMediaById(int mediaId, out MediaItem mediaItem)
        {
	        //Try immediately grabbing from cache instead of requerying the database
	        if (_mediaCache.TryGetValue(mediaId, out mediaItem!))
	        {
		        return true;
	        }
	        
	        using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
	        var command = connection.CreateCommand();
	        command.CommandText = "SELECT * FROM Media WHERE Id = @mediaId";
	        command.Parameters.AddWithValue("@mediaId", mediaId);
	        using var reader = command.ExecuteReader();
	        if (reader.Read())
	        {
		        mediaItem = ReadRowAsMedia(reader, connection);
		        _mediaCache.Add(mediaId, mediaItem);
		        return true;
	        }

	        mediaItem = null!;
	        return false;
        }
        
        /// <summary>
        /// Given an SQL reader and connection, read a row of the SQL query as a piece of media.
        /// 
        /// It is expected that it is currently on the row (so Reader.Read() should likely be called before this)
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="connection"></param>
        /// <returns></returns>
        private static MediaItem ReadRowAsMedia(SQLiteDataReader reader, SQLiteConnection connection)
        {
	        var media = new MediaItem()
	        {
		        Id = Convert.ToInt32(reader["Id"]),
		        LocalFilename = reader["LocalFilename"].ToString(),
		        Title = reader["Title"].ToString(),
		        Description = reader["Description"].ToString()
	        };

	        media.SetTags(GetTagsForMediaItem(media.Id, connection));
	        return media;
        }

        private static List<TagItem> GetTagsForMediaItem(int mediaId, SQLiteConnection connection)
        {
            var tags = new List<TagItem>();

            const string tagQuery = @"
                SELECT Tags.Id
                FROM Tags
                INNER JOIN MediaTags ON Tags.Id = MediaTags.TagId
                WHERE MediaTags.MediaId = @mediaId";

            using var command = new SQLiteCommand(tagQuery, connection);
            command.Parameters.AddWithValue("@mediaId", mediaId);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
	            if (TagRepository.TryGetTagById(Convert.ToInt32(reader["Id"]), out TagItem tag))
	            {
		            tags.Add(tag);
	            }
            }
            
            return tags;
        }
        
        public static void AddMedia(MediaItem mediaItem)
        {
            using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
            using var transaction = connection.BeginTransaction();
            // Insert media item
            const string mediaInsertQuery = @"
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
            foreach (var tag in mediaItem.GetTags())
            {
                using var command = new SQLiteCommand(MediaTagInsertQuery, connection);
                command.Parameters.AddWithValue("@mediaId", mediaItem.Id);
                command.Parameters.AddWithValue("@tagId", tag.Id);
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        public static void DeleteMedia(MediaItem mediaItem)
        {
            using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();
            using var transaction = connection.BeginTransaction();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Media WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", mediaItem.Id);
            command.ExecuteNonQuery();
            
            var command2 = connection.CreateCommand();
            command2.CommandText = "DELETE FROM MediaTags WHERE MediaId = @Id;";
            command2.Parameters.AddWithValue("@Id", mediaItem.Id);
            command2.ExecuteNonQuery();
            
            transaction.Commit();

            _mediaCache.Remove(mediaItem.Id);
        }

        public static void UpdateMedia(MediaItem mediaItem)
        {
            using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();

            using var transaction = connection.BeginTransaction();
            


            using (var updateTagCommand = new SQLiteCommand(UpdateMediaQuery, connection))
            {
                updateTagCommand.Parameters.AddWithValue("@title", mediaItem.Title);
                updateTagCommand.Parameters.AddWithValue("@description", mediaItem.Description);
                updateTagCommand.Parameters.AddWithValue("@localFilename", mediaItem.LocalFilename);
                updateTagCommand.Parameters.AddWithValue("@Id", mediaItem.Id);
                
                
                updateTagCommand.ExecuteNonQuery();
            }

            // 2. Delete existing aliases from the TagAliases table for this tag
            const string deleteTagRelationsQuery = "DELETE FROM MediaTags WHERE MediaId = @MediaId;";

            using (var deleteAliasesCommand = new SQLiteCommand(deleteTagRelationsQuery, connection))
            {
                deleteAliasesCommand.Parameters.AddWithValue("@MediaId", mediaItem.Id);
                deleteAliasesCommand.ExecuteNonQuery();
            }

            // Insert associated tags into the MediaTags table
            foreach (var tag in mediaItem.GetTags())
            {
                using var command = new SQLiteCommand(MediaTagInsertQuery, connection);
                command.Parameters.AddWithValue("@mediaId", mediaItem.Id);
                command.Parameters.AddWithValue("@tagId", tag.Id);
                command.ExecuteNonQuery();
            }
            // Commit transaction after all operations are successful
            transaction.Commit();

            // Remove from cache
            _mediaCache.Remove(mediaItem.Id);
        }
    }
}