namespace FFBAnalyzer.Models;

/// <summary>FFB capability level the device exposes.</summary>
public enum TelemetryMode
{
    /// <summary>Full SDK: position, velocity, torque readable.</summary>
    DirectFull = 0,
    /// <summary>Position/velocity only, torque not available.</summary>
    DirectPartial = 1,
    /// <summary>No telemetry – software-only measurements + manual protocol.</summary>
    SoftwareOnly = 2
}

/// <summary>Represents a detected FFB steering device.</summary>
public class Device
{
    public Guid DeviceId { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable product name reported by the driver.</summary>
    public string Name { get; set; } = string.Empty;

    public int VendorId { get; set; }
    public int ProductId { get; set; }

    /// <summary>E.g. "DirectInput", "HID", "SimuCube SDK".</summary>
    public string InterfaceType { get; set; } = "DirectInput";

    /// <summary>Driver or firmware version string (if readable).</summary>
    public string? DriverVersion { get; set; }

    /// <summary>Reported polling rate in Hz, 0 if unknown.</summary>
    public int PollingRateHz { get; set; }

    public TelemetryMode TelemetryMode { get; set; } = TelemetryMode.SoftwareOnly;

    /// <summary>Maximum reported force in Nm (0 = unknown).</summary>
    public double MaxForcNm { get; set; }

    /// <summary>Nominal steering range in degrees (0 = unknown).</summary>
    public int SteeringRangeDeg { get; set; }

    public override string ToString() => $"{Name} (VID:{VendorId:X4} PID:{ProductId:X4})";
}
