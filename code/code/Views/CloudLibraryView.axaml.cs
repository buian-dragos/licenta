using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity; // Required for RoutedEventArgs
using code.ViewModels;       // Required for CloudLibraryViewModel

namespace code.Views;

public partial class CloudLibraryView : UserControl
{
    public CloudLibraryView()
    {
        InitializeComponent();
    }

    // This method is called when the control is loaded into the visual tree.
    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is CloudLibraryViewModel vm)
        {
            // Check if already initialized if OnLoaded can be called multiple times
            // or manage initialization state within the VM itself if needed.
            await vm.InitializeAsync();
        }
    }
}

