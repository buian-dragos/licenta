using System.IO;
using SkiaSharp;

public class Image
{
    private SKBitmap _bmp;
    public Image(int w, int h) => _bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
    public void SetPixel(int x, int y, SKColor c) => _bmp.SetPixel(x, y, c);
    public void Store(string path)
    {
        using var img  = SKImage.FromBitmap(_bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        using var fs   = File.OpenWrite(path);
        data.SaveTo(fs);
    }
}

