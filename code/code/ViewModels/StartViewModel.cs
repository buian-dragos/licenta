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
                // Here you would navigate to a view that shows cloud details.
                // For now, let's just print to console or set a placeholder view.
                Console.WriteLine($"Navigate to display cloud: {selectedCloud.Name}");
                // Example: If you have a ViewModel to display a single cloud:
                // var detailViewModel = new CloudDetailViewModel(selectedCloud, cloudService);
                // CurrentViewModel = detailViewModel;
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
