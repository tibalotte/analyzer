using System.Diagnostics;
using FFBAnalyzer.Adapters;
using FFBAnalyzer.Models;

namespace FFBAnalyzer.Services;

/// <summary>Progress event args emitted during a test run.</summary>
public class TestProgressEventArgs : EventArgs
{
    public double ProgressFraction { get; init; }  // [0, 1]
    public double CurrentForce { get; init; }
    public double ElapsedSec { get; init; }
}

/// <summary>
/// Orchestrates the execution of an FFB test:
/// – sends the command signal at the correct rate
/// – records commanded + measured data
/// – calls MetricsService on completion
/// – enforces safety limits
/// </summary>
public sealed class TestRunnerService : IDisposable
{
    private readonly IDeviceAdapter _adapter;
    private CancellationTokenSource? _cts;

    /// <summary>Raised on every recorded sample (throttled to UI rate).</summary>
    public event EventHandler<TestProgressEventArgs>? Progress;

    /// <summary>Maximum allowed intensity (safety gate). Default 80%.</summary>
    public double MaxIntensityLimit { get; set; } = 0.80;

    public TestRunnerService(IDeviceAdapter adapter)
    {
        _adapter = adapter;
    }

    /// <summary>
    /// Execute the test defined in <paramref name="run"/> and populate its Data
    /// and Metrics fields.
    /// </summary>
    public async Task ExecuteAsync(Run run, CancellationToken cancellationToken = default)
    {
        var def = run.TestDefinition;

        // Safety gate
        if (def.Intensity > MaxIntensityLimit)
            throw new InvalidOperationException(
                $"Test intensity {def.Intensity:P0} exceeds safety limit {MaxIntensityLimit:P0}.");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;

        run.WasAborted = false;
        run.Data = new TimeSeries();

        // Combine repetitions if needed
        for (int rep = 0; rep < def.Repetitions && !token.IsCancellationRequested; rep++)
        {
            var repData = await ExecuteSingleRepetitionAsync(def, run.Data, token);
            if (rep == 0)
                run.Data = repData;
            else
                MergeRepetition(run.Data, repData);
        }

        // Ensure FFB is off
        _adapter.SetForce(0);

        if (token.IsCancellationRequested)
        {
            run.WasAborted = true;
            return;
        }

        // Compute metrics
        run.Metrics = MetricsService.Compute(run);
        run.ClippingDetected = run.Metrics.Metrics.Any(m => m.Key == "clipping");
    }

    /// <summary>Immediately abort any running test.</summary>
    public void Abort()
    {
        _cts?.Cancel();
        _adapter.EmergencyStop();
    }

    private async Task<TimeSeries> ExecuteSingleRepetitionAsync(
        TestDefinition def, TimeSeries existing, CancellationToken token)
    {
        var signal = FFBSignalGenerator.Generate(def);
        var ts = new TimeSeries();
        ts.Samples.Capacity = signal.Count;

        double intervalMs = 1000.0 / def.SampleRateHz;
        var sw = Stopwatch.StartNew();
        int uiThrottle = Math.Max(1, def.SampleRateHz / 30); // report to UI at ~30 Hz
        int uiCounter = 0;

        for (int i = 0; i < signal.Count && !token.IsCancellationRequested; i++)
        {
            var (timeS, force) = signal[i];

            // Send to device
            _adapter.SetForce(force);

            // Read telemetry
            var telem = _adapter.ReadTelemetry();

            ts.Samples.Add(new Sample
            {
                TimeS = timeS,
                CommandedForce = force,
                MeasuredPosition = _adapter.AvailableTelemetryMode != TelemetryMode.SoftwareOnly
                    ? telem.Position
                    : null,
                MeasuredForce = telem.TorqueNm,
                MeasuredVelocity = telem.VelocityDegS
            });

            // UI progress
            if (++uiCounter >= uiThrottle)
            {
                uiCounter = 0;
                Progress?.Invoke(this, new TestProgressEventArgs
                {
                    ProgressFraction = (double)i / signal.Count,
                    CurrentForce = force,
                    ElapsedSec = sw.Elapsed.TotalSeconds
                });
            }

            // Timing: sleep to maintain sample rate
            double targetMs = (i + 1) * intervalMs;
            double elapsedMs = sw.Elapsed.TotalMilliseconds;
            if (targetMs > elapsedMs)
                await Task.Delay(TimeSpan.FromMilliseconds(targetMs - elapsedMs), token)
                    .ConfigureAwait(false);
        }

        _adapter.SetForce(0);
        return ts;
    }

    /// <summary>Average the second repetition's signal into the first (for step metrics).</summary>
    private static void MergeRepetition(TimeSeries target, TimeSeries rep)
    {
        int n = Math.Min(target.Samples.Count, rep.Samples.Count);
        var merged = new List<Sample>(n);
        for (int i = 0; i < n; i++)
        {
            var a = target.Samples[i];
            var b = rep.Samples[i];
            merged.Add(new Sample
            {
                TimeS = a.TimeS,
                CommandedForce = (a.CommandedForce + b.CommandedForce) / 2,
                MeasuredPosition = Average(a.MeasuredPosition, b.MeasuredPosition),
                MeasuredForce = Average(a.MeasuredForce, b.MeasuredForce),
                MeasuredVelocity = Average(a.MeasuredVelocity, b.MeasuredVelocity)
            });
        }
        target.Samples.Clear();
        target.Samples.AddRange(merged);
    }

    private static double? Average(double? a, double? b) =>
        a.HasValue && b.HasValue ? (a.Value + b.Value) / 2 : a ?? b;

    public void Dispose() => _cts?.Dispose();
}
