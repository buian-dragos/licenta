using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using code.Models;
using code.Services;

namespace code.ViewModels
{
    public class CloudViewModel : ViewModelBase
    {
        private readonly ICloudService _cloudService;
        private ObservableCollection<Cloud> _clouds;
        private Cloud? _selectedCloud;
        private bool _isLoading;
        private string _statusMessage;

        public ObservableCollection<Cloud> Clouds
        {
            get => _clouds;
            set => SetProperty(ref _clouds, value);
        }

        public Cloud? SelectedCloud
        {
            get => _selectedCloud;
            set => SetProperty(ref _selectedCloud, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public IAsyncRelayCommand LoadCloudsCommand { get; }
        public IAsyncRelayCommand<Cloud> AddCloudCommand { get; }
        public IAsyncRelayCommand<Cloud> UpdateCloudCommand { get; }
        public IAsyncRelayCommand<int> DeleteCloudCommand { get; }
        public IAsyncRelayCommand<Cloud> RenderCloudAnimationCommand { get; }
        public IRelayCommand CreateNewCloudCommand { get; }

        public CloudViewModel(ICloudService cloudService)
        {
            _cloudService = cloudService;
            _clouds = new ObservableCollection<Cloud>();
            _statusMessage = "Ready";

            LoadCloudsCommand = new AsyncRelayCommand(LoadCloudsAsync);
            AddCloudCommand = new AsyncRelayCommand<Cloud>(AddCloudAsync);
            UpdateCloudCommand = new AsyncRelayCommand<Cloud>(UpdateCloudAsync, CanModifyCloud);
            DeleteCloudCommand = new AsyncRelayCommand<int>(DeleteCloudAsync);
            RenderCloudAnimationCommand = new AsyncRelayCommand<Cloud>(RenderCloudAsync, CanRenderCloud);
            CreateNewCloudCommand = new RelayCommand(CreateNewCloud);

            // Initial load
            _ = LoadCloudsAsync();
        }

        private void CreateNewCloud()
        {
            var newCloud = new Cloud
            {
                Name = "New Cloud",
                Type = CloudType.Cumulus,
                Altitude = 1000,
                Temperature = 15.0,
                Pressure = 1013.25,
                WindSpeed = 5.0,
                Humidity = 50.0,
                RenderingPreset = RenderingPreset.Fast,
                CameraPosition = CameraPosition.CloudLevel,
                CreatedAt = DateTime.Now,
                PreviewImagePath = null
            };

            SelectedCloud = newCloud;
        }

        private async Task LoadCloudsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading clouds...";

                var clouds = await _cloudService.GetAllCloudsAsync();
                Clouds.Clear();

                foreach (var cloud in clouds)
                    Clouds.Add(cloud);

                StatusMessage = $"Loaded {Clouds.Count} clouds";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading clouds: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AddCloudAsync(Cloud? cloud)
        {
            if (cloud == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Adding new cloud...";

                await _cloudService.AddCloudAsync(cloud);
                Clouds.Add(cloud);
                SelectedCloud = cloud;
                StatusMessage = $"Added cloud: {cloud.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding cloud: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task UpdateCloudAsync(Cloud? cloud)
        {
            if (cloud == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = $"Updating cloud: {cloud.Name}...";

                await _cloudService.UpdateCloudAsync(cloud);
                await LoadCloudsAsync();
                StatusMessage = $"Updated cloud: {cloud.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error updating cloud: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DeleteCloudAsync(int id)
        {
            try
            {
                IsLoading = true;
                StatusMessage = $"Deleting cloud with ID {id}...";

                await _cloudService.DeleteCloudAsync(id);

                var cloudToRemove = Clouds.FirstOrDefault(c => c.Id == id);
                if (cloudToRemove != null)
                    Clouds.Remove(cloudToRemove);

                if (SelectedCloud?.Id == id)
                    SelectedCloud = null;

                StatusMessage = $"Deleted cloud with ID {id}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting cloud: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RenderCloudAsync(Cloud? cloud)
        {
            if (cloud == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = $"Rendering cloud animation for {cloud.Name}...";

                var frames = await _cloudService.RenderCloudAnimationAsync(cloud);
                StatusMessage = $"Rendered {frames.Length} frames for cloud: {cloud.Name}";

                await UpdateCloudAsync(cloud);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error rendering cloud animation: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanModifyCloud(Cloud? cloud) => cloud != null;

        private bool CanRenderCloud(Cloud? cloud) => cloud != null && !IsLoading;
    }
}
