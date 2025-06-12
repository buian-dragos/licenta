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
            // Assuming the executable is in a path like /<project_root>/code/bin/Debug/netX.Y/
            // We want _cloudsBaseDirectory to be /<project_root>/code/Clouds/
            string projectRootPath = Path.GetFullPath(Path.Combine(executionPath, "..", "..", "..")); 
            _cloudsBaseDirectory = Path.Combine(projectRootPath, "Clouds");

            Directory.CreateDirectory(_cloudsBaseDirectory); // Ensure base directory exists
            LoadAllCloudsFromDisk();
        }

        private void LoadAllCloudsFromDisk()
        {
            _clouds.Clear();
            int maxId = 0;
            if (Directory.Exists(_cloudsBaseDirectory))
            {
                // Match folder names like "cloud-YYYYMMDDHHMMSSFFF"
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
                                cloud.StoragePath = dirPath; // Ensure StoragePath is correctly set upon loading
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
            // To ensure data is fresh, could reload from disk here or rely on initial load.
            // LoadAllCloudsFromDisk(); // Uncomment if fresh data is needed on every call
            return _clouds.ToList(); // Return a copy of the in-memory list
        }

        public Cloud? GetById(int id)
        {
            return _clouds.FirstOrDefault(c => c.Id == id);
        }

        public void Add(Cloud cloud)
        {
            cloud.Id = _nextId++;
            // Folder name format: cloud-{timeCreatedAt} as per user request
            string folderName = $"cloud-{cloud.CreatedAt:yyyyMMddHHmmssfff}"; 
            cloud.StoragePath = Path.Combine(_cloudsBaseDirectory, folderName);

            if (Directory.Exists(cloud.StoragePath))
            {
                // This case implies a non-unique CreatedAt timestamp to the millisecond, or a retry.
                // A robust solution might append cloud.Id or a unique suffix.
                // For now, log and proceed; new files might overwrite or merge depending on OS.
                Console.WriteLine($"Warning: Cloud storage directory {cloud.StoragePath} already exists. Potential for conflict.");
                // To ensure uniqueness, one might use:
                // folderName = $"cloud-{cloud.CreatedAt:yyyyMMddHHmmssfff}-{cloud.Id}";
                // cloud.StoragePath = Path.Combine(_cloudsBaseDirectory, folderName);
            }

            Directory.CreateDirectory(cloud.StoragePath);
            Directory.CreateDirectory(Path.Combine(cloud.StoragePath, "frames")); // Create frames subdirectory

            SaveCloudProperties(cloud); // Save properties to JSON file
            _clouds.Add(cloud);
        }

        public void Update(Cloud cloud)
        {
            var existingCloud = _clouds.FirstOrDefault(c => c.Id == cloud.Id);
            if (existingCloud != null)
            {
                // Update properties of the existingCloud object from the passed cloud object
                // This preserves the existingCloud instance in the list if other parts of the app hold references to it.
                // However, typical MVVM might replace the object or expect the passed 'cloud' to be the new reference.
                // For simplicity, let's update the fields of the existing object.
                existingCloud.Name = cloud.Name;
                existingCloud.Type = cloud.Type;
                existingCloud.Altitude = cloud.Altitude;
                existingCloud.Temperature = cloud.Temperature;
                existingCloud.Pressure = cloud.Pressure;
                existingCloud.WindSpeed = cloud.WindSpeed;
                existingCloud.Humidity = cloud.Humidity;
                existingCloud.RenderingPreset = cloud.RenderingPreset;
                existingCloud.RenderEngine = cloud.RenderEngine;
                existingCloud.CreatedAt = cloud.CreatedAt; // Should this be updatable? Usually not.
                existingCloud.PreviewImagePath = cloud.PreviewImagePath;
                // StoragePath should not change after creation.
                // existingCloud.StoragePath = cloud.StoragePath; // This should be set on Add and not change.

                if (!string.IsNullOrEmpty(existingCloud.StoragePath))
                {
                    SaveCloudProperties(existingCloud); // Save updated properties to JSON
                }
                else
                {
                     Console.WriteLine($"Warning: Cloud {existingCloud.Id} updated but its StoragePath is missing.");
                }
            }
            else
            {
                // Cloud not found in memory, perhaps it should be an error or an Add?
                Console.WriteLine($"Warning: Attempted to update cloud with ID {cloud.Id} but it was not found in the repository.");
            }
        }

        public void Delete(int id)
        {
            var cloud = GetById(id); // Find cloud in the in-memory list
            if (cloud != null)
            {
                if (!string.IsNullOrEmpty(cloud.StoragePath) && Directory.Exists(cloud.StoragePath))
                {
                    Directory.Delete(cloud.StoragePath, recursive: true); // Delete the cloud's folder from disk
                }
                _clouds.Remove(cloud); // Remove from the in-memory list
            }
        }
    }
}
