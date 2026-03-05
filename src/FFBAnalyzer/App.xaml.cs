using System.IO;
using System.Windows;
using FFBAnalyzer.Adapters;
using FFBAnalyzer.Services;
using FFBAnalyzer.ViewModels;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace FFBAnalyzer;

public partial class App : Application
{
    private MainViewModel? _mainVm;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize LiveChartsCore with SkiaSharp renderer
        LiveCharts.Configure(config => config
            .AddSkiaSharp()
            .AddDefaultMappers());

        string dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FFBAnalyzer");
        string dbPath = Path.Combine(dataDir, "ffbanalyzer.db");

        var storage = new SessionStorageService(dbPath);
        var exporter = new ExportService();
        var adapter = new CompositeDeviceAdapter(new DirectInputAdapter(), new SimulatedDeviceAdapter());

        _mainVm = new MainViewModel(storage, exporter, adapter);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"Unhandled error:\n\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                "FFB Analyzer – Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

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
