using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; // Not strictly needed for these sync methods, but often present
using code.Models;
using System.IO;        // For File and Directory operations
using System.Text.Json; // For JSON serialization
using System;           // For AppContext and DateTime

namespace code.Repositories
{
    public class CloudRepository : ICloudRepository
    {
        private readonly string _cloudsBaseDirectory;
        private readonly List<Cloud> _clouds = new();
        private int _nextId = 1;

        public CloudRepository()
        {
            string executionPath = AppContext.BaseDirectory;
            string projectRootPath = Path.GetFullPath(Path.Combine(executionPath, "..", "..", "..")); 
            _cloudsBaseDirectory = Path.Combine(projectRootPath, "Clouds");

            Directory.CreateDirectory(_cloudsBaseDirectory);
            LoadAllCloudsFromDisk();
        }

        private void LoadAllCloudsFromDisk()
        {
            _clouds.Clear();
            int maxId = 0;
            if (Directory.Exists(_cloudsBaseDirectory))
            {
                // folder names like "cloud-YYYYMMDDHHMMSSFFF"
                foreach (var dirPath in Directory.GetDirectories(_cloudsBaseDirectory, "cloud-*"))
                {
                    string propertiesFilePath = Path.Combine(dirPath, "properties.json");
                    if (File.Exists(propertiesFilePath))
                    {
                        try
                        {
                            string jsonString = File.ReadAllText(propertiesFilePath);
                            Cloud? cloud = JsonSerializer.Deserialize<Cloud>(jsonString);
                            if (cloud != null)
                            {
                                cloud.StoragePath = dirPath;
                                _clouds.Add(cloud);
                                if (cloud.Id > maxId) maxId = cloud.Id;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error loading cloud properties from {propertiesFilePath}: {ex.Message}");
                        }
                    }
                }
            }
            _nextId = maxId + 1;
        }

        private void SaveCloudProperties(Cloud cloud)
        {
            if (string.IsNullOrEmpty(cloud.StoragePath))
            {
                throw new InvalidOperationException("Cloud StoragePath is not set. Cannot save properties.");
            }
            string propertiesFilePath = Path.Combine(cloud.StoragePath, "properties.json");
            string jsonString = JsonSerializer.Serialize(cloud, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(propertiesFilePath, jsonString);
        }

        public IEnumerable<Cloud> GetAll()
        {
            return _clouds.ToList();
        }

        public Cloud? GetById(int id)
        {
            return _clouds.FirstOrDefault(c => c.Id == id);
        }

        public void Add(Cloud cloud)
        {
            cloud.Id = _nextId++;
            string folderName = $"cloud-{cloud.CreatedAt:yyyyMMddHHmmssfff}"; 
            cloud.StoragePath = Path.Combine(_cloudsBaseDirectory, folderName);

            if (Directory.Exists(cloud.StoragePath))
            {
                Console.WriteLine($"Warning: Cloud storage directory {cloud.StoragePath} already exists. Potential for conflict.");
            }

            Directory.CreateDirectory(cloud.StoragePath);
            Directory.CreateDirectory(Path.Combine(cloud.StoragePath, "frames"));

            SaveCloudProperties(cloud);
            _clouds.Add(cloud);
        }

        public void Update(Cloud cloud)
        {
            var existingCloud = _clouds.FirstOrDefault(c => c.Id == cloud.Id);
            if (existingCloud != null)
            {
                existingCloud.Name = cloud.Name;
                existingCloud.Type = cloud.Type;
                existingCloud.Altitude = cloud.Altitude;
                existingCloud.Temperature = cloud.Temperature;
                existingCloud.Pressure = cloud.Pressure;
                existingCloud.WindSpeed = cloud.WindSpeed;
                existingCloud.Humidity = cloud.Humidity;
                existingCloud.RenderingPreset = cloud.RenderingPreset;
                existingCloud.RenderEngine = cloud.RenderEngine;
                existingCloud.CreatedAt = cloud.CreatedAt;
                existingCloud.PreviewImagePath = cloud.PreviewImagePath;

                if (!string.IsNullOrEmpty(existingCloud.StoragePath))
                {
                    SaveCloudProperties(existingCloud);
                }
                else
                {
                     Console.WriteLine($"Warning: Cloud {existingCloud.Id} updated but its StoragePath is missing.");
                }
            }
            else
            {
                Console.WriteLine($"Warning: Attempted to update cloud with ID {cloud.Id} but it was not found in the repository.");
            }
        }

        public void Delete(int id)
        {
            var cloud = GetById(id);
            if (cloud != null)
            {
                if (!string.IsNullOrEmpty(cloud.StoragePath) && Directory.Exists(cloud.StoragePath))
                {
                    Directory.Delete(cloud.StoragePath, recursive: true);
                }
                _clouds.Remove(cloud);
            }
        }
    }
}
