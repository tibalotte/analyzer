using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFBAnalyzer.Models;
using FFBAnalyzer.Services;

namespace FFBAnalyzer.ViewModels;

/// <summary>States for the guided test wizard.</summary>
public enum WizardState { Instructions, Countdown, Running, Completed, Aborted }

public partial class TestWizardViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly TestWizardArgs _args;
    private TestRunnerService? _runner;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private WizardState _state = WizardState.Instructions;
    [ObservableProperty] private int _countdown = 3;
    [ObservableProperty] private double _progressFraction;
    [ObservableProperty] private double _currentForce;
    [ObservableProperty] private double _elapsedSec;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private Run? _completedRun;

    // Safety
    [ObservableProperty] private double _maxIntensity = 0.80;

    public TestDefinition TestDefinition => _args.TestDefinition;
    public bool IsBaseline => _args.IsBaseline;

    // Instruction text for each test type
    public string Instructions => _args.TestDefinition.Type switch
    {
        TestType.StepResponse      => "Hold the wheel loosely – do NOT grip hard. The wheel may push briefly.",
        TestType.SineSweep         => "Let the wheel move freely. Keep hands close but do NOT resist the motion.",
        TestType.Chirp             => "Let the wheel move freely. The frequency will increase rapidly.",
        TestType.SquareWave        => "Let the wheel move freely. You will feel a repeating side-to-side force.",
        TestType.Impulse           => "Release the wheel completely. A brief impulse will fire.",
        TestType.ConstantTorque    => "Hold the wheel with light pressure to observe steady-state behavior.",
        TestType.FrictionEmulation => "Let the wheel move freely. Very small forces will be applied.",
        _                          => "Follow safety guidelines. Keep hands ready to grab the wheel."
    };

    public TestWizardViewModel(MainViewModel main, TestWizardArgs args)
    {
        _main = main;
        _args = args;
        _runner = new TestRunnerService(main.DeviceAdapter)
        {
            MaxIntensityLimit = _maxIntensity
        };
        _runner.Progress += OnProgress;
    }

    [RelayCommand]
    public async Task StartCountdownAsync()
    {
        State = WizardState.Countdown;
        for (int i = 3; i >= 1; i--)
        {
            Countdown = i;
            await Task.Delay(1000);
        }
        await RunTestAsync();
    }

    [RelayCommand]
    public void EmergencyStop()
    {
        _cts?.Cancel();
        _runner?.Abort();
        State = WizardState.Aborted;
        StatusMessage = "Test stopped.";
    }

    [RelayCommand]
    public void ViewResults()
    {
        if (CompletedRun != null)
            _main.NavigateToResults(CompletedRun);
    }

    [RelayCommand]
    public void BackToSession()
    {
        // Pop back – in this simplified nav model we just go home
        _main.NavigateHome();
    }

    private async Task RunTestAsync()
    {
        State = WizardState.Running;
        StatusMessage = "Running…";
        _cts = new CancellationTokenSource();

        var run = new Run
        {
            SessionId = _args.Session.SessionId,
            TestId = _args.TestDefinition.TestId,
            TestDefinition = _args.TestDefinition,
            Device = _main.HomeVm.SelectedDevice ?? new Device(),
            IsBaseline = _args.IsBaseline,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            if (_runner == null) return;
            await _runner.ExecuteAsync(run, _cts.Token);

            if (run.WasAborted)
            {
                State = WizardState.Aborted;
                StatusMessage = "Test was aborted.";
                return;
            }

            CompletedRun = run;
            _args.Session.Runs.Add(run);
            await _main.Storage.SaveSessionAsync(_args.Session);

            State = WizardState.Completed;
            StatusMessage = run.ClippingDetected
                ? "Test completed – clipping detected! Reduce intensity."
                : "Test completed successfully.";
        }
        catch (OperationCanceledException)
        {
            State = WizardState.Aborted;
            StatusMessage = "Test cancelled.";
        }
        catch (Exception ex)
        {
            State = WizardState.Aborted;
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private void OnProgress(object? sender, TestProgressEventArgs e)
    {
        // Marshal to UI thread is handled by the View
        ProgressFraction = e.ProgressFraction;
        CurrentForce = e.CurrentForce;
        ElapsedSec = e.ElapsedSec;
    }

    public void Cleanup()
    {
        if (_runner != null)
            _runner.Progress -= OnProgress;
        _runner?.Dispose();
        _runner = null;
        _cts?.Dispose();
    }
}
