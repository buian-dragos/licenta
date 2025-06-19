using Avalonia.Media.Imaging;
using code.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace code.ViewModels
{
    public partial class CloudItemViewModel : ViewModelBase
    {
        private readonly Cloud? _cloud;

        public int Id => _cloud?.Id ?? 0;

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _cloudTypeString;

        [ObservableProperty]
        private string _createdAtDisplay;

        [ObservableProperty]
        private Bitmap? _previewImage;

        public Cloud? CloudModel => _cloud;

        public CloudItemViewModel()
        {
            _name = "Sample Cloud Name";
            _cloudTypeString = "Sample Type";
            _createdAtDisplay = "Sample Date";
        }

        public CloudItemViewModel(Cloud cloud)
        {
            _cloud = cloud ?? throw new ArgumentNullException(nameof(cloud));
            Name = _cloud.Name; 
            CloudTypeString = _cloud.Type.ToString();
            CreatedAtDisplay = _cloud.CreatedAt.ToString("g");
            LoadPreviewImage();
        }

        private void LoadPreviewImage()
        {
            if (_cloud == null || string.IsNullOrEmpty(_cloud.PreviewImagePath) || !File.Exists(_cloud.PreviewImagePath))
            {
                // Console.WriteLine($"Preview image path is null, empty, or file does not exist for Cloud ID {Id}. Path: '{_cloud?.PreviewImagePath}'");
                PreviewImage = null; 
                return;
            }
            try
            {
                PreviewImage = new Bitmap(_cloud.PreviewImagePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading preview image for Cloud ID {Id} from path '{_cloud.PreviewImagePath}': {ex.Message}");
                PreviewImage = null;
            }
        }
    }
}
