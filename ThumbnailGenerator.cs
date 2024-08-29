using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;

namespace Filterizer2
{
    public class ThumbnailGenerator
    {
        public static string GenerateOrGetThumbnail(string mediaFilePath)
        {
            string fileName = Path.GetFileName(mediaFilePath);
            //Save all as jpg
            string thumbnailPath = Path.ChangeExtension(Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Thumbs"), fileName), ".jpg");

            
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
                    case ".gif":
                        GenerateImageThumbnail(mediaFilePath, thumbnailPath);
                        break;

                    case ".mp4":
                    case ".webm":
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
            thumbnail.Save(thumbnailPath, ImageFormat.Jpeg);
        }

        private static void GenerateVideoOrGifThumbnail(string mediaPath, string thumbnailPath)
        {
            new FFMPEG().GetThumbnail(mediaPath, thumbnailPath);
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