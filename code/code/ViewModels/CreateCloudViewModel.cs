// File: ViewModels/CreateCloudViewModel.cs

using System;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using code.Services;
using code.Models;
using Avalonia.Media.Imaging;
using code.Views;
using Avalonia; // Added
using Avalonia.Controls; // Added
using Avalonia.Controls.ApplicationLifetimes; // Added

namespace code.ViewModels
{
    public partial class CreateCloudViewModel : ViewModelBase
    {
        private readonly CloudService _cloudService;
        private readonly Action? _onCreated;
        private readonly Action? _onCancel;

        public bool IsPreviewVisible => !IsPreviewLoading;
        public bool CanGeneratePreview => !IsPreviewLoading;

        partial void OnIsPreviewLoadingChanged(bool value)
        {
            OnPropertyChanged(nameof(IsPreviewVisible));
            OnPropertyChanged(nameof(CanGeneratePreview));
        }

        // 0 = SingleScatter, 1 = MultipleScatter, 2 = VolumetricRender (or cloud types)
        [ObservableProperty]
        private int _cloudTypeIndex = 0;

        [ObservableProperty]
        private double _altitude;

        [ObservableProperty]
        private double _pressure;

        [ObservableProperty]
        private double _humidity = 50;

        [ObservableProperty]
        private double _temperature;

        [ObservableProperty]
        private double _windSpeed;

        // 0 = Fast, 1 = Quality
        [ObservableProperty]
        private int _renderingPresetIndex = 0;

        // 0 = CPU, 1 = GPU
        [ObservableProperty]
        private int _renderEngineIndex = 0; // Default to CPU

        [ObservableProperty]
        private string _statusMessage;

        [ObservableProperty]
        private bool _isPreviewLoading;

        [ObservableProperty]
        private Bitmap _previewImage;

        [ObservableProperty]
        private string? _previewImagePath;

        [ObservableProperty]
        private bool _isRenderComplete;
        
        [ObservableProperty]
        private double _renderProgress;    // 0–100

        [ObservableProperty]
        private bool _isRendering;

        
        public bool HasPreview => PreviewImage != null;

        partial void OnPreviewImageChanged(Bitmap value)
        {
            OnPropertyChanged(nameof(HasPreview));
        }


        // Cloud Type Toggle Button Properties
        private bool _isCumulonimbusSelected;
        
        public bool IsCumulonimbusSelected
        {
            get => _isCumulonimbusSelected;
            set
            {
                if (SetProperty(ref _isCumulonimbusSelected, value) && value)
                {
                    // When selected, deselect all others and update index
                    DeselectionAllCloudTypesExcept("Cumulonimbus");
                    CloudTypeIndex = 0;
                }
            }
        }

        private bool _isCumulusSelected;
        public bool IsCumulusSelected
        {
            get => _isCumulusSelected;
            set
            {
                if (SetProperty(ref _isCumulusSelected, value) && value)
                {
                    DeselectionAllCloudTypesExcept("Cumulus");
                    CloudTypeIndex = 1;
                }
            }
        }

        private bool _isStratusSelected;
        public bool IsStratusSelected
        {
            get => _isStratusSelected;
            set
            {
                if (SetProperty(ref _isStratusSelected, value) && value)
                {
                    DeselectionAllCloudTypesExcept("Stratus");
                    CloudTypeIndex = 2;
                }
            }
        }

        private bool _isStratocumulusSelected;
        public bool IsStratocumulusSelected
        {
            get => _isStratocumulusSelected;
            set
            {
                if (SetProperty(ref _isStratocumulusSelected, value) && value)
                {
                    DeselectionAllCloudTypesExcept("Stratocumulus");
                    CloudTypeIndex = 3;
                }
            }
        }

        private bool _isNimbostratusSelected;
        public bool IsNimbostratusSelected
        {
            get => _isNimbostratusSelected;
            set
            {
                if (SetProperty(ref _isNimbostratusSelected, value) && value)
                {
                    DeselectionAllCloudTypesExcept("Nimbostratus");
                    CloudTypeIndex = 4;
                }
            }
        }

        private bool _isAltostratusSelected;
        public bool IsAltostratusSelected
        {
            get => _isAltostratusSelected;
            set
            {
                if (SetProperty(ref _isAltostratusSelected, value) && value)
                {
                    DeselectionAllCloudTypesExcept("Altostratus");
                    CloudTypeIndex = 5;
                }
            }
        }

