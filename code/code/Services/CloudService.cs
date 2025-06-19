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
    public class CloudService(ICloudRepository cloudRepository) : ICloudService
    {
        public Task<IEnumerable<Cloud>> GetAllCloudsAsync()
            => Task.FromResult(cloudRepository.GetAll());

        public Task<Cloud?> GetCloudByIdAsync(int id)
            => Task.FromResult(cloudRepository.GetById(id));

        public Task AddCloudAsync(Cloud cloud)
        {
            cloudRepository.Add(cloud);
            return Task.CompletedTask;
        }

        public Task UpdateCloudAsync(Cloud cloud)
        {
            cloudRepository.Update(cloud);
            return Task.CompletedTask;
        }

        public Task DeleteCloudAsync(int id)
        {
            cloudRepository.Delete(id);
            return Task.CompletedTask;
        }

        private static void CleanUpCloudFramesDirectory(Cloud? cloudWithStoragePath)
        {
            if (cloudWithStoragePath == null || string.IsNullOrEmpty(cloudWithStoragePath.StoragePath))
            {
                Console.WriteLine($"Warning: Cannot cleanup frames directory for cloud {cloudWithStoragePath?.Id}, storage path is unknown.");
                return;
            }
            string cloudFramesPath = Path.Combine(cloudWithStoragePath.StoragePath, "frames");
            if (Directory.Exists(cloudFramesPath))
            {
                Directory.Delete(cloudFramesPath, recursive: true);
            }
            Directory.CreateDirectory(cloudFramesPath);
        }

        public async Task<string[]> RenderCloudAnimationAsync(Cloud cloud, int nFrames = 36, int width = 800, int height = 600, bool noLights = false) {
            var persistedCloud = await GetCloudByIdAsync(cloud.Id);
            if (persistedCloud == null || string.IsNullOrEmpty(persistedCloud.StoragePath))
            {
                throw new InvalidOperationException($"Cloud with ID {cloud.Id} not found in repository or has no storage path.");
            }

            string cloudFramesPath = Path.Combine(persistedCloud.StoragePath, "frames");
            CleanUpCloudFramesDirectory(persistedCloud);

            var cloudCenter = new Vector3(0, 0, 0);
            var lights = noLights ? Array.Empty<Light>() : CreateLights();

            Func<Light?, Geometry> geometryFactory = light =>
                SingleScatterCloud.FromType(cloud.Type, cloudCenter, cloud.Humidity,cloud.Temperature,cloud.WindSpeed, light);

            var geometries = new List<Geometry>();
            if (noLights)
            {
                geometries.Add(geometryFactory(null));
            }
            else
            {
                foreach (var light in lights) geometries.Add(geometryFactory(light));
            }

            IRenderer localRenderer;
            if (cloud.RenderEngine == RenderEngineType.GPU)
            {
                Console.WriteLine($"[CloudService] Creating OpenGLRenderer for cloud {cloud.Id} in RenderCloudAnimationAsync.");
                localRenderer = new OpenGLRenderer(geometries.ToArray(), lights);
                Console.WriteLine($"[CloudService] OpenGLRenderer created for cloud {cloud.Id}.");
            }
            else // Default to CPU
            {
                Console.WriteLine($"[CloudService] Creating RayTracer for cloud {cloud.Id} in RenderCloudAnimationAsync.");
                localRenderer = new RayTracer(geometries.ToArray(), lights);
                Console.WriteLine($"[CloudService] RayTracer created for cloud {cloud.Id}.");
            }

            var up = Vector3.UnitY;
            float cameraDist = 60.0f;
            float angleStep = 360.0f / nFrames;
            var filenames = new string[nFrames];

            if (cloud.RenderEngine == RenderEngineType.GPU)
            {
                for (int i = 0; i < nFrames; i++)
                {
                    filenames[i] = Path.Combine(cloudFramesPath, $"cloud_frame_{i + 1:000}.png");

                    float angleRad = (float)(angleStep * i * Math.PI / 180.0);
                    float camX = (float)Math.Cos(angleRad) * cameraDist;
                    float camZ = (float)Math.Sin(angleRad) * cameraDist + cloudCenter.Z;
                    float camY = 0;

                    var camPos = new Vector3(camX, camY, camZ);
                    var dir = Vector3.Normalize(new Vector3(cloudCenter.X, cloudCenter.Y, cloudCenter.Z) - camPos);

                    var camera = new Camera(
                        camPos, dir, up, 65.0f,
                        width * 0.2f, height * 0.2f,
                        0.1f, 200.0f
                    );

                    Console.WriteLine($"[CloudService] Rendering (GPU) frame {i + 1}/{nFrames}, cloud {cloud.Id}. Output: {filenames[i]}");
                    localRenderer.Render(camera, width, height, filenames[i]);
                    Console.WriteLine($"Frame {i + 1}/{nFrames} for cloud {cloud.Id} completed: {filenames[i]}");
                }
            }
            else
            {
                // Parallel rendering for CPU-based renderer
                var tasks = new List<Task>();
                for (int i = 0; i < nFrames; i++)
                {
                    int idx = i;
                    filenames[idx] = Path.Combine(cloudFramesPath, $"cloud_frame_{idx + 1:000}.png");

                    tasks.Add(Task.Run(() =>
                    {
                        Console.WriteLine($"[CloudService] Task.Run started for frame {idx + 1}/{nFrames}, cloud {cloud.Id}. Filename: {filenames[idx]}");

                        float angleRad = (float)(angleStep * idx * Math.PI / 180.0);
                        float camX = (float)Math.Cos(angleRad) * cameraDist;
                        float camZ = (float)Math.Sin(angleRad) * cameraDist + cloudCenter.Z;
                        float camY = 0;

                        var camPos = new Vector3(camX, camY, camZ);
                        var dir = Vector3.Normalize(new Vector3(cloudCenter.X, cloudCenter.Y, cloudCenter.Z) - camPos);

                        var camera = new Camera(
                            camPos, dir, up, 65.0f,
                            width * 0.2f, height * 0.2f,
                            0.1f, 200.0f
                        );

                        Console.WriteLine($"[CloudService] Attempting to render frame {idx + 1} for cloud {cloud.Id} using {localRenderer.GetType().Name}. Output: {filenames[idx]}");
                        localRenderer.Render(camera, width, height, filenames[idx]);
                        Console.WriteLine($"Frame {idx + 1}/{nFrames} for cloud {cloud.Id} completed: {filenames[idx]}");
                    }));
                }
                await Task.WhenAll(tasks);
            }

            Console.WriteLine($"Rendering complete for cloud {cloud.Id}. Frames saved to {cloudFramesPath}");

            cloud.PreviewImagePath = filenames.Length > 0 ? filenames[0] : null;
            await UpdateCloudAsync(cloud);

            return filenames;
        }

        public async Task<string> GeneratePreviewAsync(Cloud cloud, int width = 800, int height = 600)
        {
            string previewFilePath;
            bool isNewCloud = cloud.Id == 0;

            if (isNewCloud)
            {
                string executionPath = AppContext.BaseDirectory;

                string projectRootPath = Path.GetFullPath(Path.Combine(executionPath, "..", "..", ".."));
                string tempPreviewDirectory = Path.Combine(projectRootPath, "preview");

                Directory.CreateDirectory(tempPreviewDirectory);
                previewFilePath = Path.Combine(tempPreviewDirectory, $"temp_preview_{DateTime.Now:yyyyMMddHHmmssfff}.png");
            }
            else
            {
                Cloud? persistedCloud = await GetCloudByIdAsync(cloud.Id);
                if (persistedCloud == null || string.IsNullOrEmpty(persistedCloud.StoragePath))
                {
                    throw new InvalidOperationException($"Cloud with ID {cloud.Id} not found or has no storage path for preview generation.");
                }
                previewFilePath = Path.Combine(persistedCloud.StoragePath, "preview.png");
            }
            
            if (File.Exists(previewFilePath))
            {
                File.Delete(previewFilePath);
            }

            var cloudCenter = new Vector3(0, 0, 0);
            var geometry = SingleScatterCloud.FromType(cloud.Type, cloudCenter, cloud.Humidity,cloud.Temperature,cloud.WindSpeed, light: null); // No light for preview

            IRenderer localRenderer;
            if (cloud.RenderEngine == RenderEngineType.GPU)
            {
                Console.WriteLine($"[CloudService] Creating OpenGLRenderer for cloud {(isNewCloud ? "NEW (temp)" : cloud.Id.ToString())} in GeneratePreviewAsync.");
                localRenderer = new OpenGLRenderer(new Geometry[] { geometry }, Array.Empty<Light>());
                Console.WriteLine($"[CloudService] OpenGLRenderer created for cloud {(isNewCloud ? "NEW (temp)" : cloud.Id.ToString())}.");
            }
            else // Default to CPU
            {
                Console.WriteLine($"[CloudService] Creating RayTracer for cloud {(isNewCloud ? "NEW (temp)" : cloud.Id.ToString())} in GeneratePreviewAsync.");
                localRenderer = new RayTracer(new Geometry[] { geometry }, Array.Empty<Light>());
                Console.WriteLine($"[CloudService] RayTracer created for cloud {(isNewCloud ? "NEW (temp)" : cloud.Id.ToString())}.");
            }

            var up = Vector3.UnitY;
            float cameraDist = 60.0f;
            var camPos = new Vector3(cameraDist, 0, 0);
            var dir = Vector3.Normalize(cloudCenter - camPos);

            var camera = new Camera(
                camPos, dir, up, 65.0f,
                width * 0.2f, height * 0.2f,
                0.1f, 200.0f
            );

            Console.WriteLine($"[CloudService] Attempting to generate preview for cloud {(isNewCloud ? "NEW (temp)" : cloud.Id.ToString())} using {localRenderer.GetType().Name}. Output: {previewFilePath}");
            localRenderer.Render(camera, width, height, previewFilePath);
            Console.WriteLine($"Preview generated for cloud {(isNewCloud ? "NEW (temp)" : cloud.Id.ToString())} at {previewFilePath}");

            cloud.PreviewImagePath = previewFilePath;

            if (!isNewCloud)
            {
                await UpdateCloudAsync(cloud);
            }

            return previewFilePath;
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


