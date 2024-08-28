using System.Data.SQLite;
using System.IO;
using System.Windows;

namespace Filterizer2
{
    public static class ManagementHelpers
    {
        public static string? CopyMediaToLocalFolder(string sourceFilePath, string destinationFolder)
        {
            try
            {
                string fileName = Path.GetFileName(sourceFilePath);
                string uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
                string destinationFilePath = Path.Combine(destinationFolder, uniqueFileName);

                File.Copy(sourceFilePath, destinationFilePath);

                return destinationFilePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
        
        
        private static readonly string DatabasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MediaDatabase.db");

        /// <summary>
        /// Returns an SQLiteConnection for the Media database. Creates the database if it does not exist.
        /// </summary>
        public static SQLiteConnection GetAndOpenDatabaseConnection()
        {
            return GetDatabaseConnection(DatabasePath, CreateDatabase);
        }

        /// <summary>
        /// Returns an SQLiteConnection for a given database path. Creates the database using a provided creation function if it does not exist.
        /// </summary>
        private static SQLiteConnection GetDatabaseConnection(string databasePath, Action createDatabase)
        {
            try
            {
                bool dbExists = File.Exists(databasePath);

                // Open or create the SQLite connection
                var connection = new SQLiteConnection($"Data Source={databasePath}");
                connection.Open();

                // If the database doesn't exist, create it
                if (dbExists && CheckedThisLaunch) return connection;
                
                createDatabase.Invoke();
                CheckedThisLaunch = true;
                
                return connection;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to database: {ex.Message}");
                throw;
            }
        }

        private static bool CheckedThisLaunch = false;

        /// <summary>
        /// Creates the Media database with the required schema.
        /// </summary>
        private static void CreateDatabase()
        {
            using var connection = new SQLiteConnection($"Data Source={DatabasePath}");
            connection.Open();
            
            foreach (string createTableQuery in new string[]
                     {
                         @"
                    CREATE TABLE IF NOT EXISTS Media (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        LocalFilename TEXT NOT NULL,
                        Title TEXT,
                        Description TEXT
                    );",
                         @"
                    CREATE TABLE IF NOT EXISTS Tags (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Category INTEGER,
                        Description TEXT
                    );",
                         @"
                    CREATE TABLE IF NOT EXISTS MediaTags (
                        MediaId INTEGER NOT NULL,
                        TagId INTEGER NOT NULL,
                        PRIMARY KEY (MediaId, TagId),
                        FOREIGN KEY (MediaId) REFERENCES Media(Id),
                        FOREIGN KEY (TagId) REFERENCES Tags(Id)
                    );",
                         @"
                    CREATE TABLE IF NOT EXISTS TagAliases (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        TagId INTEGER NOT NULL,
                        Alias TEXT NOT NULL,
                        FOREIGN KEY (TagId) REFERENCES Tags(Id) ON DELETE CASCADE
                    );"
                     })
            {
                using var command = new SQLiteCommand(createTableQuery, connection);
                command.ExecuteNonQuery();
            }

        }
        
        public static bool ShowConfirmationDialog(string confirmationText)
        {
            // Show a MessageBox with Yes and No options
            var result = MessageBox.Show(
                confirmationText, 
                "Confirmation",  // Title of the dialog
                MessageBoxButton.YesNo,  // Options available to the user
                MessageBoxImage.Question);  // Icon shown in the dialog

            // Return true if the user clicked "Yes" (confirm), false otherwise
            return result == MessageBoxResult.Yes;
        }
    }
}