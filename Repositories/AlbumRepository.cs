using System.Data.SQLite;

namespace Filterizer2.Repositories
{
	public class AlbumRepository
	{
		private const string DeleteAlbumMediaQuery = "DELETE FROM AlbumMedia WHERE AlbumId = @AlbumId;";
		private const string InsertAlbumMediaQuery = "INSERT INTO AlbumMedia (AlbumId, MediaId) VALUES (@AlbumId, @MediaId);";
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

			const string insertAlbumMediaQuery = "INSERT INTO AlbumMedia (AlbumId, MediaId) VALUES (@AlbumId, @MediaId);";

			using (var command = new SQLiteCommand(insertAlbumMediaQuery, connection))
			{
				command.Parameters.AddWithValue("@AlbumId", album.Id);

				foreach (var mediaItem in album.MediaItems)
				{
					command.Parameters.AddWithValue("@MediaId", mediaItem.Id);
					command.ExecuteNonQuery();
					command.Parameters.RemoveAt("@MediaId");  // Clear parameter for next loop iteration
				}
			}

			transaction.Commit();
		}
		
		
		public static List<AlbumItem> GetAlbums()
		{
			List<AlbumItem> albums = new List<AlbumItem>();

			using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();

			string selectAlbumsQuery = "SELECT Id, Name, Description FROM Album;";

			using (var command = new SQLiteCommand(selectAlbumsQuery, connection))
			{
				using (var reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						var album = new AlbumItem
						{
							Id = reader.GetInt32(0),
							Name = reader.GetString(1),
							Description = reader.GetString(2),
						};
						albums.Add(album);
					}
				}
			}

			foreach (var album in albums)
			{
				const string selectAlbumMediaQuery = @"
		                SELECT m.Id, m.LocalFilename, m.Title, m.Description 
		                FROM Media m 
		                INNER JOIN AlbumMedia am ON am.MediaId = m.Id 
		                WHERE am.AlbumId = @AlbumId;";

				using var command = new SQLiteCommand(selectAlbumMediaQuery, connection);
				command.Parameters.AddWithValue("@AlbumId", album.Id);

				using var reader = command.ExecuteReader();
				while (reader.Read())
				{
					var mediaItem = new MediaItem
					{
						Id = reader.GetInt32(0),
						LocalFilename = reader.GetString(1),
						Title = reader.GetString(2),
						Description = reader.GetString(3)
					};
					album.MediaItems.Add(mediaItem);
				}
			}

			return albums;
		}

		
		public static void UpdateAlbum(AlbumItem? album)
		{
			using var connection = ManagementHelpers.GetAndOpenDatabaseConnection();

			using var transaction = connection.BeginTransaction();
			// Update the album details
			string updateAlbumQuery = @"
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

				foreach (var mediaItem in album.MediaItems)
				{
					command.Parameters.AddWithValue("@MediaId", mediaItem.Id);
					command.ExecuteNonQuery();
					command.Parameters.RemoveAt("@MediaId");
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