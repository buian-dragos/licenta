using System.Numerics;

namespace code.Models
{
    public class Light
    {
        public Vector3 Position { get; set; }
        public Color Ambient { get; set; }
        public Color Diffuse { get; set; }
        public Color Specular { get; set; }
        public double Intensity { get; set; }

        public Light()
        {
            Position = new Vector3();
            Ambient = new Color();
            Diffuse = new Color();
            Specular = new Color();
            Intensity = 0;
        }

        public Light(Vector3 position, Color ambient, Color diffuse, Color specular, double intensity)
        {
            Position = position;
            Ambient = new Color(ambient);
            Diffuse = new Color(diffuse);
            Specular = new Color(specular);
            Intensity = intensity;
        }
    }
}