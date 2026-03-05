using FFBAnalyzer.Models;

namespace FFBAnalyzer.Adapters;

/// <summary>
/// Merges real hardware (DirectInput) and virtual simulation into a single adapter.
/// Device enumeration returns hardware wheels first, then simulated profiles.
/// All per-operation calls are routed to the adapter that owns the open device.
/// </summary>
public sealed class CompositeDeviceAdapter : IDeviceAdapter
{
    private readonly DirectInputAdapter    _hardware;
    private readonly SimulatedDeviceAdapter _simulated;
    private IDeviceAdapter? _active;

    public CompositeDeviceAdapter(DirectInputAdapter hardware, SimulatedDeviceAdapter simulated)
    {
        _hardware  = hardware;
        _simulated = simulated;
    }

    public string AdapterName => "Composite (Hardware + Simulated)";

    public async Task<IReadOnlyList<Device>> EnumerateDevicesAsync()
    {
        var result = new List<Device>();

        // Hardware devices first – swallow exceptions (e.g. no DirectInput available)
        try   { result.AddRange(await _hardware.EnumerateDevicesAsync()); }
        catch { /* hardware unavailable; continue with simulation */ }

        result.AddRange(await _simulated.EnumerateDevicesAsync());
        return result;
    }

    public Task OpenDeviceAsync(Device device)
    {
        // Route by InterfaceType tag written by SimulatedDeviceAdapter
        _active = device.InterfaceType == "Simulated"
            ? (IDeviceAdapter)_simulated
            : _hardware;

        return _active.OpenDeviceAsync(device);
    }

    public Task CloseDeviceAsync() => Active.CloseDeviceAsync();

    public bool IsOpen => _active?.IsOpen ?? false;

    public void EmergencyStop() => _active?.EmergencyStop();

    public void SetForce(double normalizedForce) => Active.SetForce(normalizedForce);

    public WheelTelemetry ReadTelemetry() => Active.ReadTelemetry();

    public TelemetryMode AvailableTelemetryMode =>
        _active?.AvailableTelemetryMode ?? TelemetryMode.SoftwareOnly;

    public async ValueTask DisposeAsync()
    {
        await _hardware.DisposeAsync();
        await _simulated.DisposeAsync();
    }

    // Convenience: throw a clear message if called before OpenDeviceAsync
    private IDeviceAdapter Active =>
        _active ?? throw new InvalidOperationException("No device is currently open.");
}
