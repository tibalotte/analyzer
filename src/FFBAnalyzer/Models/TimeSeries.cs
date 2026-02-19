namespace FFBAnalyzer.Models;

/// <summary>A single sample in the time series.</summary>
public readonly struct Sample
{
    /// <summary>Time offset from test start, in seconds.</summary>
    public double TimeS { get; init; }

    /// <summary>Commanded FFB signal [-1, +1].</summary>
    public double CommandedForce { get; init; }

    /// <summary>Measured position (normalised -1…+1, 0 = centre). Null if not available.</summary>
    public double? MeasuredPosition { get; init; }

    /// <summary>Estimated or measured torque/force. Null if not available.</summary>
    public double? MeasuredForce { get; init; }

    /// <summary>Wheel velocity (deg/s or normalised). Null if not available.</summary>
    public double? MeasuredVelocity { get; init; }
}

/// <summary>Complete time-series data for one run.</summary>
public class TimeSeries
{
    public List<Sample> Samples { get; set; } = new();

    public double DurationSec => Samples.Count > 0
        ? Samples[^1].TimeS - Samples[0].TimeS
        : 0;

    public double SampleRateHz => Samples.Count > 1
        ? (Samples.Count - 1) / DurationSec
        : 0;

    /// <summary>True if measured position data is present.</summary>
    public bool HasPosition => Samples.Any(s => s.MeasuredPosition.HasValue);

    /// <summary>True if measured force data is present.</summary>
    public bool HasForce => Samples.Any(s => s.MeasuredForce.HasValue);

    public double[] GetCommandedArray() =>
        Samples.Select(s => s.CommandedForce).ToArray();

    public double[] GetTimeArray() =>
        Samples.Select(s => s.TimeS).ToArray();

    public double[]? GetPositionArray() => HasPosition
        ? Samples.Select(s => s.MeasuredPosition ?? 0).ToArray()
        : null;

    public double[]? GetForceArray() => HasForce
        ? Samples.Select(s => s.MeasuredForce ?? 0).ToArray()
        : null;
}
