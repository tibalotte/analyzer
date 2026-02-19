using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFBAnalyzer.Adapters;
using FFBAnalyzer.Models;

namespace FFBAnalyzer.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty] private ObservableCollection<Device> _devices = new();
    [ObservableProperty] private Device? _selectedDevice;
    [ObservableProperty] private ObservableCollection<Session> _recentSessions = new();
    [ObservableProperty] private string _statusMessage = "Detecting devices…";
    [ObservableProperty] private bool _isBusy;

    public HomeViewModel(MainViewModel main)
    {
        _main = main;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Scanning for FFB devices…";

        try
        {
            var devices = await _main.DeviceAdapter.EnumerateDevicesAsync();
            Devices = new ObservableCollection<Device>(devices);
            SelectedDevice = Devices.FirstOrDefault();
            StatusMessage = devices.Count == 0
                ? "No FFB devices found. Connect a wheel and refresh."
                : $"{devices.Count} device(s) detected.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Device scan failed: {ex.Message}";
        }

        try
        {
            var sessions = await _main.Storage.LoadAllSessionsAsync();
            RecentSessions = new ObservableCollection<Session>(sessions.Take(10));
        }
        catch { /* non-critical */ }

        IsBusy = false;
    }

    [RelayCommand]
    public async Task RefreshDevicesAsync()
    {
        IsBusy = true;
        StatusMessage = "Refreshing…";
        var devices = await _main.DeviceAdapter.EnumerateDevicesAsync();
        Devices = new ObservableCollection<Device>(devices);
        SelectedDevice = Devices.FirstOrDefault();
        StatusMessage = $"{devices.Count} device(s) found.";
        IsBusy = false;
    }

    [RelayCommand]
    public void OpenSession(Session session) => _main.NavigateToSession(session);

    [RelayCommand]
    public void NewSession() => _main.NavigateToNewSession();

    [RelayCommand]
    public void OpenExportImport() => _main.NavigateToExportImport();
}
