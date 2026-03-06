using System.IO;
using System.Windows;
using System.Windows.Controls;
using FFBAnalyzer.ViewModels;
using Microsoft.Win32;

namespace FFBAnalyzer.Views;

public partial class ExportImportView : UserControl
{
    public ExportImportView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ExportImportViewModel vm)
            await vm.LoadSessionsCommand.ExecuteAsync(null);
    }

    private async void OnImportJson(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ExportImportViewModel vm) return;
        var dlg = new OpenFileDialog
        {
            Title = "Import Session JSON",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            await vm.ImportJsonCommand.ExecuteAsync(dlg.FileName);
    }

    private async void OnImportZip(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ExportImportViewModel vm) return;
        var dlg = new OpenFileDialog
        {
            Title = "Import Session ZIP",
            Filter = "ZIP files (*.zip)|*.zip|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            await vm.ImportZipCommand.ExecuteAsync(dlg.FileName);
    }

    private async void OnExportJson(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ExportImportViewModel vm) return;
        if (vm.SelectedSession == null)
        {
            MessageBox.Show("Select a session first.", "FFB Analyzer",
                            MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new SaveFileDialog
        {
            Title = "Export Session as JSON",
            Filter = "JSON files (*.json)|*.json",
            FileName = $"session_{vm.SelectedSession.SessionId:N}.json",
            DefaultExt = ".json"
        };
        if (dlg.ShowDialog() == true)
            await vm.ExportJsonCommand.ExecuteAsync(dlg.FileName);
    }

    private async void OnExportZip(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ExportImportViewModel vm) return;
        if (vm.SelectedSession == null)
        {
            MessageBox.Show("Select a session first.", "FFB Analyzer",
                            MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new SaveFileDialog
        {
            Title = "Export Session as ZIP",
            Filter = "ZIP files (*.zip)|*.zip",
            FileName = $"session_{vm.SelectedSession.SessionId:N}.zip",
            DefaultExt = ".zip"
        };
        if (dlg.ShowDialog() == true)
            await vm.ExportZipCommand.ExecuteAsync(dlg.FileName);
    }
}
