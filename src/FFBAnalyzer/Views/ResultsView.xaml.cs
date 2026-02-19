using System.IO;
using System.Windows;
using System.Windows.Controls;
using FFBAnalyzer.ViewModels;
using Microsoft.Win32;

namespace FFBAnalyzer.Views;

public partial class ResultsView : UserControl
{
    public ResultsView()
    {
        InitializeComponent();
    }

    private async void OnExportCsv(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ResultsViewModel vm) return;

        var dlg = new SaveFileDialog
        {
            Title = "Export Run as CSV",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"run_{vm.Run.RunId:N}.csv",
            DefaultExt = ".csv"
        };

        if (dlg.ShowDialog() == true)
            await vm.ExportCsvAsyncCommand.ExecuteAsync(dlg.FileName);
    }
}
