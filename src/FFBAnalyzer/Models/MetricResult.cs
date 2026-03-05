namespace FFBAnalyzer.Models;

/// <summary>Semantic interpretation label attached to a metric comparison.</summary>
public enum InterpretationLabel
{
    None,
    MoreDamped,
    LessDamped,
    MoreFiltered,
    LessFiltered,
    HigherLatency,
    LowerLatency,
    MoreResonance,
    LessResonance,
    SteadyStateError,
    Clipping
}

/// <summary>A single computed metric with name, value, unit and interpretation.</summary>
public class Metric
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Description { get; set; }
    public InterpretationLabel Interpretation { get; set; } = InterpretationLabel.None;
}

/// <summary>All metrics computed for a single run of a specific test type.</summary>
public class MetricResult
{
    public Guid RunId { get; set; }
    public TestType TestType { get; set; }
    public List<Metric> Metrics { get; set; } = new();

    // ── Step-response shortcut accessors ──────────────────────────────────
    public double? RiseTimeMs => Get("rise_time_ms");
    public double? OvershootPct => Get("overshoot_pct");
    public double? SettlingTimeMs => Get("settling_time_ms");
    public double? SteadyStateError => Get("steady_state_error");

    // ── Sweep shortcut accessors ──────────────────────────────────────────
    public double? CutoffFreqHz => Get("cutoff_freq_hz");
    public double? PeakResonanceHz => Get("peak_resonance_hz");
    public double? PeakResonanceDb => Get("peak_resonance_db");

    // ── Square / latency accessors ────────────────────────────────────────
    public double? EstimatedLatencyMs => Get("latency_ms");
    public double? SmoothingIndex => Get("smoothing_index");

    public double? Get(string key) =>
        Metrics.FirstOrDefault(m => m.Key == key)?.Value;

    /// <summary>Human-readable interpretation sentences derived from the metrics.</summary>
    public IReadOnlyList<string> InterpretationSentences =>
        Metrics
            .Where(m => m.Interpretation != InterpretationLabel.None)
            .Select(m => FormatInterpretation(m))
            .ToList();

    private static string FormatInterpretation(Metric m) => m.Interpretation switch
    {
        InterpretationLabel.MoreDamped      => $"High damping / inertia detected (settling {m.Value:F0} ms)",
        InterpretationLabel.LessDamped      => $"Low damping detected (settling {m.Value:F0} ms)",
        InterpretationLabel.MoreFiltered    => $"Strong filter / smoothing (cutoff ≈ {m.Value:F1} Hz)",
        InterpretationLabel.LessFiltered    => $"Light filtering (cutoff ≈ {m.Value:F1} Hz)",
        InterpretationLabel.HigherLatency   => $"Increased latency detected ({m.Value:F1} ms)",
        InterpretationLabel.LowerLatency    => $"Low latency ({m.Value:F1} ms)",
        InterpretationLabel.MoreResonance   => $"Resonance peak at {m.Value:F1} Hz",
        InterpretationLabel.SteadyStateError => $"Steady-state error {m.Value:P1}",
        InterpretationLabel.Clipping        => "Output clipping / saturation detected",
        _                                    => string.Empty
    };
}
