using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFBAnalyzer.Models;

namespace FFBAnalyzer.ViewModels;

public partial class ExportImportViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty] private ObservableCollection<Session> _sessions = new();
    [ObservableProperty] private Session? _selectedSession;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public ExportImportViewModel(MainViewModel main)
    {
        _main = main;
    }

    [RelayCommand]
    public async Task LoadSessionsAsync()
    {
        IsBusy = true;
        var sessions = await _main.Storage.LoadAllSessionsAsync();
        Sessions = new ObservableCollection<Session>(sessions);
        IsBusy = false;
    }

    [RelayCommand]
    public async Task ExportJsonAsync(string filePath)
    {
        if (SelectedSession == null) return;
        IsBusy = true;
        try
        {
            await _main.Exporter.ExportSessionJsonAsync(SelectedSession, filePath);
            StatusMessage = $"Exported to {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
        IsBusy = false;
    }

    [RelayCommand]
    public async Task ExportZipAsync(string filePath)
    {
        if (SelectedSession == null) return;
        IsBusy = true;
        try
        {
            await _main.Exporter.ExportSessionZipAsync(SelectedSession, filePath);
            StatusMessage = $"Exported ZIP to {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
        IsBusy = false;
    }

    [RelayCommand]
    public async Task ImportJsonAsync(string filePath)
    {
        IsBusy = true;
        try
        {
            var session = await _main.Exporter.ImportSessionJsonAsync(filePath);
            if (session != null)
            {
                await _main.Storage.SaveSessionAsync(session);
                Sessions.Insert(0, session);
                StatusMessage = $"Imported session: {session.Name}";
            }
        }
        catch (NotSupportedException ex)
        {
            StatusMessage = $"Incompatible format: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
        }
        IsBusy = false;
    }

    [RelayCommand]
    public async Task ImportZipAsync(string filePath)
    {
        IsBusy = true;
        try
        {
            var session = await _main.Exporter.ImportSessionZipAsync(filePath);
            if (session != null)
            {
                await _main.Storage.SaveSessionAsync(session);
                Sessions.Insert(0, session);
                StatusMessage = $"Imported session: {session.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
        }
        IsBusy = false;
    }

    [RelayCommand]
    public void GoHome() => _main.NavigateHome();
}
