using System.IO;
using System.Windows;
using FFBAnalyzer.Adapters;
using FFBAnalyzer.Services;
using FFBAnalyzer.ViewModels;

namespace FFBAnalyzer;

public partial class App : Application
{
    private MainViewModel? _mainVm;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FFBAnalyzer");
        string dbPath = Path.Combine(dataDir, "ffbanalyzer.db");

        var storage = new SessionStorageService(dbPath);
        var exporter = new ExportService();
        var adapter = new DirectInputAdapter();

        _mainVm = new MainViewModel(storage, exporter, adapter);

        var mainWindow = new MainWindow { DataContext = _mainVm };
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // Ensure FFB is stopped
        if (_mainVm != null)
        {
            try { _mainVm.DeviceAdapter.EmergencyStop(); } catch { }
            await _mainVm.DeviceAdapter.DisposeAsync();
        }
        base.OnExit(e);
    }
}