        private bool _isAltocumulusSelected;
        public bool IsAltocumulusSelected
        {
            get => _isAltocumulusSelected;
            set
            {
                if (SetProperty(ref _isAltocumulusSelected, value) && value)
                {
                    DeselectionAllCloudTypesExcept("Altocumulus");
                    CloudTypeIndex = 6;
                }
            }
        }

        private bool _isCirrostratusSelected;
        public bool IsCirrostratusSelected
        {
            get => _isCirrostratusSelected;
            set
            {
                if (SetProperty(ref _isCirrostratusSelected, value) && value)
                {
                    DeselectionAllCloudTypesExcept("Cirrostratus");
                    CloudTypeIndex = 7;
                }
            }
        }

        private bool _isCirrocumulusSelected;
        public bool IsCirrocumulusSelected
        {
            get => _isCirrocumulusSelected;
            set
            {
                if (SetProperty(ref _isCirrocumulusSelected, value) && value)
                {
                    DeselectionAllCloudTypesExcept("Cirrocumulus");
                    CloudTypeIndex = 8;
                }
            }
        }

        private bool _isCirrusSelected;
        public bool IsCirrusSelected
        {
            get => _isCirrusSelected;
            set
            {
                if (SetProperty(ref _isCirrusSelected, value) && value)
                {
                    DeselectionAllCloudTypesExcept("Cirrus");
                    CloudTypeIndex = 9;
                }
            }
        }

        // Rendering Preset Toggle Button Properties
        private bool _isFastRenderingSelected;
        public bool IsFastRenderingSelected
        {
            get => _isFastRenderingSelected;
            set
            {
                if (SetProperty(ref _isFastRenderingSelected, value) && value)
                {
                    // If Fast is selected, deselect Quality and update index
                    IsQualityRenderingSelected = false;
                    RenderingPresetIndex = 0;
                }
                else if (!value && !IsQualityRenderingSelected)
                {
                    // Prevent deselecting both - if this one is deselected and other is not selected, reselect this one
                    _isFastRenderingSelected = true;
                    OnPropertyChanged(nameof(IsFastRenderingSelected));
                }
            }
        }

        private bool _isQualityRenderingSelected = true; // Default to Quality
        public bool IsQualityRenderingSelected
        {
            get => _isQualityRenderingSelected;
            set
            {
                if (SetProperty(ref _isQualityRenderingSelected, value) && value)
                {
                    // If Quality is selected, deselect Fast and update index
                    IsFastRenderingSelected = false;
                    RenderingPresetIndex = 1;
                }
                else if (!value && !IsFastRenderingSelected)
                {
                    // Prevent deselecting both - if this one is deselected and other is not selected, reselect this one
                    _isQualityRenderingSelected = true;
                    OnPropertyChanged(nameof(IsQualityRenderingSelected));
                }
            }
        }

        // Render Engine Toggle Button Properties
        private bool _isCpuRenderEngineSelected = true; // Default to CPU
        public bool IsCpuRenderEngineSelected
        {
            get => _isCpuRenderEngineSelected;
            set
            {
                if (SetProperty(ref _isCpuRenderEngineSelected, value) && value)
                {
                    IsGpuRenderEngineSelected = false;
                    RenderEngineIndex = 0;
                }
                else if (!value && !IsGpuRenderEngineSelected)
                {
                    _isCpuRenderEngineSelected = true;
                    OnPropertyChanged(nameof(IsCpuRenderEngineSelected));
                }
            }
        }

        private bool _isGpuRenderEngineSelected;
        public bool IsGpuRenderEngineSelected
        {
            get => _isGpuRenderEngineSelected;
            set
            {
                if (SetProperty(ref _isGpuRenderEngineSelected, value) && value)
                {
                    IsCpuRenderEngineSelected = false;
                    RenderEngineIndex = 1;
                }
                else if (!value && !IsCpuRenderEngineSelected)
                {
                    _isGpuRenderEngineSelected = true;
                    OnPropertyChanged(nameof(IsGpuRenderEngineSelected));
                }
            }
        }

