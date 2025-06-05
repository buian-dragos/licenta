using System.IO;
using SkiaSharp;

public class Image
{
    private SKBitmap bitmap;
    private SKCanvas canvas;
    private SKPaint paint;

    public Image(int width, int height)
    {
        bitmap = new SKBitmap(width, height);
        canvas = new SKCanvas(bitmap);
        paint = new SKPaint();
    }

    public void SetPixel(int x, int y, SKColor color)
    {
        paint.Color = color;
        canvas.DrawPoint(x, y, paint);
    }

    public void Store(string filename)
    {
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        {
            using (var stream = File.OpenWrite(filename))
            {
                data.SaveTo(stream);
            }
        }
    }
}