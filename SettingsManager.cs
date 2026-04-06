using System.IO;
using System.Text.Json;

namespace Filterizer2
{
	public static class SettingsManager
	{
		private static readonly string FilePath =
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
				"Filterizer2",
				"settings.json");
		
		public static AppSettings Settings { get; set; } = new();

		public static int ThemeIndex => Settings.ThemeIndex;
		public static double WindowWidth => Settings.WindowWidth;
		public static double WindowHeight => Settings.WindowHeight;

		public static void Load()
		{
			try
			{
				if (!File.Exists(FilePath))
				{
					CreateDefaultSettingsFile();
				}

				var json = File.ReadAllText(FilePath);
				Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
			}
			catch
			{
				// If file is corrupt, reset it
				CreateDefaultSettingsFile();
				Load();
			}
		}
		
		private static void CreateDefaultSettingsFile()
		{
			Settings = new AppSettings();

			var directory = Path.GetDirectoryName(FilePath);
			if (!Directory.Exists(directory))
				Directory.CreateDirectory(directory!);

			Save();
		}

		public static void Save()
		{
			var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions
			{
				WriteIndented = true
			});

			File.WriteAllText(FilePath, json);
		}

		public static void SetTheme(int index)
		{
			Settings.ThemeIndex = index;
		}

		public static void SetWindowWidth(double width)
		{
			Settings.WindowWidth = width;
		}

		public static void SetWindowHeight(double height)
		{
			Settings.WindowHeight = height;
		}
	}

	public class AppSettings
	{
		public int ThemeIndex { get; set; } = 0;

		// future-proofing
		public double WindowWidth { get; set; } = 1200;
		public double WindowHeight { get; set; } = 675;
	}
}