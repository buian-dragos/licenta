// File: ViewModels/CreateCloudViewModel.cs

using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using code.Services;

namespace code.ViewModels
{
    public partial class CreateCloudViewModel : ViewModelBase
    {
        private readonly CloudService _cloudService;
        private readonly Action _onCreated;
        private readonly Action _onCancel;

        // ─── Bound Properties ────────────────────────────────────────────

        [ObservableProperty]
        private DateTimeOffset _createdDate = DateTimeOffset.Now;

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
        private int _renderingPresetIndex = 1;

        // 0 = GroundLevel, 1 = CloudLevel, 2 = AboveClouds
        [ObservableProperty]
        private int _cameraPositionIndex = 1;

        [ObservableProperty]
        private string _previewImagePath;

        [ObservableProperty]
        private string _statusMessage;

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

        // Camera Position Toggle Button Properties
        private bool _isGroundLevelSelected;
        public bool IsGroundLevelSelected
        {
            get => _isGroundLevelSelected;
            set
            {
                if (SetProperty(ref _isGroundLevelSelected, value) && value)
                {
                    // If Ground Level is selected, deselect others and update index
                    IsCloudLevelSelected = false;
                    IsAboveCloudsSelected = false;
                    CameraPositionIndex = 0;
                }
                else if (!value && !IsCloudLevelSelected && !IsAboveCloudsSelected)
                {
                    // Prevent deselecting all - if this one is deselected and no others are selected, reselect this one
                    _isGroundLevelSelected = true;
                    OnPropertyChanged(nameof(IsGroundLevelSelected));
                }
            }
        }

        private bool _isCloudLevelSelected = true; // Default to Cloud Level
        public bool IsCloudLevelSelected
        {
            get => _isCloudLevelSelected;
            set
            {
                if (SetProperty(ref _isCloudLevelSelected, value) && value)
                {
                    // If Cloud Level is selected, deselect others and update index
                    IsGroundLevelSelected = false;
                    IsAboveCloudsSelected = false;
                    CameraPositionIndex = 1;
                }
                else if (!value && !IsGroundLevelSelected && !IsAboveCloudsSelected)
                {
                    // Prevent deselecting all - if this one is deselected and no others are selected, reselect this one
                    _isCloudLevelSelected = true;
                    OnPropertyChanged(nameof(IsCloudLevelSelected));
                }
            }
        }

        private bool _isAboveCloudsSelected;
        public bool IsAboveCloudsSelected
        {
            get => _isAboveCloudsSelected;
            set
            {
                if (SetProperty(ref _isAboveCloudsSelected, value) && value)
                {
                    // If Above Clouds is selected, deselect others and update index
                    IsGroundLevelSelected = false;
                    IsCloudLevelSelected = false;
                    CameraPositionIndex = 2;
                }
                else if (!value && !IsGroundLevelSelected && !IsCloudLevelSelected)
                {
                    // Prevent deselecting all - if this one is deselected and no others are selected, reselect this one
                    _isAboveCloudsSelected = true;
                    OnPropertyChanged(nameof(IsAboveCloudsSelected));
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
        public string[] CameraPositionLabels { get; } = new[] { "Ground Level", "Cloud Level", "Above Clouds" };

        // ─── Constructor ──────────────────────────────────────────────────
        public CreateCloudViewModel(
            CloudService cloudService,
            Action onCreated = null,
            Action onCancel  = null)
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

            // Set camera position toggle states
            IsGroundLevelSelected = CameraPositionIndex == 0;
            IsCloudLevelSelected = CameraPositionIndex == 1;
            IsAboveCloudsSelected = CameraPositionIndex == 2;
        }

        // ─── Commands to set toggle‐button indices ─────────────────────────

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
        private void SetCameraPositionIndex(int index)
        {
            CameraPositionIndex = index;
            UpdateToggleButtonStatesFromIndices();
        }

        // ─── GeneratePreviewCommand ───────────────────────────────────────
        [RelayCommand]
        private async Task GeneratePreviewAsync()
        {
            StatusMessage = string.Empty;

            try
            {
                StatusMessage = "Generating preview...";

                // Build a minimal data‐transfer object for your service
                var dto = new CloudPreviewDto
                {
                    CreatedAt           = CreatedDate.DateTime,
                    CloudTypeIndex      = CloudTypeIndex,
                    Altitude            = Altitude,
                    Pressure            = Pressure,
                    Humidity            = Humidity,
                    Temperature         = Temperature,
                    WindSpeed           = WindSpeed,
                    RenderingPresetIndex = RenderingPresetIndex,
                    CameraPositionIndex  = CameraPositionIndex
                };

                // Call service to generate preview (service should return a file path)
                // var previewPath = await _cloudService.GeneratePreviewAsync(dto);
                //
                // PreviewImagePath = previewPath;
                StatusMessage = "Preview generated!";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Preview failed: {ex.Message}";
            }
        }

        // ─── RenderCloudCommand ───────────────────────────────────────────
        [RelayCommand]
        private async Task RenderCloudAsync()
        {
            StatusMessage = string.Empty;

            try
            {
                StatusMessage = "Creating and rendering cloud...";

                var dto = new CloudRenderDto
                {
                    CreatedAt           = CreatedDate.DateTime,
                    CloudTypeIndex      = CloudTypeIndex,
                    Altitude            = Altitude,
                    Pressure            = Pressure,
                    Humidity            = Humidity,
                    Temperature         = Temperature,
                    WindSpeed           = WindSpeed,
                    RenderingPresetIndex = RenderingPresetIndex,
                    CameraPositionIndex  = CameraPositionIndex
                };

                // Add the cloud (service handles mapping indices however it likes)
                // await _cloudService.AddCloudAsync(dto);
                // StatusMessage = "Cloud added. Rendering animation...";
                //
                // await _cloudService.RenderCloudAnimationAsync(dto);
                // StatusMessage = "Cloud rendered successfully!";

                _onCreated?.Invoke();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        // ─── CancelCommand ─────────────────────────────────────────────────
        [RelayCommand]
        private void NavigateToStartPage()
        {
            // Simply invokes the cancel callback, which in your MainWindowViewModel
            // navigates back to the start page.
            _onCancel?.Invoke();
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
        public int CameraPositionIndex { get; set; }
    }

    public class CloudRenderDto : CloudPreviewDto
    {
        // You can add extra fields here if needed
    }
}
