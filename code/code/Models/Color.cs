using System;
using SkiaSharp;

namespace code.Models
{
    public class Color
    {
        public static readonly Color NONE = new Color(0.0f, 0.0f, 0.0f, 0.0f);

        public float Red { get; set; }
        public float Green { get; set; }
        public float Blue { get; set; }
        public float Alpha { get; set; }

        public Color()
        {
            Red = 0;
            Green = 0;
            Blue = 0;
            Alpha = 0;
        }

        public Color(float red, float green, float blue, float alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public Color(Color c)
        {
            Red = c.Red;
            Green = c.Green;
            Blue = c.Blue;
            Alpha = c.Alpha;
        }
        
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        private static byte ToSrgb8(float linear)
        {
            linear = Clamp01(linear);
            // simple 1/2.2 gamma — good enough for UI bitmaps
            float srgb = MathF.Pow(linear, 1f / 2.2f);
            return (byte)MathF.Round(srgb * 255f);
        }
        
        
        // Convert to SkiaSharp's SKColor

        public SKColor ToSKColor()
            => new SKColor(ToSrgb8(Red), ToSrgb8(Green), ToSrgb8(Blue), ToSrgb8(Alpha));
        public static Color operator +(Color a, Color b)
        {
            return new Color(a.Red + b.Red, a.Green + b.Green, a.Blue + b.Blue, a.Alpha + b.Alpha);
        }

        public static Color operator -(Color a, Color b)
        {
            return new Color(a.Red - b.Red, a.Green - b.Green, a.Blue - b.Blue, a.Alpha - b.Alpha);
        }

        public static Color operator *(Color a, Color b)
        {
            return new Color(a.Red * b.Red, a.Green * b.Green, a.Blue * b.Blue, a.Alpha * b.Alpha);
        }

        public static Color operator /(Color a, Color b)
        {
            return new Color(a.Red / b.Red, a.Green / b.Green, a.Blue / b.Blue, a.Alpha / b.Alpha);
        }

        public static Color operator *(Color c, float k)
        {
            return new Color(c.Red * k, c.Green * k, c.Blue * k, c.Alpha * k);
        }

        public static Color operator /(Color c, float k)
        {
            return new Color(c.Red / k, c.Green / k, c.Blue / k, c.Alpha / k);
        }
    }
}
