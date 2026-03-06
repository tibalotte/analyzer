using System.Windows;
using System.Windows.Controls;
using FFBAnalyzer.ViewModels;

namespace FFBAnalyzer.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
