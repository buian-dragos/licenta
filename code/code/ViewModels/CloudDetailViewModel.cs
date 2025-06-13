using Avalonia.Media.Imaging; // Added to resolve Bitmap type
using code.Models;
using code.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO; // Added for Path and Directory
using System.Linq; // Added for OrderBy and ToArray
using System.Collections.Generic; // Added for List<string>

namespace code.ViewModels
{
    public partial class CloudDetailViewModel : ViewModelBase
    {
        private readonly Cloud _cloud;
        private readonly ICloudService _cloudService;
        private readonly Action _onNavigateBack;

        private List<string> _framePaths = new List<string>();

        [ObservableProperty]
        private string _cloudName;

        [ObservableProperty]
        private string _cloudType;

        [ObservableProperty]
        private double _altitude;

        [ObservableProperty]
        private double _temperature;

        [ObservableProperty]
        private double _pressure;

        [ObservableProperty]
        private double _windSpeed;

        [ObservableProperty]
        private double _humidity;

        [ObservableProperty]
        private string _renderingPreset;

        [ObservableProperty]
        private string _renderEngine;

        [ObservableProperty]
        private string _createdAt;

        [ObservableProperty]
        private Bitmap? _previewImage;

        [ObservableProperty]
        private double _imageScale = 1.0;

        [ObservableProperty]
        private int _maxFrameIndex = 0;

        [ObservableProperty]
        private int _currentFrameIndex = 0;

        [ObservableProperty] // Added for slider visibility
        private bool _isFrameSliderVisible = false;

        private const double ZoomStep = 0.1;
        private const double MinZoom = 0.5;
        private const double MaxZoom = 3.0;

        // Design-time constructor
        public CloudDetailViewModel()
        {
            if (!Avalonia.Controls.Design.IsDesignMode)
            {
                throw new InvalidOperationException("This constructor is intended for design-time use only.");
            }

            // Initialize properties with default/sample values for XAML preview
            // No dummy Cloud object is created here.
            CloudName = "Sample Cloud (Design)";
            CloudType = Models.CloudType.Cumulus.ToString(); // Using actual enum for consistency
            Altitude = 2.5;
            Temperature = 15.0;
            Pressure = 1012.0;
            WindSpeed = 5.5;
            Humidity = 60.0;
            RenderingPreset = Models.RenderingPreset.Quality.ToString();
            RenderEngine = Models.RenderEngineType.CPU.ToString();
            CreatedAt = DateTime.Now.ToString("g");
            PreviewImage = null; // No preview image at design time by default
            // Design-time values for slider
            MaxFrameIndex = 0; // Or a sample value like 99 if you want to see the slider enabled
            CurrentFrameIndex = 0;
            IsFrameSliderVisible = false; // Or true if MaxFrameIndex > 0 for design
            
            // _cloud, _cloudService, and _onNavigateBack remain uninitialized (null) in design mode,
            // as they are not typically used by the XAML previewer for basic property display.
        }

        public CloudDetailViewModel(Cloud cloud, ICloudService cloudService, Action onNavigateBack)
        {
            _cloud = cloud ?? throw new ArgumentNullException(nameof(cloud));
            _cloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
            _onNavigateBack = onNavigateBack ?? throw new ArgumentNullException(nameof(onNavigateBack));

            CloudName = _cloud.Name;
            CloudType = _cloud.Type.ToString();
            Altitude = _cloud.Altitude;
            Temperature = _cloud.Temperature;
            Pressure = _cloud.Pressure;
            WindSpeed = _cloud.WindSpeed;
            Humidity = _cloud.Humidity;
            RenderingPreset = _cloud.RenderingPreset.ToString();
            RenderEngine = _cloud.RenderEngine.ToString();
            CreatedAt = _cloud.CreatedAt.ToString("g"); // General date/time pattern (short time)

            LoadFramesAndInitialPreview();
        }

        private void LoadFramesAndInitialPreview()
        {
            _framePaths.Clear();
            MaxFrameIndex = 0;
            CurrentFrameIndex = 0;

            if (_cloud == null || string.IsNullOrEmpty(_cloud.StoragePath))
            {
                PreviewImage = null;
                IsFrameSliderVisible = false;
                return;
            }

            string framesDirectory = Path.Combine(_cloud.StoragePath, "frames");
            if (Directory.Exists(framesDirectory))
            {
                _framePaths = Directory.GetFiles(framesDirectory, "*.png") // Assuming PNG frames
                                     .OrderBy(f => f) // Ensure natural sort order
                                     .ToList();

                if (_framePaths.Any())
                {
                    MaxFrameIndex = _framePaths.Count - 1;
                    IsFrameSliderVisible = _framePaths.Count > 1; // Set visibility
                    LoadFrameAtIndex(CurrentFrameIndex); // Load the first frame
                }
                else
                {
                    PreviewImage = null; // No frames found
                    IsFrameSliderVisible = false;
                }
            }
            else
            {
                // If frames directory doesn't exist, try loading the single PreviewImagePath as fallback
                LoadSinglePreviewImage(); 
                IsFrameSliderVisible = false; // No frames to slide through
            }
        }

        private void LoadSinglePreviewImage()
        {
            if (_cloud == null || string.IsNullOrEmpty(_cloud.PreviewImagePath) || !System.IO.File.Exists(_cloud.PreviewImagePath))
            {
                PreviewImage = null;
                return;
            }
            try
            {
                PreviewImage = new Avalonia.Media.Imaging.Bitmap(_cloud.PreviewImagePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading single preview image for Cloud ID {_cloud.Id} in DetailView from path '{_cloud.PreviewImagePath}': {ex.Message}");
                PreviewImage = null;
            }
        }

        private void LoadFrameAtIndex(int index)
        {
            if (index >= 0 && index < _framePaths.Count)
            {
                string framePath = _framePaths[index];
                if (File.Exists(framePath))
                {
                    try
                    {
                        PreviewImage = new Bitmap(framePath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading frame '{framePath}' for Cloud ID {_cloud.Id}: {ex.Message}");
                        PreviewImage = null;
                    }
                }
                else
                {
                    PreviewImage = null;
                }
            }
            else
            {
                PreviewImage = null; // Index out of bounds
            }
        }

        partial void OnCurrentFrameIndexChanged(int value)
        {
            LoadFrameAtIndex(value);
        }

        [RelayCommand]
        private void NavigateBack()
        {
            _onNavigateBack?.Invoke();
        }

        [RelayCommand]
        private void ZoomIn()
        {
            ImageScale = Math.Min(MaxZoom, ImageScale + ZoomStep);
        }

        [RelayCommand]
        private void ZoomOut()
        {
            ImageScale = Math.Max(MinZoom, ImageScale - ZoomStep);
        }
    }
}
