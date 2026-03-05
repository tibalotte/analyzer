using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFBAnalyzer.Models;

namespace FFBAnalyzer.ViewModels;

public partial class SessionViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty] private Session _session;
    [ObservableProperty] private string _sessionName;
    [ObservableProperty] private ObservableCollection<Run> _runs = new();
    [ObservableProperty] private Run? _selectedRun;
    [ObservableProperty] private TestBattery _selectedBattery;
    [ObservableProperty] private ObservableCollection<TestBattery> _availableBatteries;

    public SessionViewModel(MainViewModel main, Session session)
    {
        _main = main;
        _session = session;
        _sessionName = session.Name;
        _runs = new ObservableCollection<Run>(session.Runs);
        _selectedBattery = TestBattery.Quick2Min();
        _availableBatteries = new ObservableCollection<TestBattery>
        {
            TestBattery.Quick2Min(),
            TestBattery.Standard5Min(),
            TestBattery.Deep12Min()
        };
    }

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task StartBaselineAsync()
    {
        Session.Name = SessionName;
        foreach (var testDef in SelectedBattery.Tests)
        {
            var args = new TestWizardArgs(Session, testDef, IsBaseline: true);
            _main.NavigateToTestWizard(args);
            // Navigation is immediate; results will be appended on return
            return; // wizard navigates back when done
        }
    }

    [RelayCommand]
    public void StartNewRun(TestDefinition testDef)
    {
        Session.Name = SessionName;
        var args = new TestWizardArgs(Session, testDef, IsBaseline: false);
        _main.NavigateToTestWizard(args);
    }

    [RelayCommand]
    public void ViewRun(Run run) => _main.NavigateToResults(run);

    [RelayCommand]
    public void CompareRuns()
    {
        var toCompare = Runs.Where(r => !r.WasAborted).ToList();
        if (toCompare.Count < 2) return;
        _main.NavigateToComparison(toCompare);
    }

    [RelayCommand]
    public async Task SaveSessionAsync()
    {
        Session.Name = SessionName;
        Session.UpdatedAt = DateTime.UtcNow;
        await _main.Storage.SaveSessionAsync(Session);
    }

    [RelayCommand]
    public async Task ExportJsonAsync(string filePath)
    {
        Session.Name = SessionName;
        await _main.Exporter.ExportSessionJsonAsync(Session, filePath);
    }

    [RelayCommand]
    public async Task ExportZipAsync(string filePath)
    {
        Session.Name = SessionName;
        await _main.Exporter.ExportSessionZipAsync(Session, filePath);
    }

    [RelayCommand]
    public void GoHome() => _main.NavigateHome();

    // ── Called by TestWizardViewModel after completion ─────────────────────

    public void OnRunCompleted(Run run)
    {
        if (!Session.Runs.Contains(run))
            Session.Runs.Add(run);
        if (!Runs.Contains(run))
            Runs.Add(run);
    }
}
