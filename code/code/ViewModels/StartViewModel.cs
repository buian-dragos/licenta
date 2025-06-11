using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using code.Services;
using code.Repositories;

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
                onCancel:     () => CurrentViewModel = this               // On cancel, return to start page
            );

            // ─── 4. Initial view is the “Start Page” (this VM) ──────────────
            CurrentViewModel = this;
        }

        // ─── Command: Switch to CreateCloudViewModel ──────────────────────
        [RelayCommand]
        private void NavigateToCreateCloud()
        {
            CurrentViewModel = CreateCloudViewModel;
        }

        // ─── Optional: Explicitly navigate back to “start page” ───────────
        [RelayCommand]
        private void NavigateToStartPage()
        {
            CurrentViewModel = this;
        }
    }
}

