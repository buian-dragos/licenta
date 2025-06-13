using Avalonia.Media.Imaging;
using code.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace code.ViewModels
{
    public partial class CloudItemViewModel : ViewModelBase
    {
        private readonly Cloud? _cloud; // Nullable for design time

        public int Id => _cloud?.Id ?? 0; // Design-time Id will be 0

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _cloudTypeString;

        [ObservableProperty]
        private string _createdAtDisplay;

        [ObservableProperty]
        private Bitmap? _previewImage;

        public Cloud? CloudModel => _cloud;

        // Design-time constructor
        public CloudItemViewModel()
        {
            // Initialize with placeholder values for XAML preview
            _name = "Sample Cloud Name";
            _cloudTypeString = "Sample Type";
            _createdAtDisplay = "Sample Date";
            // _previewImage can be null or a placeholder Bitmap if available/needed for design
        }

        public CloudItemViewModel(Cloud cloud)
        {
            _cloud = cloud ?? throw new ArgumentNullException(nameof(cloud));
            Name = _cloud.Name; // Uses the setter of the ObservableProperty
            CloudTypeString = _cloud.Type.ToString(); // Uses the setter
            CreatedAtDisplay = _cloud.CreatedAt.ToString("g"); // Uses the setter
            LoadPreviewImage(); // This should set PreviewImage property
        }

        private void LoadPreviewImage()
        {
            if (_cloud == null || string.IsNullOrEmpty(_cloud.PreviewImagePath) || !File.Exists(_cloud.PreviewImagePath))
            {
                // Console.WriteLine($"Preview image path is null, empty, or file does not exist for Cloud ID {Id}. Path: '{_cloud?.PreviewImagePath}'");
                PreviewImage = null; // Make sure this is the ObservableProperty
                return;
            }
            try
            {
                PreviewImage = new Bitmap(_cloud.PreviewImagePath); // Sets the ObservableProperty
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading preview image for Cloud ID {Id} from path '{_cloud.PreviewImagePath}': {ex.Message}");
                PreviewImage = null;
            }
        }
    }
}
