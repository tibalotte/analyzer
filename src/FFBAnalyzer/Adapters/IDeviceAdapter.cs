using FFBAnalyzer.Models;

namespace FFBAnalyzer.Adapters;

/// <summary>Telemetry snapshot from the wheel at one instant.</summary>
public readonly struct WheelTelemetry
{
    /// <summary>Normalised steering position [-1, +1], 0 = centre.</summary>
    public double Position { get; init; }

    /// <summary>Angular velocity (deg/s) – positive = clockwise. Null if unavailable.</summary>
    public double? VelocityDegS { get; init; }

    /// <summary>Estimated torque (Nm or normalised). Null if unavailable.</summary>
    public double? TorqueNm { get; init; }

    /// <summary>UTC timestamp of this sample.</summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Contract every device adapter must implement.
/// Adapters encapsulate brand-specific SDKs and expose a unified API.
/// </summary>
public interface IDeviceAdapter : IAsyncDisposable
{
    /// <summary>Human-readable adapter name (e.g. "DirectInput Generic").</summary>
    string AdapterName { get; }

    /// <summary>Returns all detected FFB-capable devices.</summary>
    Task<IReadOnlyList<Device>> EnumerateDevicesAsync();

    /// <summary>Activates a specific device for output and telemetry.</summary>
    Task OpenDeviceAsync(Device device);

    /// <summary>Releases the current device.</summary>
    Task CloseDeviceAsync();

    /// <summary>True if a device is currently open.</summary>
    bool IsOpen { get; }

    // ── Safety ─────────────────────────────────────────────────────────────

    /// <summary>Immediately cancels all active FFB effects. Thread-safe.</summary>
    void EmergencyStop();

    // ── Output ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Send a single FFB force sample.
    /// <paramref name="normalizedForce"/> is in [-1, +1].
    /// </summary>
    void SetForce(double normalizedForce);

    // ── Input / Telemetry ──────────────────────────────────────────────────

    /// <summary>Reads the latest telemetry from the device.</summary>
    WheelTelemetry ReadTelemetry();

    /// <summary>Available telemetry mode for the currently open device.</summary>
    TelemetryMode AvailableTelemetryMode { get; }
}
