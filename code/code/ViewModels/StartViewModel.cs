using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using code.Services;
using code.Repositories;
using code.Models; // Added for Action<Cloud>
using System;      // Added for Action

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
            // ─── 1. Setup services ───────────────────────────────────────────
            var cloudRepository = new CloudRepository();
            var cloudService = new CloudService(cloudRepository);

            // ─── 2. Instantiate CloudViewModel ──────────────────────────────
            CloudViewModel = new CloudViewModel(cloudService);

            // ─── 3. Instantiate CreateCloudViewModel with navigation callbacks ─
            CreateCloudViewModel = new CreateCloudViewModel(
                cloudService,
                // onCreated is optional, so not passing it is fine if not needed yet
                onCancel:     () => CurrentViewModel = this               // On cancel, return to start page
            );

            // ─── 4. Instantiate CloudLibraryViewModel ───────────────────────
            // Define the action for when a cloud is selected for display
            Action<Cloud> displayCloudAction = (selectedCloud) =>
            {
                // Instantiate CloudDetailViewModel with the selected cloud and navigation callback
                this.CloudDetailViewModel = new CloudDetailViewModel(selectedCloud, cloudService, () => CurrentViewModel = CloudLibraryViewModel);
                CurrentViewModel = this.CloudDetailViewModel; // Navigate to the detail view
            };

            CloudLibraryViewModel = new CloudLibraryViewModel(
                cloudService,                       // Pass the ICloudService instance
                onCancel: () => CurrentViewModel = this, // Action to navigate back
                onDisplayCloud: displayCloudAction      // Action to display a selected cloud
            );

            // ─── 5. Initial view is the “Start Page” (this VM) ──────────────
            CurrentViewModel = this;
        }

        // ─── Command: Switch to CreateCloudViewModel ──────────────────────
        [RelayCommand]
        private void NavigateToCreateCloud()
        {
            CurrentViewModel = CreateCloudViewModel;
        }

        // ─── Command: Switch to CloudLibraryViewModel ─────────────────────
        [RelayCommand]
        private void NavigateToCloudLibrary()
        {
            CurrentViewModel = CloudLibraryViewModel;
        }

        // ─── Optional: Explicitly navigate back to “start page” ───────────
        [RelayCommand]
        private void NavigateToStartPage()
        {
            CurrentViewModel = this;
        }
    }
}
