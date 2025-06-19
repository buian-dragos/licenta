using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using code.Services;
using code.Repositories;
using code.Models; // Added for Action<Cloud>
using System;     

namespace code.ViewModels
{
    public partial class StartViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _greeting = "Welcome to Cloud Raytracing!";

        [ObservableProperty]
        private ViewModelBase _currentViewModel;

        public CloudViewModel CloudViewModel { get; }
        public CreateCloudViewModel CreateCloudViewModel { get; }
        public CloudLibraryViewModel CloudLibraryViewModel { get; }
        public CloudDetailViewModel? CloudDetailViewModel { get; private set; } // Property to hold the instance

        public StartViewModel()
        {
            var cloudRepository = new CloudRepository();
            var cloudService = new CloudService(cloudRepository);

            CloudViewModel = new CloudViewModel(cloudService);

            CreateCloudViewModel = new CreateCloudViewModel(
                cloudService,
                onCancel:     () => CurrentViewModel = this
            );

            
            Action<Cloud> displayCloudAction = (selectedCloud) =>
            {
                this.CloudDetailViewModel = new CloudDetailViewModel(selectedCloud, cloudService, () => CurrentViewModel = CloudLibraryViewModel);
                CurrentViewModel = this.CloudDetailViewModel; // Navigate to the detail view
            };

            CloudLibraryViewModel = new CloudLibraryViewModel(
                cloudService,                       // Pass the ICloudService instance
                onCancel: () => CurrentViewModel = this, // Action to navigate back
                onDisplayCloud: displayCloudAction      // Action to display a selected cloud
            );

            CurrentViewModel = this;
        }

        [RelayCommand]
        private void NavigateToCreateCloud()
        {
            CurrentViewModel = CreateCloudViewModel;
        }

        [RelayCommand]
        private void NavigateToCloudLibrary()
        {
            CurrentViewModel = CloudLibraryViewModel;
        }

        [RelayCommand]
        private void NavigateToStartPage()
        {
            CurrentViewModel = this;
        }
    }
}
