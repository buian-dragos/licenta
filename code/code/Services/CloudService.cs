using System;
using System.IO;
using System.Threading.Tasks;
using System.Numerics;
using System.Collections.Generic;
using code.Models;
using code.Repositories;
using code.Utils;

namespace code.Services
{
    public class CloudService : ICloudService
    {
        private readonly ICloudRepository _cloudRepository;
        private readonly string _framesDirectory = Path.Combine("..", "..", "..", "frames");

        public CloudService(ICloudRepository cloudRepository)
        {
            _cloudRepository = cloudRepository;
        }

        public Task<IEnumerable<Cloud>> GetAllCloudsAsync()
            => Task.FromResult(_cloudRepository.GetAll());

        public Task<Cloud?> GetCloudByIdAsync(int id)
            => Task.FromResult(_cloudRepository.GetById(id));

        public Task AddCloudAsync(Cloud cloud)
        {
            _cloudRepository.Add(cloud);
            return Task.CompletedTask;
        }

        public Task UpdateCloudAsync(Cloud cloud)
        {
            _cloudRepository.Update(cloud);
            return Task.CompletedTask;
        }

        public Task DeleteCloudAsync(int id)
        {
            _cloudRepository.Delete(id);
            return Task.CompletedTask;
        }

        public async Task<string[]> RenderCloudAnimationAsync(Cloud cloud, int nFrames = 36, int width = 800, int height = 600, bool noLights = false)
        {
            CleanUpOldFrames();

            // Position the cloud center at its altitude
            var cloudCenter = new Vector3(0, 0, 0);

            // Create lights
            var lights = noLights ? Array.Empty<Light>() : CreateLights();

            // Generate geometries based on cloud type and rendering preset
            Func<Light?, Geometry> geometryFactory = light => // Light can be null for noLights case
            {
                // Use SingleScatterCloud.FromType, passing humidity
                return SingleScatterCloud.FromType(cloud.Type, cloudCenter, cloud.Humidity, light);
            };

            // Setup ray tracer
            var geometries = new List<Geometry>();
            if (noLights)
            {
                geometries.Add(geometryFactory(null)); // Pass null light for noLights
            }
            else
            {
                foreach (var light in lights)
                    geometries.Add(geometryFactory(light));
            }

            var rayTracer = new RayTracer(geometries.ToArray(), lights);

            // Camera parameters
            var up = Vector3.UnitY;
            float cameraDist = 60.0f;
            float angleStep = 360.0f / nFrames;

            var tasks = new List<Task>();
            var filenames = new string[nFrames];

            for (int i = 0; i < nFrames; i++)
            {
                int idx = i;
                filenames[i] = Path.Combine(_framesDirectory, $"cloud_{idx + 1:000}.png");

                tasks.Add(Task.Run(() =>
                {
                    float angleRad = (float)(angleStep * idx * Math.PI / 180.0);
                    float camX = (float)Math.Cos(angleRad) * cameraDist;
                    float camZ = (float)Math.Sin(angleRad) * cameraDist + cloudCenter.Z;

                    // Determine camera vertical position based on user selection
                    float camY = cloudCenter.Y;
                    switch (cloud.CameraPosition)
                    {
                        case CameraPosition.GroundLevel:
                            camY = 0;
                            break;
                        case CameraPosition.CloudLevel:
                            camY = cloudCenter.Y;
                            break;
                        case CameraPosition.AboveClouds:
                            camY = cloudCenter.Z + 20;
                            break;
                    }

                    var camPos = new Vector3(camX, camY, camZ);
                    var dir = Vector3.Normalize(cloudCenter - camPos);

                    var camera = new Camera(
                        camPos,
                        dir,
                        up,
                        65.0f,
                        width * 0.2f,
                        height * 0.2f,
                        0.0f,
                        200.0f
                    );

                    rayTracer.Render(camera, width, height, filenames[idx]);
                    Console.WriteLine($"Frame {idx + 1}/{nFrames} completed");
                }));
            }

            await Task.WhenAll(tasks);
            Console.WriteLine("Rendering complete.");

            // Use first frame as preview
            cloud.PreviewImagePath = filenames[0];
            await UpdateCloudAsync(cloud);

            return filenames;
        }

        public async Task<string> GeneratePreviewAsync(Cloud cloud, int width = 800, int height = 600)
        {
            // Position the cloud center at its altitude
            var cloudCenter = new Vector3(0, 0, (float)cloud.Altitude);

            // Build single-scatter geometry for preview using FromType, without any light
            var geometry = SingleScatterCloud.FromType(cloud.Type, cloudCenter, cloud.Humidity, light: null);

            // Setup ray tracer for preview with no lights
            var rayTracer = new RayTracer(new Geometry[] { geometry }, Array.Empty<Light>());

            // Use a fixed camera angle (first frame)
            var up = Vector3.UnitY;
            float cameraDist = 60.0f;
            var camPos = new Vector3(cameraDist, 0, cloudCenter.Z);
            var dir = Vector3.Normalize(cloudCenter - camPos);

            var camera = new Camera(
                camPos,
                dir,
                up,
                65.0f,
                width * 0.2f,
                height * 0.2f,
                0.0f,
                200.0f
            );

            // Prepare preview directory
            var previewDir = Path.Combine("..", "..", "..", "preview");
            if (!Directory.Exists(previewDir))
            {
                Directory.CreateDirectory(previewDir);
            }
            else
            {
                // Clean up old preview file if it exists
                var oldPreviewFile = Path.Combine(previewDir, "preview.png");
                if (File.Exists(oldPreviewFile))
                {
                    File.Delete(oldPreviewFile);
                }
            }
            
            // Render one frame without lights directly to preview directory
            var previewFilePath = Path.Combine(previewDir, "preview.png");
            rayTracer.Render(camera, width, height, previewFilePath);

            // Update cloud preview path
            cloud.PreviewImagePath = previewFilePath;
            await UpdateCloudAsync(cloud);
            return previewFilePath;
        }

        private void CleanUpOldFrames()
        {
            if (Directory.Exists(_framesDirectory))
                Directory.Delete(_framesDirectory, recursive: true);

            Directory.CreateDirectory(_framesDirectory);
        }

        private Light[] CreateLights()
            => new[]
            {
                new Light(
                    new Vector3(50, 80, 100),
                    new Color(1f, 1f, 1f, 1f),
                    new Color(1f, 1f, 1f, 1f),
                    new Color(1f, 1f, 1f, 1f),
                    10.0f
                )
            };
    }
}
