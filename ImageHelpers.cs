using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using ImageMagick;

namespace Filterizer2
{
    public static class ImageHelpers
    {
        public static Bitmap NewBitmap(this FileInfo fi) {
            Bitmap? bitmap = null;
            try {
                bitmap = new Bitmap(fi.FullName);
            } catch (Exception)
            {
                // use 'MagickImage()' if you want just the 1st frame of an animated image. 
                // 'MagickImageCollection()' returns all frames
                using var magickImages = new MagickImageCollection(fi);
                var ms = new MemoryStream();
                magickImages.Write(ms, magickImages.Count > 1 ? MagickFormat.Gif : MagickFormat.Png);
                bitmap?.Dispose();
                bitmap = new Bitmap(ms);
                // keep MemoryStream from being garbage collected while Bitmap is in use
                bitmap.Tag = ms;
            }
            return bitmap;
        }
        
        public static BitmapImage ConvertBitmapToBitmapImage(Bitmap? bitmap)
        {
            using var memory = new MemoryStream();
            bitmap?.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
            memory.Position = 0;
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            return bitmapImage;
        }
    }
    
}