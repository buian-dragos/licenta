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

        private void CleanUpCloudFramesDirectory(Cloud cloudWithStoragePath)
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

        public async Task<string[]> RenderCloudAnimationAsync(Cloud cloud, int nFrames = 36, int width = 800, int height = 600, bool noLights = false)
        {
            Cloud? persistedCloud = await GetCloudByIdAsync(cloud.Id);
            if (persistedCloud == null || string.IsNullOrEmpty(persistedCloud.StoragePath))
            {
                throw new InvalidOperationException($"Cloud with ID {cloud.Id} not found in repository or has no storage path.");
            }

            string cloudFramesPath = Path.Combine(persistedCloud.StoragePath, "frames");
            CleanUpCloudFramesDirectory(persistedCloud);

            var cloudCenter = new Vector3(0, 0, 0);
            var lights = noLights ? Array.Empty<Light>() : CreateLights();

            Func<Light?, Geometry> geometryFactory = light =>
                SingleScatterCloud.FromType(cloud.Type, cloudCenter, cloud.Humidity, light);

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
                localRenderer = new OpenGLRenderer(geometries.ToArray(), lights);
            }
            else // Default to CPU
            {
                localRenderer = new RayTracer(geometries.ToArray(), lights);
            }

            var up = Vector3.UnitY;
            float cameraDist = 60.0f;
            float angleStep = 360.0f / nFrames;
            var tasks = new List<Task>();
            var filenames = new string[nFrames];

            for (int i = 0; i < nFrames; i++)
            {
                int idx = i;
                filenames[i] = Path.Combine(cloudFramesPath, $"cloud_frame_{idx + 1:000}.png");

                tasks.Add(Task.Run(() =>
                {
                    float angleRad = (float)(angleStep * idx * Math.PI / 180.0);
                    float camX = (float)Math.Cos(angleRad) * cameraDist;
                    float camZ = (float)Math.Sin(angleRad) * cameraDist + cloudCenter.Z;

                    // Default camera Y position to cloud altitude as CameraPosition is removed
                    float camY = (float)cloud.Altitude;

                    var camPos = new Vector3(camX, camY, camZ);
                    var dir = Vector3.Normalize(new Vector3(cloudCenter.X, (float)cloud.Altitude, cloudCenter.Z) - camPos);

                    var camera = new Camera(
                        camPos, dir, up, 65.0f,
                        width * 0.2f, height * 0.2f,
                        0.0f, 200.0f
                    );

                    localRenderer.Render(camera, width, height, filenames[idx]);
                    Console.WriteLine($"Frame {idx + 1}/{nFrames} for cloud {cloud.Id} completed: {filenames[idx]}");
                }));
            }

            await Task.WhenAll(tasks);
            Console.WriteLine($"Rendering complete for cloud {cloud.Id}. Frames saved to {cloudFramesPath}");

            cloud.PreviewImagePath = filenames.Length > 0 ? filenames[0] : null;
            await UpdateCloudAsync(cloud);

            return filenames;
        }

        public async Task<string> GeneratePreviewAsync(Cloud cloud, int width = 800, int height = 600)
        {
            string previewFilePath;
            bool isNewCloud = cloud.Id == 0; // Assuming Id is 0 for a new, unsaved cloud from ViewModel

            if (isNewCloud)
            {
                // For a new, unsaved cloud, generate preview in a temporary global location.
                string executionPath = AppContext.BaseDirectory;
                // executionPath is typically <project_folder>/bin/Debug/netX.Y/
                // projectRootPath should then be <project_folder>/
                string projectRootPath = Path.GetFullPath(Path.Combine(executionPath, "..", "..", ".."));
                string tempPreviewDirectory = Path.Combine(projectRootPath, "preview"); // Should resolve to code/preview/
                
                Directory.CreateDirectory(tempPreviewDirectory); // Ensure the directory exists
                // Use a unique name for the temporary preview file
                previewFilePath = Path.Combine(tempPreviewDirectory, $"temp_preview_{DateTime.Now:yyyyMMddHHmmssfff}.png");
            }
            else
            {
                // For an existing cloud, use its dedicated storage path.
                Cloud? persistedCloud = await GetCloudByIdAsync(cloud.Id);
                if (persistedCloud == null || string.IsNullOrEmpty(persistedCloud.StoragePath))
                {
                    throw new InvalidOperationException($"Cloud with ID {cloud.Id} not found or has no storage path for preview generation.");
                }
                previewFilePath = Path.Combine(persistedCloud.StoragePath, "preview.png");
            }

            // If a file already exists at the path, delete it before rendering the new one.
            // This is especially important for the non-temporary preview.png.
            if (File.Exists(previewFilePath))
            {
                File.Delete(previewFilePath);
            }

            // Rendering logic using 'cloud' parameter's properties (Type, Humidity, Altitude etc.)
            var cloudCenter = new Vector3(0, 0, (float)cloud.Altitude); // Use altitude from input 'cloud'
            var geometry = SingleScatterCloud.FromType(cloud.Type, cloudCenter, cloud.Humidity, light: null); // No light for preview
            
            IRenderer localRenderer;
            if (cloud.RenderEngine == RenderEngineType.GPU)
            {
                localRenderer = new OpenGLRenderer(new Geometry[] { geometry }, Array.Empty<Light>());
            }
            else // Default to CPU
            {
                localRenderer = new RayTracer(new Geometry[] { geometry }, Array.Empty<Light>());
            }
            
            var up = Vector3.UnitY;
            float cameraDist = 60.0f;
            // Fixed camera position for preview, using altitude from the input 'cloud'
            var camPos = new Vector3(cameraDist, (float)cloud.Altitude, (float)cloud.Altitude); 
            var dir = Vector3.Normalize(cloudCenter - camPos);

            var camera = new Camera(
                camPos, dir, up, 65.0f,
                width * 0.2f, height * 0.2f, // viewplane size adjustment
                0.0f, 200.0f // near/far planes
            );
            
            localRenderer.Render(camera, width, height, previewFilePath);
            Console.WriteLine($"Preview generated for cloud {(isNewCloud ? "NEW (temp)" : cloud.Id.ToString())} at {previewFilePath}");

            // Update the PreviewImagePath on the cloud object passed in (which might be a ViewModel's DTO)
            cloud.PreviewImagePath = previewFilePath;

            if (!isNewCloud)
            {
                // Only persist the PreviewImagePath update to storage if it's an existing cloud.
                // The 'cloud' object here should have all properties of the persisted cloud if it came from GetById.
                // If 'cloud' is a DTO from UI, ensure it has the correct Id for UpdateCloudAsync.
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
