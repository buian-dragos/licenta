using System;
using System.IO;
using System.Threading.Tasks;
using System.Numerics;
using System.Collections.Generic;
using code.Domain;
using code.Repositories;
namespace code.Services
{
    public class CloudService : ICloudService
    {
        private readonly ICloudRepository _cloudRepository;
        private readonly string _framesDirectory = "../../../frames";
        public CloudService(ICloudRepository cloudRepository)
        {
            _cloudRepository = cloudRepository;
        }
        public Task<IEnumerable<Cloud>> GetAllCloudsAsync()
        {
            return Task.FromResult(_cloudRepository.GetAll());
        }
        public Task<Cloud?> GetCloudByIdAsync(int id)
        {
            return Task.FromResult(_cloudRepository.GetById(id));
        }
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
        public async Task<string[]> RenderCloudAnimationAsync(Cloud cloud, int nFrames = 36, int width = 800, int height = 600)
        {
            CleanUpOldFrames();
            var lights = CreateLights();
            var geometries = CreateGeometries(lights[0]);
            var rayTracer = new RayTracer(geometries, lights);
            var cloudCenter = new Vector3(0.0f, 0.0f, 100.0f);
            var up = Vector3.UnitY;
            float cameraDist = 60.0f;
            float step = 360.0f / nFrames;
            var tasks = new List<Task>();
            var filenames = new string[nFrames];
            for (int i = 0; i < nFrames; i++)
            {
                int frameIndex = i;
                string filename = $"{_framesDirectory}/cloud_{frameIndex + 1:000}.png";
                filenames[i] = filename;
                tasks.Add(Task.Run(() =>
                {
                    float angle = (float)(step * frameIndex * Math.PI / 180.0);
                    float camX = (float)Math.Cos(angle) * cameraDist;
                    float camZ = (float)Math.Sin(angle) * cameraDist;
                    Vector3 camPos = new Vector3(camX, 0.0f, camZ + cloudCenter.Z);
                    Vector3 dir = Vector3.Normalize(cloudCenter - camPos);
                    var camera = new Camera(
                        camPos,
                        dir,
                        up,
                        65.0f,   // FOV
                        160.0f,  // viewplane width
                        120.0f,  // viewplane height
                        0.0f,    // near
                        200.0f   // far
                    );
                    rayTracer.Render(camera, width, height, filename);
                    Console.WriteLine($"Frame {frameIndex + 1}/{nFrames} completed");
                }));
            }
            await Task.WhenAll(tasks);
            Console.WriteLine("Done!");
            // Update the cloud with the image path
            cloud.ImagePath = filenames[0]; // Store the first frame as the thumbnail
            await UpdateCloudAsync(cloud);
            return filenames;
        }
        private void CleanUpOldFrames()
        {
            if (Directory.Exists(_framesDirectory))
            {
                var dir = new DirectoryInfo(_framesDirectory);
                foreach (var file in dir.EnumerateFiles("*.png"))
                    file.Delete();
            }
            Directory.CreateDirectory(_framesDirectory);
        }
        private Light[] CreateLights()
        {
            return new[]
            {
                new Light(
                    new Vector3(50.0f, 80.0f, 100.0f),
                    new Color(1.0, 1.0, 1.0, 1.0),
                    new Color(1.0, 1.0, 1.0, 1.0),
                    new Color(1.0, 1.0, 1.0, 1.0),
                    1.5f
                )
            };
        }
        private Geometry[] CreateGeometries(Light light)
        {
            return new Geometry[]
            {
                new SingleScatterCloud(
                    new Vector3(0.0f, 0.0f, 100.0f),
                    new Vector3(35.0f, 15.0f, 25.0f),
                    0.12f,
                    new Color(1.0, 1.0, 1.0, 0.3),
                    light
                )
            };
        }
    }
}