        // Helper method to deselect all cloud types except the one specified
        private void DeselectionAllCloudTypesExcept(string cloudType)
        {
            if (cloudType != "Cumulonimbus") _isCumulonimbusSelected = false;
            if (cloudType != "Cumulus") _isCumulusSelected = false;
            if (cloudType != "Stratus") _isStratusSelected = false;
            if (cloudType != "Stratocumulus") _isStratocumulusSelected = false;
            if (cloudType != "Nimbostratus") _isNimbostratusSelected = false;
            if (cloudType != "Altostratus") _isAltostratusSelected = false;
            if (cloudType != "Altocumulus") _isAltocumulusSelected = false;
            if (cloudType != "Cirrostratus") _isCirrostratusSelected = false;
            if (cloudType != "Cirrocumulus") _isCirrocumulusSelected = false;
            if (cloudType != "Cirrus") _isCirrusSelected = false;

            // Notify that all properties have changed
            OnPropertyChanged(nameof(IsCumulonimbusSelected));
            OnPropertyChanged(nameof(IsCumulusSelected));
            OnPropertyChanged(nameof(IsStratusSelected));
            OnPropertyChanged(nameof(IsStratocumulusSelected));
            OnPropertyChanged(nameof(IsNimbostratusSelected));
            OnPropertyChanged(nameof(IsAltostratusSelected));
            OnPropertyChanged(nameof(IsAltocumulusSelected));
            OnPropertyChanged(nameof(IsCirrostratusSelected));
            OnPropertyChanged(nameof(IsCirrocumulusSelected));
            OnPropertyChanged(nameof(IsCirrusSelected));
        }

        // Arrays of labels for binding toggle‐button Content
        public string[] CloudTypeLabels { get; } = new[] { "SingleScatter", "MultipleScatter", "VolumetricRender" };
        public string[] RenderingPresetLabels { get; } = new[] { "Fast", "Quality" };
        public string[] RenderEngineLabels { get; } = new[] { "CPU", "GPU" };

        public CreateCloudViewModel(
            CloudService cloudService,
            Action? onCreated = null,
            Action? onCancel  = null)
        {
            _cloudService = cloudService;
            _onCreated    = onCreated;
            _onCancel     = onCancel;

            // Initialize toggle button states based on default indices
            UpdateToggleButtonStatesFromIndices();
        }

        // Helper method to sync toggle states with indices
        private void UpdateToggleButtonStatesFromIndices()
        {
            // Set cloud type toggle states based on index
            switch (CloudTypeIndex)
            {
                case 0:
                    IsCumulonimbusSelected = true;
                    break;
                case 1:
                    IsCumulusSelected = true;
                    break;
                case 2:
                    IsStratusSelected = true;
                    break;
                case 3:
                    IsStratocumulusSelected = true;
                    break;
                case 4:
                    IsNimbostratusSelected = true;
                    break;
                case 5:
                    IsAltostratusSelected = true;
                    break;
                case 6:
                    IsAltocumulusSelected = true;
                    break;
                case 7:
                    IsCirrostratusSelected = true;
                    break;
                case 8:
                    IsCirrocumulusSelected = true;
                    break;
                case 9:
                    IsCirrusSelected = true;
                    break;
                default:
                    IsCumulonimbusSelected = true; // Default
                    break;
            }

            // Set rendering preset toggle states
            IsFastRenderingSelected = RenderingPresetIndex == 0;
            IsQualityRenderingSelected = RenderingPresetIndex == 1;

            // Set render engine toggle states
            IsCpuRenderEngineSelected = RenderEngineIndex == 0;
            IsGpuRenderEngineSelected = RenderEngineIndex == 1;
        }
        
        [RelayCommand]
        private void SetCloudTypeIndex(int index)
        {
            CloudTypeIndex = index;
            UpdateToggleButtonStatesFromIndices();
        }

        [RelayCommand]
        private void SetRenderingPresetIndex(int index)
        {
            RenderingPresetIndex = index;
            UpdateToggleButtonStatesFromIndices();
        }

        [RelayCommand]
        private void SetRenderEngineIndex(int index)
        {
            RenderEngineIndex = index;
            UpdateToggleButtonStatesFromIndices();
        }

