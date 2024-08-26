using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;

namespace Filterizer2
{
    public class ThumbnailGenerator
    {
        public static string GenerateThumbnail(string mediaFilePath, string thumbsDirectory)
        {
            string fileName = Path.GetFileName(mediaFilePath);
            string thumbnailPath = Path.Combine(thumbsDirectory, fileName);

            if (File.Exists(thumbnailPath))
            {
                return thumbnailPath;
            }

            string extension = Path.GetExtension(mediaFilePath).ToLowerInvariant();

            try
            {
                switch (extension)
                {
                    case ".png":
                    case ".jpg":
                    case ".jpeg":
                    case ".webp":
                        GenerateImageThumbnail(mediaFilePath, thumbnailPath);
                        break;

                    case ".mp4":
                    case ".webm":
                    case ".gif":
                        GenerateVideoOrGifThumbnail(mediaFilePath, thumbnailPath);
                        break;

                    default:
                        throw new NotSupportedException($"Unsupported media format: {extension}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate thumbnail for {mediaFilePath}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return thumbnailPath;
        }

        private static void GenerateImageThumbnail(string imagePath, string thumbnailPath)
        {
            using var image = new Bitmap(imagePath);
            var thumbnail = image.GetThumbnailImage(64, 64, () => false, IntPtr.Zero);
            thumbnail.Save(thumbnailPath, ImageFormat.Png);
        }

        private static void GenerateVideoOrGifThumbnail(string mediaPath, string thumbnailPath)
        {
            using (var mediaPlayer = new Vlc.DotNet.Core.VlcMediaPlayer(new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libvlc"))))
            {
                mediaPlayer.SetMedia(new FileInfo(mediaPath));
                //TODO? mediaPlayer.Position = 0.1f;

                mediaPlayer.Play();

                mediaPlayer.TakeSnapshot(new FileInfo(thumbnailPath), 64, 64);

                mediaPlayer.Stop();
            }
        }

        private static Bitmap ResizeImage(Bitmap imgToResize, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(imgToResize.HorizontalResolution, imgToResize.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(imgToResize, destRect, 0, 0, imgToResize.Width, imgToResize.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }
    }
}