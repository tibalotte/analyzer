using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFBAnalyzer.Adapters;
using FFBAnalyzer.Models;
using FFBAnalyzer.Services;

namespace FFBAnalyzer.ViewModels;

/// <summary>Root shell view model – owns navigation and shared services.</summary>
public partial class MainViewModel : ObservableObject
{
    // ── Services (injected) ────────────────────────────────────────────────
    public SessionStorageService Storage { get; }
    public ExportService Exporter { get; }
    public IDeviceAdapter DeviceAdapter { get; }

    // ── Navigation ────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableObject? _currentView;

    // ── Child ViewModels ──────────────────────────────────────────────────
    public HomeViewModel HomeVm { get; }

    public MainViewModel(SessionStorageService storage, ExportService exporter, IDeviceAdapter adapter)
    {
        Storage = storage;
        Exporter = exporter;
        DeviceAdapter = adapter;

        HomeVm = new HomeViewModel(this);
        CurrentView = HomeVm;
    }

    // ── Navigation commands ────────────────────────────────────────────────

    [RelayCommand]
    public void NavigateHome() => CurrentView = HomeVm;

    [RelayCommand]
    public void NavigateToSession(Session session)
    {
        var vm = new SessionViewModel(this, session);
        CurrentView = vm;
    }

    [RelayCommand]
    public void NavigateToNewSession()
    {
        var session = new Session { Name = "New Session" };
        NavigateToSession(session);
    }

    [RelayCommand]
    public void NavigateToTestWizard(TestWizardArgs args)
    {
        var vm = new TestWizardViewModel(this, args);
        CurrentView = vm;
    }

    [RelayCommand]
    public void NavigateToResults(Run run)
    {
        var vm = new ResultsViewModel(this, run);
        CurrentView = vm;
    }

    [RelayCommand]
    public void NavigateToComparison(IReadOnlyList<Run> runs)
    {
        var vm = new ComparisonViewModel(this, runs);
        CurrentView = vm;
    }

    [RelayCommand]
    public void NavigateToExportImport()
    {
        var vm = new ExportImportViewModel(this);
        CurrentView = vm;
    }
}

/// <summary>Arguments passed when launching the test wizard.</summary>
public record TestWizardArgs(Session Session, TestDefinition TestDefinition, bool IsBaseline);
