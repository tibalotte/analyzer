namespace FFBAnalyzer.Models;

/// <summary>Records what changed between this run and the previous one.</summary>
public class DriverSettingChange
{
    public string ParameterName { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
}

/// <summary>
/// One execution of a <see cref="TestDefinition"/> on a specific device with
/// a specific driver configuration.
/// </summary>
public class Run
{
    public Guid RunId { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid TestId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Snapshot of the device at run time.</summary>
    public Device Device { get; set; } = new();

    /// <summary>Snapshot of the test definition used.</summary>
    public TestDefinition TestDefinition { get; set; } = new();

    // ── Context / metadata ─────────────────────────────────────────────────
    public bool IsBaseline { get; set; }
    public string? Label { get; set; }
    public string? Notes { get; set; }

    /// <summary>Driver settings at the time of this run (key=value freeform).</summary>
    public Dictionary<string, string> DriverSettings { get; set; } = new();

    /// <summary>Changes applied vs. previous run (if any).</summary>
    public List<DriverSettingChange> SettingChanges { get; set; } = new();

    // ── Optional context metadata ──────────────────────────────────────────
    public string? GameOrFFBProfile { get; set; }
    public double? RoomTemperatureCelsius { get; set; }
    public int? UsbPollingRateHz { get; set; }

    // ── Data ──────────────────────────────────────────────────────────────
    public TimeSeries Data { get; set; } = new();
    public MetricResult? Metrics { get; set; }

    // ── Quality flags ──────────────────────────────────────────────────────
    public bool WasAborted { get; set; }
    public bool ClippingDetected { get; set; }

    public string DisplayLabel =>
        Label ?? (IsBaseline ? "Baseline" : Timestamp.ToLocalTime().ToString("HH:mm:ss"));
}