        [RelayCommand]
        private async Task GeneratePreviewAsync()
        {
            PreviewImage = null;
            PreviewImagePath = null;
            
            PreviewImage?.Dispose();
            
            IsPreviewLoading = true;
            StatusMessage = string.Empty;

            try
            {
                StatusMessage = "Generating preview...";
                // Build cloud model DTO from inputs
                var cloud = new Cloud
                {
                    Name = $"Preview_{DateTime.Now:yyyyMMdd_HHmmss}",
                    Type = (CloudType)CloudTypeIndex,
                    Altitude = Altitude,
                    Pressure = Pressure,
                    Humidity = Humidity,
                    Temperature = Temperature,
                    WindSpeed = WindSpeed,
                    RenderingPreset = RenderingPreset.Fast,
                    RenderEngine = (RenderEngineType)RenderEngineIndex // Set RenderEngine for preview
                };
                // Use service to generate single-frame preview
                var previewPath = await _cloudService.GeneratePreviewAsync(cloud, width: 800, height: 600);
                PreviewImagePath = previewPath;
                PreviewImage = new Bitmap(previewPath);
                StatusMessage = "Preview generated!";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Preview failed: {ex.Message}";
            }
            finally
            {
                IsPreviewLoading = false;
            }
        }

        [RelayCommand]
        private async Task RenderCloudAsync()
        {
            StatusMessage = string.Empty;
            
            RenderProgress = 0;
            IsRendering = true;

            try
            {
                StatusMessage = "Creating and rendering cloud...";

                // Build cloud model from inputs
                var cloud = new Cloud
                {
                    Name = $"Cloud_{DateTime.Now:yyyyMMdd_HHmmss}",
                    Type = (CloudType)CloudTypeIndex,
                    Altitude = Altitude,
                    Pressure = Pressure,
                    Humidity = Humidity,
                    Temperature = Temperature,
                    WindSpeed = WindSpeed,
                    RenderingPreset = (RenderingPreset)RenderingPresetIndex,
                    RenderEngine = (RenderEngineType)RenderEngineIndex // Set RenderEngine for full render
                };
                
                // Persist and render
                await _cloudService.AddCloudAsync(cloud);
                // var frames = await _cloudService.RenderCloudAnimationAsync(cloud);

                int nFrames = RenderingPresetIndex == 0 ? 36 : 360; // 0=Fast, 1=Quality

                var progress = new Progress<double>(p => RenderProgress = p * 100);

                var frames = await _cloudService.RenderCloudAnimationAsync(cloud, nFrames: nFrames, progress: progress);




                // Update preview
                PreviewImagePath = cloud.PreviewImagePath;
                StatusMessage = $"Rendered {frames.Length} frames successfully.";
                IsRenderComplete = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error rendering cloud: {ex.Message}";
            }
            finally
            {
                IsRendering = false;
            }
        }

        [RelayCommand]
        private void NavigateToStartPage()
        {
            _onCancel?.Invoke();
        }
        
        [RelayCommand]
        private void CloseRenderCompletePopup()
        {
            IsRenderComplete = false;
        }
        
        [RelayCommand]
        private async Task ImportWeatherDataAsync()
        {
            var dialog = new WeatherLocationPickerDialog
            {
            };

            Window? ownerWindow = null;
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                ownerWindow = desktopLifetime.MainWindow;
            }
            
            var result = await dialog.ShowDialog<(double Lat, double Lon)?>(ownerWindow);

            if (result == null)
                return;

            var (lat, lon) = result.Value;
            const string apiKey = "6b5faa7fb32bd53231e7cc77dc92a076"; 
            var url = $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&appid={apiKey}&units=metric";

            try
            {
                using var client = new HttpClient();
                var json = await client.GetStringAsync(url);
                dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                Temperature = (double)data.main.temp;
                Humidity    = (double)data.main.humidity;
                Pressure    = (double)data.main.pressure;
                WindSpeed   = (double)data.wind.speed;

                StatusMessage = $"Imported weather for lat={lat:F4}, lon={lon:F4}";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to fetch weather: " + ex.Message;
            }
        }

        
        
        [RelayCommand]
        private void Cancel()
        {
            _onCancel?.Invoke();
        }
    }

    // ─── DTOs for service calls ──────────────────────────────────────────

    public class CloudPreviewDto
    {
        public DateTime CreatedAt { get; set; }
        public int CloudTypeIndex { get; set; }
        public double Altitude { get; set; }
        public double Pressure { get; set; }
        public double Humidity { get; set; }
        public double Temperature { get; set; }
        public double WindSpeed { get; set; }
        public int RenderingPresetIndex { get; set; }
        public int RenderEngineIndex { get; set; } // Added for DTO
    }

    public class CloudRenderDto : CloudPreviewDto
    {
    }
}
