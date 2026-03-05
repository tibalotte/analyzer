using FFBAnalyzer.Models;
using FFBAnalyzer.Services;
using FluentAssertions;
using Xunit;

namespace FFBAnalyzer.Tests;

public class MetricsServiceTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static Run BuildRunFromSignal(TestDefinition def, Func<double, double, double> responseFunc)
    {
        var signal = FFBSignalGenerator.Generate(def);
        var data = new TimeSeries();
        foreach (var (t, f) in signal)
        {
            data.Samples.Add(new Sample
            {
                TimeS = t,
                CommandedForce = f,
                MeasuredPosition = responseFunc(t, f)
            });
        }

        return new Run
        {
            TestDefinition = def,
            Data = data,
            Device = new Device()
        };
    }

    // ── Step Response ──────────────────────────────────────────────────────

    [Fact]
    public void StepMetrics_IdealResponse_RiseTimeIsNearZero()
    {
        var def = TestDefinition.StepResponse(0.20);
        // Ideal: response equals commanded instantly
        var run = BuildRunFromSignal(def, (t, f) => f);
        var result = MetricsService.Compute(run);

        var riseTime = result.Get("rise_time_ms");
        riseTime.Should().NotBeNull();
        riseTime!.Value.Should().BeLessOrEqualTo(10.0,
            because: "ideal response should rise almost immediately");
    }

    [Fact]
    public void StepMetrics_NoOvershoot_WhenResponseIsDamped()
    {
        var def = TestDefinition.StepResponse(0.20);
        // Critically damped: response ramps smoothly to target, never exceeds
        var run = BuildRunFromSignal(def, (t, f) =>
        {
            double rampEnd = def.RampMs / 1000.0 + def.HoldSec;
            return t < def.RampMs / 1000.0 ? f * 0.5 : Math.Min(f, def.Intensity);
        });
        var result = MetricsService.Compute(run);

        var overshoot = result.Get("overshoot_pct");
        overshoot.Should().NotBeNull();
        overshoot!.Value.Should().BeLessOrEqualTo(0.0);
    }

    [Fact]
    public void StepMetrics_DetectsHighSettlingTime_WhenResponseIsSlug()
    {
        var def = TestDefinition.StepResponse(0.20);
        def.DurationSec = 10;
        def.HoldSec = 8;

        // Very slow response: takes most of the hold period to settle
        var run = BuildRunFromSignal(def, (t, f) =>
        {
            // Exponential rise with tau = 2 seconds
            double target = def.Intensity;
            double rampEnd = def.RampMs / 1000.0;
            if (t < rampEnd) return 0;
            return target * (1 - Math.Exp(-(t - rampEnd) / 2.0));
        });
        var result = MetricsService.Compute(run);

        var settling = result.Get("settling_time_ms");
        settling.Should().NotBeNull();
        settling!.Value.Should().BeGreaterThan(500,
            because: "slow response should have a long settling time");

        // Should also be interpreted as more-damped
        result.Metrics.Should().Contain(m =>
            m.Key == "settling_time_ms" && m.Interpretation == InterpretationLabel.MoreDamped);
    }

    [Fact]
    public void StepMetrics_DetectsClipping_WhenResponseSaturates()
    {
        var def = TestDefinition.StepResponse(0.20);
        // Response saturates at 0.98
        var run = BuildRunFromSignal(def, (t, f) => Math.Min(f * 5.0, 0.98));
        var result = MetricsService.Compute(run);

        result.Metrics.Should().Contain(m => m.Key == "clipping",
            because: "saturated response at 0.98 should trigger clipping detection");
    }

    [Fact]
    public void StepMetrics_SteadyStateError_IsNearZero_ForPerfectResponse()
    {
        var def = TestDefinition.StepResponse(0.20);
        var run = BuildRunFromSignal(def, (t, f) => f);
        var result = MetricsService.Compute(run);

        var ssError = result.Get("steady_state_error");
        ssError.Should().NotBeNull();
        ssError!.Value.Should().BeLessThan(0.05,
            because: "a perfect response should have no steady-state error");
    }

    // ── Signal Generator ───────────────────────────────────────────────────

    [Fact]
    public void Generator_Step_IntensityIsRespected()
    {
        var def = TestDefinition.StepResponse(0.35);
        var signal = FFBSignalGenerator.Generate(def);

        signal.Should().NotBeEmpty();
        signal.Max(s => s.Force).Should().BeApproximately(0.35, 0.01,
            because: "step peak should match intensity");
    }

    [Fact]
    public void Generator_SineSweep_AllValuesWithinBounds()
    {
        var def = TestDefinition.SineSweep(0.20);
        var signal = FFBSignalGenerator.Generate(def);

        signal.Should().AllSatisfy(s =>
            Math.Abs(s.Force).Should().BeLessOrEqualTo(0.201),
            because: "sine sweep must never exceed intensity");
    }

    [Fact]
    public void Generator_Square_AlternatesSign()
    {
        var def = TestDefinition.SquareWave(0.15);
        def.DurationSec = 1.0;
        var signal = FFBSignalGenerator.Generate(def);

        bool hasPositive = signal.Any(s => s.Force > 0.10);
        bool hasNegative = signal.Any(s => s.Force < -0.10);
        hasPositive.Should().BeTrue("square wave must have positive half-cycles");
        hasNegative.Should().BeTrue("square wave must have negative half-cycles");
    }

    [Fact]
    public void Generator_Impulse_OnlyPositiveForceBeforeDurationEnd()
    {
        var def = TestDefinition.Impulse(0.30);
        def.DurationSec = 0.05;
        def.SampleRateHz = 1000;
        var signal = FFBSignalGenerator.Generate(def);

        // During the pulse
        var duringPulse = signal.TakeWhile(s => s.TimeS < def.DurationSec - 0.001);
        duringPulse.Should().AllSatisfy(s =>
            s.Force.Should().BeApproximately(def.Intensity, 0.001));

        // After the pulse
        var afterPulse = signal.SkipWhile(s => s.TimeS < def.DurationSec);
        afterPulse.Should().AllSatisfy(s =>
            s.Force.Should().BeApproximately(0, 0.001));
    }

    [Fact]
    public void Generator_Chirp_FrequencyIncreases()
    {
        var def = TestDefinition.Chirp(0.20);
        def.DurationSec = 5.0;
        def.FreqStartHz = 5.0;
        def.FreqEndHz = 40.0;
        var signal = FFBSignalGenerator.Generate(def);

        // Count zero-crossings in first vs last quarter
        int firstQuarterSamples = signal.Count / 4;
        var firstQ = signal.Take(firstQuarterSamples).ToList();
        var lastQ = signal.Skip(signal.Count - firstQuarterSamples).ToList();

        int crossingsFirst = CountZeroCrossings(firstQ.Select(s => s.Force).ToList());
        int crossingsLast = CountZeroCrossings(lastQ.Select(s => s.Force).ToList());

        crossingsLast.Should().BeGreaterThan(crossingsFirst,
            because: "chirp should have more zero crossings at high frequency end");
    }

    [Fact]
    public void SweepMetrics_ReturnsCutoffFrequency()
    {
        var def = TestDefinition.SineSweep(0.20);
        def.DurationSec = 20.0;
        def.FreqStartHz = 1.0;
        def.FreqEndHz = 60.0;

        // Simulate a first-order low-pass at 20 Hz: H(f) = 1 / sqrt(1 + (f/f_c)^2)
        double fc = 20.0;
        var run = BuildRunFromSignal(def, (t, f) =>
        {
            // Estimate instantaneous frequency (approximation)
            double k = (def.FreqEndHz - def.FreqStartHz) / def.DurationSec;
            double freq = def.FreqStartHz + k * t;
            double gain = 1.0 / Math.Sqrt(1 + (freq / fc) * (freq / fc));
            return f * gain;
        });

        var result = MetricsService.Compute(run);
        var cutoff = result.Get("cutoff_freq_hz");
        cutoff.Should().NotBeNull();
        cutoff!.Value.Should().BeInRange(10, 35,
            because: "detected cutoff should be in the rough area of the 20 Hz filter");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static int CountZeroCrossings(List<double> values)
    {
        int count = 0;
        for (int i = 1; i < values.Count; i++)
            if (values[i - 1] < 0 && values[i] >= 0 ||
                values[i - 1] >= 0 && values[i] < 0)
                count++;
        return count;
    }
}
