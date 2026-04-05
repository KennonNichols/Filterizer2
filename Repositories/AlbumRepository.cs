using System.Data;
using System.Data.SQLite;

namespace Filterizer2.Repositories
{
	public class AlbumRepository
	{
		private const string DeleteAlbumMediaQuery = "DELETE FROM AlbumMedia WHERE AlbumId = @AlbumId;";
		private const string InsertAlbumMediaQuery = "INSERT INTO AlbumMedia (AlbumId, MediaId, MediaIndex) VALUES (@AlbumId, @MediaId, @MediaIndex);";
		private const string DeleteAlbumQuery = "DELETE FROM Album WHERE Id = @Id;";


		public static void AddAlbum(AlbumItem album)
		{
			using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();

			using var transaction = connection.BeginTransaction();
			const string insertAlbumQuery = @"
                INSERT INTO Album (Name, Description) 
                VALUES (@Name, @Description);
                SELECT last_insert_rowid();";

			using (var command = new SQLiteCommand(insertAlbumQuery, connection))
			{
				command.Parameters.AddWithValue("@Name", album.Name);
				command.Parameters.AddWithValue("@Description", album.Description);

				album.Id = Convert.ToInt32(command.ExecuteScalar());
			}

			const string insertAlbumMediaQuery = "INSERT INTO AlbumMedia (AlbumId, MediaId, MediaIndex) VALUES (@AlbumId, @MediaId, @MediaIndex);";

			using (var command = new SQLiteCommand(insertAlbumMediaQuery, connection))
			{
				command.Parameters.AddWithValue("@AlbumId", album.Id);
				int index = 0;
				
				foreach (var mediaItem in album.MediaItems)
				{
					command.Parameters.AddWithValue("@MediaId", mediaItem.Id);
					command.Parameters.AddWithValue("@MediaIndex", index);
					command.ExecuteNonQuery();
					//Clear parameters for next loop iteration
					command.Parameters.RemoveAt("@MediaId");  
					command.Parameters.RemoveAt("@MediaIndex");  

					index++;
				}
			}

			transaction.Commit();
		}
		
		
		public static IEnumerable<AlbumItem> GetAlbums()
		{
			using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();

			const string selectAlbumsQuery = "SELECT Id, Name, Description FROM Album;";

			using var command = new SQLiteCommand(selectAlbumsQuery, connection);
			using var reader = command.ExecuteReader();
			while (reader.Read())
			{
				var album = new AlbumItem
				{
					Id = reader.GetInt32(0),
					Name = reader.GetString(1),
					Description = reader.GetString(2),
				};
						
				//An album is made, now we need to get media items
						
				//First, get all media IDs associated with this album
				const string selectAlbumMediaQuery = @"
			                SELECT m.Id, am.MediaIndex
			                FROM Media m 
			                INNER JOIN AlbumMedia am ON am.MediaId = m.Id 
			                WHERE am.AlbumId = @AlbumId;";

				using var mediaCommand = new SQLiteCommand(selectAlbumMediaQuery, connection);
				mediaCommand.Parameters.AddWithValue("@AlbumId", album.Id);

				using var mediaReader = mediaCommand.ExecuteReader();

				List<MediaItem> items = new List<MediaItem>();
				Dictionary<MediaItem, int> indices = new Dictionary<MediaItem, int>();
				
				while (mediaReader.Read())
				{
					int mediaId = mediaReader.GetInt32(0);
					if (MediaRepository.TryGetMediaById(mediaId, out MediaItem foundItem))
					{
						items.Add(foundItem);
						indices[foundItem] = mediaReader.GetInt32(1);
					}
				}
				items.Sort((itemA, itemB) => indices[itemA].CompareTo(indices[itemB]));
				foreach (MediaItem mediaItem in items)
				{
					album.MediaItems.Add(mediaItem);
				}
				
				yield return album;
			}
		}

		
		public static void UpdateAlbum(AlbumItem? album)
		{
			using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();

			using var transaction = connection.BeginTransaction();
			// Update the album details
			const string updateAlbumQuery = @"
                UPDATE Album 
                SET Name = @Name, Description = @Description 
                WHERE Id = @Id;";

			using (var command = new SQLiteCommand(updateAlbumQuery, connection))
			{
				command.Parameters.AddWithValue("@Name", album.Name);
				command.Parameters.AddWithValue("@Description", album.Description);
				command.Parameters.AddWithValue("@Id", album.Id);
				command.ExecuteNonQuery();
			}


			using (var command = new SQLiteCommand(DeleteAlbumMediaQuery, connection))
			{
				command.Parameters.AddWithValue("@AlbumId", album.Id);
				command.ExecuteNonQuery();
			}


			using (var command = new SQLiteCommand(InsertAlbumMediaQuery, connection))
			{
				command.Parameters.AddWithValue("@AlbumId", album.Id);
				int index = 0;
				
				foreach (var mediaItem in album.MediaItems)
				{
					command.Parameters.AddWithValue("@MediaId", mediaItem.Id);
					command.Parameters.AddWithValue("@MediaIndex", index);
					command.ExecuteNonQuery();
					//Clear parameters for next loop iteration
					command.Parameters.RemoveAt("@MediaId");  
					command.Parameters.RemoveAt("@MediaIndex");  

					index++;
				}
			}

			transaction.Commit();
		}
		
		public static void DeleteAlbum(AlbumItem albumItem)
		{
			using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();

			using var transaction = connection.BeginTransaction();

			using (var command = new SQLiteCommand(DeleteAlbumQuery, connection))
			{
				command.Parameters.AddWithValue("@Id", albumItem.Id);
				command.ExecuteNonQuery();
			}

			transaction.Commit();
		}


	}
}