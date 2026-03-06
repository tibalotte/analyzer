using System.Runtime.InteropServices;
using FFBAnalyzer.Models;
using SharpDX.DirectInput;
using Device = FFBAnalyzer.Models.Device;

namespace FFBAnalyzer.Adapters;

/// <summary>
/// DirectInput-based adapter for generic FFB wheels on Windows.
/// Uses SharpDX.DirectInput for device enumeration and effect playback,
/// and reads HID position data as telemetry (Mode B – position only).
/// </summary>
public sealed class DirectInputAdapter : IDeviceAdapter
{
    private DirectInput? _di;
    private Joystick? _joystick;
    private Effect? _constantForceEffect;
    private int _lastForce;
    private volatile bool _stopped;

    public string AdapterName => "DirectInput (Generic)";
    public bool IsOpen => _joystick != null;
    public TelemetryMode AvailableTelemetryMode => TelemetryMode.DirectPartial; // position only

    public async Task<IReadOnlyList<Device>> EnumerateDevicesAsync()
    {
        await Task.Yield();

        _di ??= new DirectInput();
        var devices = new List<Device>();

        var diDevices = _di.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.ForceFeedback)
            .Concat(_di.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.ForceFeedback))
            .Concat(_di.GetDevices(DeviceType.Driving, DeviceEnumerationFlags.ForceFeedback));

        foreach (var di in diDevices)
        {
            devices.Add(new Device
            {
                Name = di.ProductName,
                VendorId = di.ProductGuid.ToByteArray() is { } b ? (b[1] << 8 | b[0]) : 0,
                ProductId = di.ProductGuid.ToByteArray() is { } b2 ? (b2[3] << 8 | b2[2]) : 0,
                InterfaceType = "DirectInput",
                TelemetryMode = TelemetryMode.DirectPartial
            });
        }

        return devices;
    }

    public async Task OpenDeviceAsync(Device device)
    {
        await Task.Yield();

        _di ??= new DirectInput();

        var diDevices = _di.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.ForceFeedback)
            .Concat(_di.GetDevices(DeviceType.Driving, DeviceEnumerationFlags.ForceFeedback))
            .ToList();

        DeviceInstance? match = diDevices.FirstOrDefault(d => d.ProductName == device.Name);
        if (match == null)
            throw new InvalidOperationException($"Device '{device.Name}' not found.");

        CloseDevice();

        _joystick = new Joystick(_di, match.InstanceGuid);
        _joystick.SetCooperativeLevel(GetForegroundWindow(),
            CooperativeLevel.Background | CooperativeLevel.NonExclusive);
        _joystick.Properties.BufferSize = 128;
        _joystick.Acquire();

        // Set up constant force effect
        var effectParams = new EffectParameters
        {
            Flags = EffectFlags.Cartesian | EffectFlags.ObjectOffsets,
            Duration = -1,  // DIEFF_INFINITE = 0xFFFFFFFF
            SamplePeriod = 0,
            Gain = 10000,
            TriggerButton = -1,
            TriggerRepeatInterval = -1,  // DIEFF_INFINITE
            Axes = new[] { 0 },
            Directions = new[] { 0 },
            Envelope = null,
            Parameters = new ConstantForce { Magnitude = 0 },
            StartDelay = 0
        };

        _constantForceEffect = new Effect(_joystick, EffectGuid.ConstantForce, effectParams);
        _constantForceEffect.Start(1, EffectPlayFlags.NoDownload);
        _stopped = false;
    }

    public async Task CloseDeviceAsync()
    {
        await Task.Yield();
        CloseDevice();
    }

    private void CloseDevice()
    {
        _constantForceEffect?.Dispose();
        _constantForceEffect = null;
        _joystick?.Unacquire();
        _joystick?.Dispose();
        _joystick = null;
    }

    public void EmergencyStop()
    {
        _stopped = true;
        try
        {
            _constantForceEffect?.Stop();
        }
        catch { /* intentionally swallow – this is emergency path */ }
    }

    public void SetForce(double normalizedForce)
    {
        if (_stopped || _constantForceEffect == null) return;

        // DirectInput magnitude: -10000 to +10000
        int magnitude = (int)Math.Clamp(normalizedForce * 10000, -10000, 10000);
        if (magnitude == _lastForce) return;
        _lastForce = magnitude;

        try
        {
            var effectParams = new EffectParameters
            {
                Parameters = new ConstantForce { Magnitude = magnitude }
            };
            _constantForceEffect.SetParameters(effectParams,
                EffectParameterFlags.TypeSpecificParameters | EffectParameterFlags.NoRestart);
        }
        catch (SharpDX.SharpDXException) { /* ignore transient errors during normal operation */ }
    }

    public WheelTelemetry ReadTelemetry()
    {
        if (_joystick == null)
            return new WheelTelemetry { Timestamp = DateTime.UtcNow };

        try
        {
            _joystick.Poll();
            var state = _joystick.GetCurrentState();

            // X axis is typically steering: 0 (full left) to 65535 (full right)
            double position = (state.X - 32767.5) / 32767.5;

            return new WheelTelemetry
            {
                Position = Math.Clamp(position, -1, 1),
                VelocityDegS = null,   // not available via generic HID
                TorqueNm = null,       // not available via generic HID
                Timestamp = DateTime.UtcNow
            };
        }
        catch
        {
            return new WheelTelemetry { Timestamp = DateTime.UtcNow };
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseDeviceAsync();
        _di?.Dispose();
        _di = null;
    }

    // P/Invoke for cooperative level window handle
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
}
