using System.Numerics;
using code.Models;

namespace code.Utils
{
    /// <summary>
    /// Compact presets tuned for a typical 800×600 render.
    /// Axes are already in metres; the widest cloud is ~70 m in model space so
    /// it fits comfortably in camera with a 65° FOV at 60 m distance.
    /// </summary>
    public static class CloudFactory
    {
        public static (Vector3 Axes, float BaseDensity, Color Tint) GetCloudParameters(CloudType type)
        {
            Vector3 axes;      // width, height, depth  (m)
            float   baseDens;  // scattering coeff σ_s (0-1 scaled)
            Color   tint;      // RGB + single-scatter albedo in A

            switch (type)
            {
                /*────────────── Convective / vertical development ─────────────*/
                case CloudType.Cumulus:               // fair-weather puff
                    axes     = new Vector3(35, 15, 25);
                    baseDens = 0.12f;
                    tint     = new Color(1.0f, 1.0f, 1.0f, 0.75f);
                    break;

                case CloudType.Cumulonimbus:          // moderate tower, not full anvil
                    axes     = new Vector3(45, 35, 35);  // keep reference width but add height
                    baseDens = 0.18f;
                    tint     = new Color(0.52f, 0.52f, 0.57f, 0.85f);
                    break;

                /*────────────── Low clouds (sheet-like) ───────────────────────*/
                case CloudType.Stratus:
                    axes     = new Vector3(55, 10, 40);  // thicker than before but modest
                    baseDens = 0.11f;
                    tint     = new Color(0.93f, 0.94f, 0.98f, 0.75f);
                    break;

                case CloudType.Stratocumulus:
                    axes     = new Vector3(45, 14, 35);
                    baseDens = 0.13f;
                    tint     = new Color(0.96f, 0.96f, 1.0f, 0.8f);
                    break;

                case CloudType.Nimbostratus:
                    axes     = new Vector3(60, 20, 45);
                    baseDens = 0.20f;
                    tint     = new Color(0.85f, 0.87f, 0.92f, 0.9f);
                    break;

                /*────────────── Mid-level ─────────────────────────────────────*/
                case CloudType.Altostratus:
                    axes     = new Vector3(60, 12, 45);
                    baseDens = 0.10f;
                    tint     = new Color(0.97f, 0.97f, 1.0f, 0.65f);
                    break;

                case CloudType.Altocumulus:
                    axes     = new Vector3(30, 12, 25);
                    baseDens = 0.09f;
                    tint     = new Color(1.0f, 1.0f, 1.0f, 0.7f);
                    break;

                /*────────────── High clouds ───────────────────────────────────*/
                case CloudType.Cirrus:
                    axes     = new Vector3(60, 6, 45);
                    baseDens = 0.04f;
                    tint     = new Color(0.92f, 0.97f, 1.0f, 0.4f);
                    break;

                case CloudType.Cirrostratus:
                    axes     = new Vector3(70, 8, 50);
                    baseDens = 0.05f;
                    tint     = new Color(0.95f, 0.98f, 1.0f, 0.45f);
                    break;

                case CloudType.Cirrocumulus:
                    axes     = new Vector3(25, 8, 20);
                    baseDens = 0.045f;
                    tint     = new Color(0.95f, 0.99f, 1.0f, 0.45f);
                    break;

                /*────────────── Fallback ─────────────────────────────────────*/
                default:
                    axes     = new Vector3(45, 18, 35);
                    baseDens = 0.12f;
                    tint     = new Color(1.0f, 1.0f, 1.0f, 0.75f);
                    break;
            }

            return (axes, baseDens, tint);
        }
    }
}