using FFBAnalyzer.Models;

namespace FFBAnalyzer.Services;

/// <summary>
/// Generates time-indexed FFB command sequences for each test type.
/// All values are normalised to [-1, +1] (positive = right / push).
/// The generator is pure (no I/O) so it is fully unit-testable.
/// </summary>
public static class FFBSignalGenerator
{
    /// <summary>
    /// Generate a sequence of (timeS, commandedForce) pairs for the given test.
    /// The caller feeds these to the FFB output layer at the specified rate.
    /// </summary>
    public static IReadOnlyList<(double TimeS, double Force)> Generate(TestDefinition def)
    {
        double dt = 1.0 / def.SampleRateHz;
        int totalSamples = (int)(def.DurationSec * def.SampleRateHz);

        return def.Type switch
        {
            TestType.StepResponse      => GenerateStep(def, dt, totalSamples),
            TestType.SineSweep         => GenerateSineSweep(def, dt, totalSamples),
            TestType.Chirp             => GenerateChirp(def, dt, totalSamples),
            TestType.SquareWave        => GenerateSquare(def, dt, totalSamples),
            TestType.Impulse           => GenerateImpulse(def, dt, totalSamples),
            TestType.ConstantTorque    => GenerateConstant(def, dt, totalSamples),
            TestType.FrictionEmulation => GenerateFriction(def, dt, totalSamples),
            _                          => throw new NotSupportedException($"Unknown test type: {def.Type}")
        };
    }

    // ── Individual generators ──────────────────────────────────────────────

    /// <summary>
    /// Step: ramp-up → hold at intensity → ramp-down to 0.
    /// </summary>
    private static List<(double, double)> GenerateStep(TestDefinition def, double dt, int n)
    {
        var result = new List<(double, double)>(n);
        double rampDuration = def.RampMs / 1000.0;
        double holdEnd = rampDuration + def.HoldSec;

        for (int i = 0; i < n; i++)
        {
            double t = i * dt;
            double force;
            if (t < rampDuration)
                force = def.Intensity * (rampDuration > 0 ? t / rampDuration : 1.0);
            else if (t < holdEnd)
                force = def.Intensity;
            else
                // ramp back to 0
                force = def.Intensity * Math.Max(0, 1.0 - (t - holdEnd) / Math.Max(rampDuration, dt));

            result.Add((t, force));
        }
        return result;
    }

    /// <summary>
    /// Sine sweep: linearly increases frequency from FreqStartHz to FreqEndHz.
    /// Phase is integrated to avoid discontinuities.
    /// </summary>
    private static List<(double, double)> GenerateSineSweep(TestDefinition def, double dt, int n)
    {
        var result = new List<(double, double)>(n);
        double phase = 0;
        double k = (def.FreqEndHz - def.FreqStartHz) / def.DurationSec;

        for (int i = 0; i < n; i++)
        {
            double t = i * dt;
            double freq = def.FreqStartHz + k * t;
            phase += 2 * Math.PI * freq * dt;
            double force = def.Intensity * Math.Sin(phase);
            result.Add((t, force));
        }
        return result;
    }

    /// <summary>
    /// Chirp: exponential frequency sweep (log scale) – better coverage at high freqs.
    /// </summary>
    private static List<(double, double)> GenerateChirp(TestDefinition def, double dt, int n)
    {
        var result = new List<(double, double)>(n);
        double phase = 0;
        double logRatio = Math.Log(def.FreqEndHz / def.FreqStartHz);

        for (int i = 0; i < n; i++)
        {
            double t = i * dt;
            double freq = def.FreqStartHz * Math.Exp(logRatio * t / def.DurationSec);
            phase += 2 * Math.PI * freq * dt;
            double force = def.Intensity * Math.Sin(phase);
            result.Add((t, force));
        }
        return result;
    }

    /// <summary>
    /// Square wave at a fixed frequency with optional ramp on each edge.
    /// </summary>
    private static List<(double, double)> GenerateSquare(TestDefinition def, double dt, int n)
    {
        var result = new List<(double, double)>(n);
        double period = 1.0 / def.FrequencyHz;
        double ramp = def.RampMs / 1000.0;

        for (int i = 0; i < n; i++)
        {
            double t = i * dt;
            double tInPeriod = t % period;
            bool high = tInPeriod < period / 2.0;
            double target = high ? def.Intensity : -def.Intensity;

            double edgeTime = tInPeriod < period / 2.0 ? tInPeriod : tInPeriod - period / 2.0;
            double rampFactor = ramp > 0 ? Math.Min(1.0, edgeTime / ramp) : 1.0;
            double force = target * rampFactor;
            result.Add((t, force));
        }
        return result;
    }

    /// <summary>
    /// Single brief impulse (rectangular pulse of very short duration).
    /// </summary>
    private static List<(double, double)> GenerateImpulse(TestDefinition def, double dt, int n)
    {
        var result = new List<(double, double)>(n);
        for (int i = 0; i < n; i++)
        {
            double t = i * dt;
            double force = t < def.DurationSec ? def.Intensity : 0.0;
            result.Add((t, force));
        }
        return result;
    }

    /// <summary>
    /// Constant torque hold with leading ramp.
    /// </summary>
    private static List<(double, double)> GenerateConstant(TestDefinition def, double dt, int n)
    {
        var result = new List<(double, double)>(n);
        double rampDuration = def.RampMs / 1000.0;

        for (int i = 0; i < n; i++)
        {
            double t = i * dt;
            double rampFactor = rampDuration > 0 ? Math.Min(1.0, t / rampDuration) : 1.0;
            double force = def.Intensity * rampFactor;
            result.Add((t, force));
        }
        return result;
    }

    /// <summary>
    /// Friction emulation: micro-oscillations around zero to probe deadband/friction.
    /// </summary>
    private static List<(double, double)> GenerateFriction(TestDefinition def, double dt, int n)
    {
        var result = new List<(double, double)>(n);
        double amplitude = def.Intensity * def.FrictionOscAmplitude;
        double phase = 0;

        for (int i = 0; i < n; i++)
        {
            double t = i * dt;
            phase += 2 * Math.PI * def.FrequencyHz * dt;
            double force = amplitude * Math.Sin(phase);
            result.Add((t, force));
        }
        return result;
    }
}
