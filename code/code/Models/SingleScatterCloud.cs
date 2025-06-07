using System;
using System.Numerics;
using code.Utils;

namespace code.Models
{
    public class SingleScatterCloud : Geometry
    {
        private readonly Vector3 _center;
        private readonly Vector3 _axes;          // Ellipsoid axes (width, height, depth)
        private readonly float   _baseDensity;   // Base σ_s inside the cloud
        private readonly Color   _cloudColor;    // RGB of the cloud (alpha = single-scatter albedo)
        private readonly float   _step;          // Ray-march step size
        private readonly Light   _light;         // Light to shadow against (may be null)
        private readonly PerlinNoise _noise = new PerlinNoise(42);   // Deterministic noise

        /*──────────────────────────────*/
        /*  Constructors                */
        /*──────────────────────────────*/

        // Back-compat: old signature -> no light, default step = 0.4
        public SingleScatterCloud(Vector3 center,
                                  Vector3 axes,
                                  float   baseDensity,
                                  Color   cloudColor,
                                  float   step = 0.4f)
            : this(center, axes, baseDensity, cloudColor, null, step) { }

        // New signature with optional Light for self-shadowing
        public SingleScatterCloud(Vector3 center,
                                  Vector3 axes,
                                  float   baseDensity,
                                  Color   cloudColor,
                                  Light   light,
                                  float   step = 0.4f)
            : base(Color.NONE)
        {
            _center      = center;
            _axes        = axes;
            _baseDensity = baseDensity;
            _cloudColor  = cloudColor;
            _light       = light;
            _step        = step;
        }
        

        private bool EllipsoidRayHit(Line ray, float minDist, float maxDist,
                                     out float tEnter, out float tExit)
        {
            tEnter = tExit = 0f;

            Vector3 invA  = new Vector3(1f / _axes.X, 1f / _axes.Y, 1f / _axes.Z);
            Vector3 oc    = (ray.X0 - _center) * invA;
            Vector3 rd    =  ray.Dx * invA;

            float A = Vector3.Dot(rd, rd);
            float B = 2f * Vector3.Dot(oc, rd);
            float C = Vector3.Dot(oc, oc) - 1f;

            float disc = B * B - 4f * A * C;
            if (disc < 0f) return false;

            float s = MathF.Sqrt(disc);
            float t0 = (-B - s) / (2f * A);
            float t1 = (-B + s) / (2f * A);

            if (t1 < minDist || t0 > maxDist) return false;

            tEnter = MathF.Max(t0, minDist);
            tExit  = MathF.Min(t1, maxDist);
            return tExit > tEnter;
        }
        

        private float DensityAt(Vector3 p)
        {
            Vector3 lp = (p - _center) / _axes;
            float   r  = lp.Length();
            if (r > 1f) return 0f;
            
            float nLarge  = _noise.Noise(p.X * 0.05f, p.Y * 0.05f, p.Z * 0.05f);
            float nMed    = _noise.Noise(p.X * 0.15f, p.Y * 0.15f, p.Z * 0.15f);
            float nSmall  = _noise.Noise(p.X * 0.30f, p.Y * 0.30f, p.Z * 0.30f);

            float boundary = 1f
                           + 0.3f  * _noise.Noise(lp.X * 2f, lp.Y * 2f, lp.Z * 2f)
                           + 0.15f * _noise.Noise(lp.X * 4f, lp.Y * 4f, lp.Z * 4f);

            if (r > boundary) return 0f;

            float n = 0.6f
                    + 0.35f * nLarge
                    + 0.25f * nMed
                    + 0.15f * nSmall;

            n = MathF.Max(0f, n);

            float fade   = MathF.Pow(1f - r / boundary, 0.35f);
            float height = MathF.Max(0f, 1f - MathF.Pow(MathF.Max(0, lp.Y + 0.3f), 2));

            const float minBase = 0.03f;
            return _baseDensity * height * (n * fade + minBase * (1f - fade));
        }
        

        private float LightTransmittance(Vector3 pos)
        {
            if (_light == null) return 1f; // no self-shadow requested

            Vector3 toL   = _light.Position - pos;
            float   dist  = toL.Length();
            Vector3 dir   = Vector3.Normalize(toL);

            float t   = 0f;
            float tau = 0f;
            float dt  = _step * 2f;        // can step coarser than view-ray march

            while (t < dist)
            {
                Vector3 sample = pos + dir * t;
                tau += DensityAt(sample) * dt;

                if (tau > 10f) return 0f;  // nearly opaque, early-out
                t += dt;
            }

            return MathF.Exp(-tau);
        }

        public override Intersection GetIntersection(Line  ray,
                                                     float minDist,
                                                     float maxDist)
        {
            if (!EllipsoidRayHit(ray, minDist, maxDist,
                                 out float t0, out float t1))
                return Intersection.NONE;

            Color accum = Color.NONE;
            float Tview = 1f;
            float t     = t0;

            while (t < t1)
            {
                Vector3 p    = ray.CoordinateToPosition(t);
                float   dens = DensityAt(p);
                float   dt   = _step;
                float   tau  = dens * dt;

                float absorb     = MathF.Exp(-tau);   // Beer–Lambert along view
                float scatterAmt = 1f - absorb;       // portion scattered here

                /* --- self-shadow against the sun --- */
                float Tlight = LightTransmittance(p);

                Color inscatter = _cloudColor
                                * scatterAmt
                                * Tview
                                * Tlight;

                accum += inscatter;
                Tview *= absorb;

                if (Tview < 0.02f) break;            // fully saturated

                t += _step;     
            }

            if (accum.Alpha <= 0.1) return Intersection.NONE;

            accum.Alpha = 1f - Tview;                // overall cloud opacity
            return new Intersection(
                valid:     true,
                visible:   true,
                geometry:  this,
                line:      ray,
                t:         t0,
                normal:    Vector3.Zero,             // not used for volumetric
                material:  Material,
                color:     accum
            );
        }
    }
}
