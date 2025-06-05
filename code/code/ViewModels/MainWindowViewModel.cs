using CommunityToolkit.Mvvm.ComponentModel;
using code.Services;
using code.Repositories;
namespace code.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _greeting = "Welcome to Cloud Raytracing!";
        [ObservableProperty] 
        private ViewModelBase _currentViewModel;
        public CloudViewModel CloudViewModel { get; }
        public MainWindowViewModel()
        {
            // Create repositories, services, and view models with proper dependency injection
            var cloudRepository = new CloudRepository();
            var cloudService = new CloudService(cloudRepository);
            // Create and keep reference to the CloudViewModel
            CloudViewModel = new CloudViewModel(cloudService);
            // Set the cloud view model as the default active view
            CurrentViewModel = CloudViewModel;
        }
    }
}
