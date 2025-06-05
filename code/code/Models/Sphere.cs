using System;
using System.Numerics;

namespace code.Services
{
    public class Sphere : Ellipsoid
    {
        public Sphere(Vector3 center, float radius, Material material, Color color) : base(center, new Vector3(1f, 1f, 1f), radius, material, color)
        {
        }
        public Sphere(Vector3 center, float radius, Color color) : base(center, new Vector3(1, 1, 1), radius, color)
        {
        }
    }
}