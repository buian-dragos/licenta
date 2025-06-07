using System;
using System.Numerics;


namespace code.Models
{
    public class Ellipsoid : Geometry
    {
        private Vector3 Center { get; }
        private Vector3 SemiAxesLength { get; }
        private float Radius { get; }
        
        
        public Ellipsoid(Vector3 center, Vector3 semiAxesLength, float radius, Material material, Color color) : base(material, color)
        {
            Center = center;
            SemiAxesLength = semiAxesLength;
            Radius = radius;
        }

        public Ellipsoid(Vector3 center, Vector3 semiAxesLength, float radius, Color color) : base(color)
        {
            Center = center;
            SemiAxesLength = semiAxesLength;
            Radius = radius;
        }

    public override Intersection GetIntersection(Line line, float minDist, float maxDist)
    {
        Vector3 translatedOrigin = line.X0 - Center;
        
        float a = SemiAxesLength.X;
        float b = SemiAxesLength.Y;
        float c = SemiAxesLength.Z;
        
        Vector3 D = line.Dx;

        float A = (D.X * D.X) / (a * a) + (D.Y * D.Y) / (b * b) + (D.Z * D.Z) / (c * c);
        float B = 2 * ((translatedOrigin.X * D.X) / (a * a) +
                        (translatedOrigin.Y * D.Y) / (b * b) +
                        (translatedOrigin.Z * D.Z) / (c * c));
        float C = (translatedOrigin.X * translatedOrigin.X) / (a * a) +
                   (translatedOrigin.Y * translatedOrigin.Y) / (b * b) +
                   (translatedOrigin.Z * translatedOrigin.Z) / (c * c) - Radius*Radius;

        float discriminant = B * B - 4 * A * C;

        if (discriminant < 0)
        {
            return Intersection.NONE;
        }

        float sqrtDiscriminant = (float)Math.Sqrt(discriminant);
        float t1 = (-B - sqrtDiscriminant) / (2 * A);
        float t2 = (-B + sqrtDiscriminant) / (2 * A);

        float t = float.MaxValue;

        if (t1 >= minDist && t1 <= maxDist)
            t = t1;
        if (t2 >= minDist && t2 <= maxDist && t2 < t)
            t = t2;

        if (t == float.MaxValue)
        {
            return Intersection.NONE;
        }

        Vector3 intersectionPoint = line.CoordinateToPosition(t);
        

        Vector3 normal = new Vector3(
            2f * (intersectionPoint.X - Center.X) / (a * a),
            2f * (intersectionPoint.Y - Center.Y) / (b * b),
            2f * (intersectionPoint.Z - Center.Z) / (c * c)
        );

        normal = Vector3.Normalize(normal);

        
        
        return new Intersection(
            true, true, this, line, t, normal, Material, Color
        );
    }

    }
}
