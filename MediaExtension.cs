using System.IO;

namespace Filterizer2
{
	public enum MediaExtension
	{
		Unsupported,
		Gif,
		Png,
		Jpg,
		Webp,
		Webm,
		Mp4
		
	}

	public static class MediaExtensionTools
	{
		public static MediaExtension MediaExtension(this string fileName)
		{
			var extension = Path.GetExtension(fileName).ToLower();
			return extension switch
			{
				".png" => Filterizer2.MediaExtension.Png,
				".jpg" => Filterizer2.MediaExtension.Jpg,
				".webp" => Filterizer2.MediaExtension.Webp,
				".gif" => Filterizer2.MediaExtension.Gif,
				".webm" => Filterizer2.MediaExtension.Webm,
				".mp4" => Filterizer2.MediaExtension.Mp4,
				_ => Filterizer2.MediaExtension.Unsupported
			};
		}

		public static bool IsSeekable(this MediaExtension extension)
		{
			return extension is Filterizer2.MediaExtension.Mp4 or Filterizer2.MediaExtension.Webm;
		}

		public static bool IsAnimated(this MediaExtension extension)
		{
			return extension is Filterizer2.MediaExtension.Mp4 or Filterizer2.MediaExtension.Webm or Filterizer2.MediaExtension.Gif;
		}
	}
}