using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using code.Services;
using code.Models;
using System.Collections.Generic;

namespace code.ViewModels
{
    public partial class CloudLibraryViewModel : ViewModelBase
    {
        private readonly ICloudService? _cloudService; // Nullable for design-time
        private readonly Action? _onCancel;
        private readonly Action<Cloud>? _onDisplayCloud;

        private List<CloudItemViewModel> _allCloudItemsCache = new();

        [ObservableProperty]
        private ObservableCollection<CloudItemViewModel> _cloudItems = new();

        [ObservableProperty]
        private bool _isLoading = true;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _totalPages = 1;

        private const int PageSize = 4;

        public bool IsStatusMessageVisible => !string.IsNullOrEmpty(StatusMessage);
        public bool AreCloudItemsVisible => !IsLoading && string.IsNullOrEmpty(StatusMessage); // Or rather !IsStatusMessageVisible
        public bool IsPaginationVisible => !IsLoading && TotalPages > 1;

        // Design-time constructor
        public CloudLibraryViewModel()
        {
            if (Avalonia.Controls.Design.IsDesignMode)
            {
                _isLoading = false;
                _statusMessage = "Design Mode - No Clouds Loaded";
                _allCloudItemsCache = new List<CloudItemViewModel>
                {
                    new CloudItemViewModel { Name = "Design Cloud 1 (Fast)", CloudTypeString = "Cumulus", CreatedAtDisplay = "01/01/2024 10:00" },
                    new CloudItemViewModel { Name = "Design Cloud 2 (Quality)", CloudTypeString = "Stratus", CreatedAtDisplay = "02/15/2024 14:30" },
                    new CloudItemViewModel { Name = "Design Cloud 3", CloudTypeString = "Cirrus", CreatedAtDisplay = "03/20/2024 08:15" },
                    new CloudItemViewModel { Name = "Design Cloud 4", CloudTypeString = "Altocumulus", CreatedAtDisplay = "04/10/2024 18:45" }
                };
                CurrentPage = 1;
                UpdatePagedCloudItems();
                 // Explicitly trigger property changed for calculated properties if needed after setup
                OnPropertyChanged(nameof(IsStatusMessageVisible));
                OnPropertyChanged(nameof(AreCloudItemsVisible));
            }
            else
            {
                // This should ideally not be called by runtime DI without parameters
                // Or throw if _cloudService is null when not in design mode.
                 _isLoading = true; // Default for runtime
                 _statusMessage = "Initializing...";
            }
        }

        public CloudLibraryViewModel(ICloudService cloudService, Action? onCancel, Action<Cloud> onDisplayCloud)
        {
            _cloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
            _onCancel = onCancel;
            _onDisplayCloud = onDisplayCloud ?? throw new ArgumentNullException(nameof(onDisplayCloud));
            // Initial load is triggered by InitializeAsync
        }

        private void UpdatePagedCloudItems()
        {
            if (_allCloudItemsCache == null) return;

            TotalPages = (int)Math.Ceiling((double)_allCloudItemsCache.Count / PageSize);
            if (TotalPages == 0) TotalPages = 1;
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var pagedClouds = _allCloudItemsCache.Skip((CurrentPage - 1) * PageSize).Take(PageSize);

            CloudItems.Clear();
            foreach (var vm in pagedClouds)
            {
                CloudItems.Add(vm);
            }

            if (!_allCloudItemsCache.Any() && !Avalonia.Controls.Design.IsDesignMode) // Don't show "No clouds" in design mode if we have design items
            {
                StatusMessage = "No clouds found in the library.";
            }
            else if (!CloudItems.Any() && _allCloudItemsCache.Any())
            {
                StatusMessage = $"No clouds on page {CurrentPage}. Try a different page.";
            }
            else if (!Avalonia.Controls.Design.IsDesignMode || _allCloudItemsCache.Any()) // Clear status if clouds are loaded or design items exist
            {
                StatusMessage = string.Empty;
            }
            // For design mode, status message is set in constructor
        }

