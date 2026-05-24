using System.Windows.Media;
using System.IO;

namespace AetherBar.Core.Media;

public static class DominantColorExtractor
{
    public static Color ExtractFromBytes(byte[] imageData, int sampleCount = 1000)
    {
        if (imageData == null || imageData.Length == 0)
            return Colors.Transparent;

        try
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            using (var ms = new MemoryStream(imageData))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
            }

            return ExtractFromBitmap(bitmap, sampleCount);
        }
        catch
        {
            return Colors.Transparent;
        }
    }

    public static Color ExtractFromBitmap(System.Windows.Media.Imaging.BitmapSource bitmap, int sampleCount = 1000)
    {
        if (bitmap == null)
            return Colors.Transparent;

        int stride = (int)(bitmap.PixelWidth * (bitmap.Format.BitsPerPixel / 8));
        byte[] pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        var rValues = new List<int>();
        var gValues = new List<int>();
        var bValues = new List<int>();

        int bytesPerPixel = bitmap.Format.BitsPerPixel / 8;
        int pixelCount = pixels.Length / bytesPerPixel;
        int step = Math.Max(1, pixelCount / sampleCount);

        for (int i = 0; i < pixels.Length && i / bytesPerPixel < pixelCount; i += bytesPerPixel * step)
        {
            if (i + 2 < pixels.Length)
            {
                bValues.Add(pixels[i]);
                gValues.Add(pixels[i + 1]);
                rValues.Add(pixels[i + 2]);
            }
        }

        if (rValues.Count == 0)
            return Colors.Transparent;

        int rMedian = Median(rValues);
        int gMedian = Median(gValues);
        int bMedian = Median(bValues);

        return Color.FromRgb((byte)rMedian, (byte)gMedian, (byte)bMedian);
    }

    private static int Median(List<int> values)
    {
        values.Sort();
        return values[values.Count / 2];
    }
}
