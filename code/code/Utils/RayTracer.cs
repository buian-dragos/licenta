using System;
using SkiaSharp;
using System.Numerics;
using code.Models;
// using Color = System.Drawing.Color;


namespace code.Utils
{
    class RayTracer : IRenderer
    {
        private Geometry[] geometries;
        private Light[] lights;

        public RayTracer(Geometry[] geometries, Light[] lights)
        {
            this.geometries = geometries;
            this.lights = lights;
        }

        private float ImageToViewPlane(int n, int imgSize, float viewPlaneSize)
        {
            return -n * viewPlaneSize / imgSize + viewPlaneSize / 2;
        }

        private Intersection FindFirstIntersection(Line ray, float minDist, float maxDist)
        {
            var intersection = Intersection.NONE;

            foreach (var geometry in geometries)
            {
                var intr = geometry.GetIntersection(ray, minDist, maxDist);

                if (!intr.Valid || !intr.Visible) continue;

                if (!intersection.Valid || !intersection.Visible)
                {
                    intersection = intr;
                }
                else if (intr.T < intersection.T)
                {
                    intersection = intr;
                }
            }

            return intersection;
        }

        private bool IsLit(Vector3 point, Light light, Geometry currentGeometry = null)
        {
            // Calculate the direction and distance to the light
            Vector3 lightDir = (light.Position - point);
            float maxDist = lightDir.Length();
            lightDir = Vector3.Normalize(lightDir);

            // Small offset to avoid self-intersection
            float bias = 0.001f;
            Vector3 shadowOrigin = point + lightDir * bias;

            // Shadow ray from the slightly offset origin
            Line shadowRay = new Line(shadowOrigin, shadowOrigin + lightDir * maxDist);

            // Check if any geometry obstructs the light
            foreach (var geometry in geometries)
            {
                // Skip self-intersection for clouds since they're volumetric
                if (geometry == currentGeometry && geometry is SingleScatterCloud)
                    continue;

                var shadowIntersection = geometry.GetIntersection(shadowRay, bias, maxDist);

                if (shadowIntersection.Valid && shadowIntersection.Visible)
                {
                    return false;  // Light is obstructed, point is in shadow
                }
            }

            return true;  // No obstructions, point is lit
        }

        private Color PhongReflection(Intersection intersection, Vector3 cameraPos)
        {
            Color pixelColor = intersection.Material.Ambient * intersection.Color;

            foreach (var light in lights)
            {
                pixelColor += light.Ambient * intersection.Material.Ambient * light.Intensity;

                if (IsLit(intersection.Position, light))
                {
                    Vector3 lightDir = Vector3.Normalize(light.Position - intersection.Position);
                    float diffuseFactor = Math.Max(0, Vector3.Dot(intersection.Normal, lightDir));
                    pixelColor += intersection.Material.Diffuse * light.Diffuse * diffuseFactor * light.Intensity;

                    // Phong reflection model
                    Vector3 viewDir = Vector3.Normalize(cameraPos - intersection.Position);
                    float ndotl = Vector3.Dot(intersection.Normal, lightDir);
                    Vector3 reflectDir = (intersection.Normal * (2 * ndotl)) - lightDir;
                    float specularDot = Math.Max(0, Vector3.Dot(viewDir, reflectDir));
                    float specularFactor = (float)Math.Pow(specularDot, intersection.Material.Shininess);
                    pixelColor += intersection.Material.Specular * light.Specular * specularFactor * light.Intensity;
                }
            }
            
            return pixelColor;
        }

        public void Render(Camera camera, int width, int height, string filename)
        {
            camera.Normalize();
            
            var background = new Color(0.05f, 0.05f, 0.05f, 1.0f); // Updated background color to #2E2E2E
            // var background = new Color(0.529f, 0.808f, 0.922f, 1.0f); // Sky blue background
            var image = new Image(width, height);

            double pixelWidth = camera.ViewPlaneWidth / width;
            double pixelHeight = camera.ViewPlaneHeight / height;

            Vector3 tempCamera = camera.Position + camera.Direction * camera.ViewPlaneDistance;
            // Vector3 right = (camera.Direction ^ camera.Up).Normalize();
            Vector3 right = Vector3.Normalize(Vector3.Cross(camera.Direction, camera.Up));
            Vector3 up = Vector3.Normalize(camera.Up);

            for (var i = 0; i < width; i++)
            {
                for (var j = 0; j < height; j++)
                {
                    float x = -ImageToViewPlane(i, width, camera.ViewPlaneWidth);
                    float y = ImageToViewPlane(j, height, camera.ViewPlaneHeight);

                    Vector3 pixelPosition = tempCamera + (right * x) + (up * y);
                    Line ray = new Line(camera.Position, pixelPosition);

                    Intersection intersection = FindFirstIntersection(ray, camera.FrontPlaneDistance, camera.BackPlaneDistance);
                    Color pixelColor;

                    if (intersection.Valid && intersection.Visible)
                    {
                        if (intersection.Geometry is SingleScatterCloud)
                        {
                            Color cloudColor = intersection.Color; // .RGB is pre-multiplied, .Alpha is overall opacity
                            float overallCloudOpacity = cloudColor.Alpha;

                            // Blend pre-multiplied cloud color with background:
                            // FinalColor.RGB = PremultipliedCloud.RGB + Background.RGB * (1 - OverallCloudOpacity)
                            // The Color class operators achieve this:
                            pixelColor = cloudColor + (background * (1.0f - overallCloudOpacity));

                            // The pixelColor.Alpha resulting from the above operation is already 1.0f.
                            // Explicitly setting it ensures the output image pixel is fully opaque.
                            pixelColor.Alpha = 1.0f; 
                        }
                        else
                        {
                            pixelColor = PhongReflection(intersection, camera.Position);
                        }
                    }
                    else
                    {
                        pixelColor = background;
                    }

                    var skColor = pixelColor.ToSKColor();
                    image.SetPixel(i, j, skColor);
                }
            }

            image.Store(filename);
        }
    }
}

