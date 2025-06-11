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
        private readonly Color   _cloudColor;    // RGB of the cloud (alpha = single‑scatter albedo)
        private readonly float   _step;          // Ray‑march step size
        private readonly Light?  _light;         // Light to shadow against (may be null)
        private readonly PerlinNoise _noise = new PerlinNoise(42);   // Deterministic noise

        #region ── Construction helpers ───────────────────────────────────────────

        public SingleScatterCloud(Vector3 center,
                                  Vector3 axes,
                                  float   baseDensity,
                                  Color   cloudColor,
                                  Light?  light = null,
                                  float   step  = 0.4f)
            : base(Color.NONE)
        {
            _center      = center;
            _axes        = axes;
            _baseDensity = baseDensity;
            _cloudColor  = cloudColor;
            _light       = light;
            _step        = step;
        }

        public static SingleScatterCloud FromType(CloudType type,
                                                  Vector3   center,
                                                  double    humidityPercent, // Added humidityPercent
                                                  Light?    light   = null,
                                                  float     step    = 0.4f)
        {
            // Shape (km → m) and density presets gathered from WMO tech docs + typical render values
            Vector3 axes;     // width, height, depth (m)
            float   baseDensityFromType;  // base scattering coefficient σ_s from type
            Color   tintFromType;     // white-ish varies toward blue/gray, alpha is base albedo

            switch (type)
            {
                case CloudType.Cumulonimbus:    // towering thundercloud
                    axes    = new Vector3(45, 18, 35);
                    baseDensityFromType = 0.20f;
                    tintFromType    = new Color(0.9, 0.9, 0.95, 0.8); // slight bluish gray
                    break;

                case CloudType.Cumulus:         // puffy fair‑weather
                    axes    = new Vector3(35, 15, 25);
                    baseDensityFromType = 0.12f;
                    tintFromType    = new Color(1.0, 1.0, 1.0, 0.7);
                    break;

                case CloudType.Stratus:         // flat low‑level sheet
                    axes    = new Vector3(60, 8, 40);
                    baseDensityFromType = 0.10f;
                    tintFromType    = new Color(0.95, 0.95, 1.0, 0.7);
                    break;

                case CloudType.Stratocumulus:
                    axes    = new Vector3(55, 12, 35);
                    baseDensityFromType = 0.13f;
                    tintFromType    = new Color(0.96, 0.96, 1.0, 0.75);
                    break;

                case CloudType.Nimbostratus:    // rain‑bearing layer
                    axes    = new Vector3(65, 15, 45);
                    baseDensityFromType = 0.18f;
                    tintFromType    = new Color(0.85, 0.85, 0.9, 0.85); // darker gray
                    break;

                case CloudType.Altostratus:
                    axes    = new Vector3(70, 10, 50);
                    baseDensityFromType = 0.09f;
                    tintFromType    = new Color(0.97, 0.97, 1.0, 0.65);
                    break;

                case CloudType.Altocumulus:
                    axes    = new Vector3(30, 10, 25);
                    baseDensityFromType = 0.08f;
                    tintFromType    = new Color(1.0, 1.0, 1.0, 0.6);
                    break;

                case CloudType.Cirrostratus:
                    axes    = new Vector3(90, 6, 60);
                    baseDensityFromType = 0.04f;
                    tintFromType    = new Color(0.95, 0.97, 1.0, 0.4); // wispy
                    break;

                case CloudType.Cirrocumulus:
                    axes    = new Vector3(20, 6, 15);
                    baseDensityFromType = 0.035f;
                    tintFromType    = new Color(0.95, 0.98, 1.0, 0.35);
                    break;

                case CloudType.Cirrus:          // high thin feathers
                default:
                    axes    = new Vector3(80, 5, 50);
                    baseDensityFromType = 0.03f;
                    tintFromType    = new Color(0.92, 0.97, 1.0, 0.3);
                    break;
            }

            float humidityScale = (float)(humidityPercent / 100.0);
            float finalDensity = baseDensityFromType * humidityScale;
            Color finalCloudColor = new Color(
                tintFromType.Red,
                tintFromType.Green,
                tintFromType.Blue,
                Math.Clamp((float)tintFromType.Alpha * humidityScale, 0.0f, 1.0f) // Modulate albedo and clamp
            );

            return new SingleScatterCloud(center, axes, finalDensity, finalCloudColor, light, step);
        }

        #endregion

        /* ────────────────────────────────────────────────────────────────── */
        /*  Core implementation (unchanged)                                 */
        /* ────────────────────────────────────────────────────────────────── */

        private bool EllipsoidRayHit(Line ray, float minDist, float maxDist,
                                     out float tEnter, out float tExit)
        {
            tEnter = tExit = 0f;
            Vector3 invA = new Vector3(1f/_axes.X, 1f/_axes.Y, 1f/_axes.Z);
            Vector3 oc   = (ray.X0 - _center) * invA;
            Vector3 rd   =  ray.Dx * invA;
            float A = Vector3.Dot(rd, rd);
            float B = 2f * Vector3.Dot(oc, rd);
            float C = Vector3.Dot(oc, oc) - 1f;
            float disc = B*B - 4f*A*C;
            if (disc < 0f) return false;
            float s  = MathF.Sqrt(disc);
            float t0 = (-B - s) / (2f*A);
            float t1 = (-B + s) / (2f*A);
            if (t1 < minDist || t0 > maxDist) return false;
            tEnter = MathF.Max(t0, minDist);
            tExit  = MathF.Min(t1, maxDist);
            return tExit > tEnter;
        }

        private float DensityAt(Vector3 p)
        {
            Vector3 lp = (p - _center) / _axes;
            float r = lp.Length();
            if (r > 1f) return 0f;

            // Layered noise for billowy look
            float nLarge = _noise.Noise(p.X*0.05f, p.Y*0.05f, p.Z*0.05f);
            float nMed   = _noise.Noise(p.X*0.15f, p.Y*0.15f, p.Z*0.15f);
            float nSmall = _noise.Noise(p.X*0.30f, p.Y*0.30f, p.Z*0.30f);

            float boundary = 1f
                           + 0.3f  * _noise.Noise(lp.X*2f, lp.Y*2f, lp.Z*2f)
                           + 0.15f * _noise.Noise(lp.X*4f, lp.Y*4f, lp.Z*4f);
            if (r > boundary) return 0f;

            float n = 0.6f + 0.35f*nLarge + 0.25f*nMed + 0.15f*nSmall;
            n = MathF.Max(0f, n);

            float fade   = MathF.Pow(1f - r/boundary, 0.35f);
            float height = MathF.Max(0f, 1f - MathF.Pow(MathF.Max(0, lp.Y + 0.3f), 2));
            const float minBase = 0.03f;
            return _baseDensity * height * (n*fade + minBase*(1f - fade));
        }

        private float LightTransmittance(Vector3 pos)
        {
            if (_light == null) return 1f;
            Vector3 toL = _light.Position - pos;
            float dist = toL.Length();
            Vector3 dir = Vector3.Normalize(toL);

            float t = 0f, tau = 0f, dt = _step*2f; // faster march for shadow
            while (t < dist)
            {
                tau += DensityAt(pos + dir*t) * dt;
                if (tau > 10f) return 0f; // opaque
                t += dt;
            }
            return MathF.Exp(-tau);
        }

        public override Intersection GetIntersection(Line ray, float minDist, float maxDist)
        {
            if (!EllipsoidRayHit(ray, minDist, maxDist, out float t0, out float t1))
                return Intersection.NONE;

            Color accum = Color.NONE;
            float Tview = 1f;
            for (float t = t0; t < t1; t += _step)
            {
                Vector3 p   = ray.CoordinateToPosition(t);
                float dens  = DensityAt(p);
                float tau   = dens * _step;
                float absorb = MathF.Exp(-tau);
                float scatter = 1f - absorb;
                float Tlight = LightTransmittance(p);

                Color inscatter = _cloudColor * scatter * Tview * Tlight;
                accum += inscatter;
                Tview *= absorb;
                if (Tview < 0.02f) break;
            }

            if (accum.Alpha <= 0.1f) return Intersection.NONE;
            accum.Alpha = 1f - Tview; // overall opacity
            return new Intersection(true, true, this, ray, t0, Vector3.Zero, Material, accum);
        }
    }
}
