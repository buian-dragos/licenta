using System.Numerics;

namespace code.Services
{
    public class Line
    {
        public Vector3 X0 { get; set; }
        public Vector3 Dx { get; set; }

        public Line()
        {
            X0 = new Vector3(0.0f, 0.0f, 0.0f);
            Dx = new Vector3(1.0f, 0.0f, 0.0f);
        }

        public Line(Vector3 x0, Vector3 x1)
        {
            X0 = x0;
            Dx = x1 - x0;
            Dx = Vector3.Normalize(Dx);
        }

        public Vector3 CoordinateToPosition(float t)
        {
            return Dx * t + X0;
        }
    }
}