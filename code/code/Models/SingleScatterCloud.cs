using System;
using System.Numerics;
using code.Utils;

namespace code.Models
{

    public class SingleScatterCloud : Geometry
    {
        private readonly Vector3    _center;
        private readonly Vector3    _axes;
        private readonly float      _baseDensity;
        private readonly Color      _cloudColor;
        private readonly float      _step;
        private readonly Light?     _light;
        private readonly float      _temperature;
        private readonly float      _windSpeed;
        private readonly PerlinNoise _noise = new PerlinNoise(42);

        // GPU accessors
        public Vector3 Center      => _center;
        public Vector3 Axes        => _axes;
        public float   BaseDensity => _baseDensity;
        public Color   CloudColor  => _cloudColor;
        public float   Step        => _step;

        public SingleScatterCloud(
            Vector3 center,
            Vector3 axes,
            float   baseDensity,
            Color   cloudColor,
            Light?  light       = null,
            float   step        = 0.4f,
            float   temperature = 20f,
            float   windSpeed   = 0f
        ) : base(Color.NONE)
        {
            _center      = center;
            _axes        = axes;
            _baseDensity = baseDensity;
            _cloudColor  = cloudColor;
            _light       = light;
            _step        = step;
            _temperature = temperature;
            _windSpeed   = windSpeed;
        }

        public static SingleScatterCloud FromType(
            CloudType type,
            Vector3   center,
            double    humidityPercent,
            double    temperature,
            double    windSpeed,
            Light?    light = null,
            float     step  = 0.4f
        )
        {
            // Base WMO parameters from cloud type
            var (axes, baseDensity, tint) = CloudFactory.GetCloudParameters(type);

            // Temperature - water/ice content
            if (temperature < 0)
            {
                baseDensity *= 0.6f; 
                tint = new Color(
                    tint.Red   * 0.85f,
                    tint.Green * 0.90f,
                    tint.Blue  * 1.10f, 
                    tint.Alpha * 0.6f
                );
            }

            // Wind - stretch the cloud
            float stretch = 1f + 0.02f * (float)windSpeed;  // 2% per m/s
            axes.X *= stretch;
            axes.Z *= stretch;

            // Humidity - final density & alpha
            float hScale = (float)(humidityPercent / 100.0);
            float finalDensity = baseDensity * hScale;
            var finalColor = new Color(
                tint.Red,
                tint.Green,
                tint.Blue,
                Math.Clamp(tint.Alpha * hScale, 0f, 1f)
            );

            return new SingleScatterCloud(
                center, axes, finalDensity, finalColor,
                light, step, (float)temperature, (float)windSpeed
            );
        }

        
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
            // Start from an ellipsoid
            Vector3 lp = (p - _center) / _axes;
            float   r  = lp.Length();
    

            const float outMargin = 0.75f;
            if (r > 1f + outMargin) return 0f;
    
            // Generate layered noise
            float nLarge = _noise.Noise(p.X * 0.05f, p.Y * 0.05f, p.Z * 0.05f);
            float nMed   = _noise.Noise(p.X * 0.15f, p.Y * 0.15f, p.Z * 0.15f);
            float nSmall = _noise.Noise(p.X * 0.30f, p.Y * 0.30f, p.Z * 0.30f);
    
            // Compute your noisy boundary
            float boundary = 1f
                             + 0.3f  * _noise.Noise(lp.X * 2f, lp.Y * 2f, lp.Z * 2f)
                             + 0.15f * _noise.Noise(lp.X * 4f, lp.Y * 4f, lp.Z * 4f);
    
 
            float envelope = (boundary - r) / outMargin;
            // clamp to [0,1]
            if (envelope <= 0f) return 0f;
            if (envelope >  1f) envelope = 1f;
    
            // Blend noise
            float n = 0.6f + 0.35f*nLarge + 0.25f*nMed + 0.15f*nSmall;
            n = MathF.Max(0f, n);
    
            // Inner fade + height fall-off
            float fade   = MathF.Pow(1f - r/boundary, 1.5f);
            float height = MathF.Max(0f, 1f - MathF.Pow(MathF.Max(0f, lp.Y + 0.3f), 2f));
            const float minBase = 0.03f;
    
            float rawDensity = _baseDensity
                               * height
                               * (n * fade + minBase * (1f - fade));
            
            return rawDensity * envelope;
        }


        private float LightTransmittance(Vector3 pos)
        {
            if (_light == null) return 1f;
            Vector3 toL = _light.Position - pos;
            float dist = toL.Length();
            Vector3 dir = Vector3.Normalize(toL);

            float t = 0f, tau = 0f, dt = _step*2f;
            while (t < dist)
            {
                tau += DensityAt(pos + dir*t) * dt;
                if (tau > 10f) return 0f;
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

            if (accum.Alpha <= 0.00001f) return Intersection.NONE;
            accum.Alpha = 1f - Tview; // overall opacity
            return new Intersection(true, true, this, ray, t0, Vector3.Zero, Material, accum);
        }
    }
}