        [RelayCommand]
        private async Task LoadCloudsAsync()
        {
            if (_cloudService == null)
            {
                StatusMessage = "Error: Cloud service not available.";
                IsLoading = false;
                return;
            }

            IsLoading = true;
            StatusMessage = "Loading clouds...";
            try
            {
                var allClouds = await _cloudService.GetAllCloudsAsync();
                _allCloudItemsCache = allClouds.Select(c => new CloudItemViewModel(c)).ToList();
                
                CurrentPage = 1;
                UpdatePagedCloudItems();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading clouds: {ex.Message}");
                StatusMessage = "Error loading clouds. Please try again.";
                _allCloudItemsCache.Clear();
                CloudItems.Clear();
                TotalPages = 1;
                CurrentPage = 1;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void NavigateBack()
        {
            _onCancel?.Invoke();
        }

        [RelayCommand]
        private void DisplayCloud(CloudItemViewModel? cloudItem)
        {
            if (cloudItem?.CloudModel != null)
            {
                _onDisplayCloud?.Invoke(cloudItem.CloudModel);
            }
        }

        [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
        private void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                UpdatePagedCloudItems();
            }
        }
        private bool CanGoToNextPage() => CurrentPage < TotalPages && !IsLoading;

        [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
        private void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                UpdatePagedCloudItems();
            }
        }
        private bool CanGoToPreviousPage() => CurrentPage > 1 && !IsLoading;
        
        [RelayCommand]
        private void GoToPage(object? pageNumber)
        {
            if (pageNumber is int page && page >= 1 && page <= TotalPages)
            {
                CurrentPage = page;
                UpdatePagedCloudItems();
            }
            else if (pageNumber is string pageStr && int.TryParse(pageStr, out int pageInt) && pageInt >=1 && pageInt <= TotalPages)
            {
                CurrentPage = pageInt;
                UpdatePagedCloudItems();
            }
        }

        [RelayCommand]
        private async Task DeleteCloudAsync(CloudItemViewModel? cloudItem)
        {
            if (cloudItem == null) return;

            // Optional: Add a confirmation dialog here before deleting
            // For now, we proceed directly with deletion.

            IsLoading = true; // Use IsLoading to disable other actions during delete
            StatusMessage = $"Deleting cloud '{cloudItem.Name}'...";

            try
            {
                await _cloudService.DeleteCloudAsync(cloudItem.Id);
                StatusMessage = $"Cloud '{cloudItem.Name}' deleted successfully.";

                // Remove from cache
                var itemToRemove = _allCloudItemsCache.FirstOrDefault(c => c.Id == cloudItem.Id);
                if (itemToRemove != null)
                {
                    _allCloudItemsCache.Remove(itemToRemove);
                }

                // Refresh the current page view
                UpdatePagedCloudItems(); 
                // If the current page becomes empty and it wasn't the first page, try to go to previous page
                if (!CloudItems.Any() && CurrentPage > 1)
                {
                    CurrentPage--;
                    UpdatePagedCloudItems();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting cloud {cloudItem.Id}: {ex.Message}");
                StatusMessage = $"Error deleting cloud '{cloudItem.Name}'. Please try again.";
            }
            finally
            {
                IsLoading = false; 
            }
        }

        public async Task InitializeAsync()
        {
            if (!Avalonia.Controls.Design.IsDesignMode) // Don't load from service in design mode
            {
                await LoadCloudsAsync();
            }
            // Ensure pagination visibility is updated after initial load or design mode setup
            OnPropertyChanged(nameof(IsPaginationVisible));
        }

        partial void OnStatusMessageChanged(string value)
        {
            OnPropertyChanged(nameof(IsStatusMessageVisible));
            OnPropertyChanged(nameof(AreCloudItemsVisible));
        }

        partial void OnIsLoadingChanged(bool value)
        {
            NextPageCommand.NotifyCanExecuteChanged();
            PreviousPageCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(AreCloudItemsVisible));
            OnPropertyChanged(nameof(IsStatusMessageVisible));
            OnPropertyChanged(nameof(IsPaginationVisible)); // Update pagination visibility
        }

        partial void OnCurrentPageChanged(int value)
        {
            NextPageCommand.NotifyCanExecuteChanged();
            PreviousPageCommand.NotifyCanExecuteChanged();
        }

        partial void OnTotalPagesChanged(int value)
        {
            NextPageCommand.NotifyCanExecuteChanged();
            PreviousPageCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(IsPaginationVisible)); // Update pagination visibility
        }
    }
}
