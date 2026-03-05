namespace FFBAnalyzer.Models;

/// <summary>All supported standardised test signal types.</summary>
public enum TestType
{
    StepResponse,
    SineSweep,
    Chirp,
    SquareWave,
    Impulse,
    ConstantTorque,
    FrictionEmulation
}

/// <summary>
/// Immutable definition of a single FFB test – parameters only, no results.
/// </summary>
public class TestDefinition
{
    public Guid TestId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public TestType Type { get; set; }

    // ── Common parameters ──────────────────────────────────────────────────

    /// <summary>Peak intensity as fraction [0, 1].</summary>
    public double Intensity { get; set; } = 0.20;

    /// <summary>Total test duration in seconds.</summary>
    public double DurationSec { get; set; } = 5.0;

    /// <summary>Attack ramp duration in milliseconds (0 = instant).</summary>
    public double RampMs { get; set; } = 50;

    /// <summary>Number of repetitions (results are averaged).</summary>
    public int Repetitions { get; set; } = 1;

    /// <summary>Target recording sample rate in Hz.</summary>
    public int SampleRateHz { get; set; } = 500;

    // ── Type-specific parameters ───────────────────────────────────────────

    /// <summary>Start frequency for sweep/chirp (Hz).</summary>
    public double FreqStartHz { get; set; } = 1.0;

    /// <summary>End frequency for sweep/chirp (Hz).</summary>
    public double FreqEndHz { get; set; } = 60.0;

    /// <summary>Square/sine wave fixed frequency (Hz).</summary>
    public double FrequencyHz { get; set; } = 10.0;

    /// <summary>Hold duration after reaching target (seconds) – for Step/Constant.</summary>
    public double HoldSec { get; set; } = 2.0;

    /// <summary>Friction zone micro-oscillation amplitude (fraction of intensity).</summary>
    public double FrictionOscAmplitude { get; set; } = 0.05;

    // ── Presets ────────────────────────────────────────────────────────────

    public static TestDefinition StepResponse(double intensity = 0.20) => new()
    {
        Name = "Step Response",
        Type = TestType.StepResponse,
        Intensity = intensity,
        DurationSec = 5.0,
        RampMs = 0,
        HoldSec = 2.0,
        Repetitions = 3,
        SampleRateHz = 500
    };

    public static TestDefinition SineSweep(double intensity = 0.20) => new()
    {
        Name = "Sine Sweep",
        Type = TestType.SineSweep,
        Intensity = intensity,
        DurationSec = 20.0,
        FreqStartHz = 1.0,
        FreqEndHz = 60.0,
        Repetitions = 1,
        SampleRateHz = 500
    };

    public static TestDefinition Chirp(double intensity = 0.20) => new()
    {
        Name = "Chirp",
        Type = TestType.Chirp,
        Intensity = intensity,
        DurationSec = 10.0,
        FreqStartHz = 5.0,
        FreqEndHz = 80.0,
        Repetitions = 1,
        SampleRateHz = 500
    };

    public static TestDefinition SquareWave(double intensity = 0.15) => new()
    {
        Name = "Square Wave",
        Type = TestType.SquareWave,
        Intensity = intensity,
        DurationSec = 15.0,
        FrequencyHz = 10.0,
        Repetitions = 1,
        SampleRateHz = 500
    };

    public static TestDefinition Impulse(double intensity = 0.30) => new()
    {
        Name = "Impulse",
        Type = TestType.Impulse,
        Intensity = intensity,
        DurationSec = 0.05,
        RampMs = 0,
        Repetitions = 3,
        SampleRateHz = 1000
    };

    public static TestDefinition ConstantTorque(double intensity = 0.30) => new()
    {
        Name = "Constant Torque",
        Type = TestType.ConstantTorque,
        Intensity = intensity,
        DurationSec = 10.0,
        HoldSec = 10.0,
        Repetitions = 1,
        SampleRateHz = 250
    };

    public static TestDefinition FrictionEmulation(double intensity = 0.10) => new()
    {
        Name = "Friction Emulation",
        Type = TestType.FrictionEmulation,
        Intensity = intensity,
        DurationSec = 8.0,
        FrequencyHz = 20.0,
        FrictionOscAmplitude = 0.05,
        Repetitions = 1,
        SampleRateHz = 500
    };
}

/// <summary>Named battery of tests.</summary>
public class TestBattery
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<TestDefinition> Tests { get; set; } = new();
    public override string ToString() => Name;

    // ── Factory presets ────────────────────────────────────────────────────

    public static TestBattery Quick2Min() => new()
    {
        Name = "Quick (2 min)",
        Description = "Rapid overview: step, sweep, square",
        Tests = new List<TestDefinition>
        {
            TestDefinition.StepResponse(0.20),
            TestDefinition.SineSweep(0.20),
            TestDefinition.SquareWave(0.15)
        }
    };

    public static TestBattery Standard5Min() => new()
    {
        Name = "Standard (5 min)",
        Description = "Balanced battery for day-to-day comparison",
        Tests = new List<TestDefinition>
        {
            TestDefinition.StepResponse(0.20),
            TestDefinition.StepResponse(0.40),
            TestDefinition.SineSweep(0.20),
            TestDefinition.Chirp(0.20),
            TestDefinition.ConstantTorque(0.30)
        }
    };

    public static TestBattery Deep12Min() => new()
    {
        Name = "Deep (12 min)",
        Description = "Comprehensive characterisation",
        Tests = new List<TestDefinition>
        {
            TestDefinition.StepResponse(0.20),
            TestDefinition.StepResponse(0.40),
            TestDefinition.SineSweep(0.20),
            TestDefinition.Chirp(0.20),
            TestDefinition.SquareWave(0.15),
            TestDefinition.Impulse(0.30),
            TestDefinition.ConstantTorque(0.30),
            TestDefinition.FrictionEmulation(0.10)
        }
    };
}
