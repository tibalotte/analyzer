using FFBAnalyzer.Models;

namespace FFBAnalyzer.Adapters;

/// <summary>
/// Virtual FFB wheel adapter for testing without hardware.
/// Simulates three distinct wheel profiles using a spring-mass-damper physical model.
///
/// Physics: m·a = F·gain − b·v − k·pos
///   m    = effective rotational inertia
///   b    = viscous damping (bearing friction)
///   k    = centering spring stiffness
///   gain = force-to-motion scaling
///
/// Latency is modelled as a first-in-first-out delay queue on the force command.
/// </summary>
public sealed class SimulatedDeviceAdapter : IDeviceAdapter
{
    // ── Device catalogue ────────────────────────────────────────────────────

    private sealed class SimProfile
    {
        public required double Mass     { get; init; }
        public required double Damping  { get; init; }
        public required double Spring   { get; init; }
        public required double Gain     { get; init; }
        public required double LatencyMs { get; init; }
        public required double MaxForcNm { get; init; }
        public required int    SteeringRangeDeg { get; init; }
    }

    private static readonly (Guid Id, string Name, SimProfile Profile)[] Catalog =
    [
        // Profile 1 – Direct Drive: underdamped (ζ ≈ 0.24), visible resonance, 2 ms latency
        (
            new Guid("00000001-ffb0-0000-0000-000000000000"),
            "Simulated Direct Drive  [Low Damping – Resonant]",
            new SimProfile { Mass = 0.08, Damping = 0.06, Spring = 0.20,
                             Gain = 0.12, LatencyMs = 2, MaxForcNm = 20, SteeringRangeDeg = 1080 }
        ),
        // Profile 2 – Belt Drive: slightly underdamped (ζ ≈ 0.78), normal response, 6 ms latency
        (
            new Guid("00000002-ffb0-0000-0000-000000000000"),
            "Simulated Belt Drive    [Normal Response]",
            new SimProfile { Mass = 0.10, Damping = 0.22, Spring = 0.20,
                             Gain = 0.10, LatencyMs = 6, MaxForcNm = 8,  SteeringRangeDeg = 900  }
        ),
        // Profile 3 – Gear Drive: overdamped (ζ ≈ 1.61), sluggish, 12 ms latency
        (
            new Guid("00000003-ffb0-0000-0000-000000000000"),
            "Simulated Gear Drive    [High Damping – Slow]",
            new SimProfile { Mass = 0.12, Damping = 0.50, Spring = 0.20,
                             Gain = 0.08, LatencyMs = 12, MaxForcNm = 6,  SteeringRangeDeg = 900  }
        ),
    ];

    // ── Internal state ──────────────────────────────────────────────────────

    private SimProfile? _profile;
    private double _cmdForce;        // latest force command [-1, +1]
    private double _pos, _vel;       // wheel position and velocity (normalised)
    private Queue<double>? _delay;   // force latency queue
    private DateTime _lastTick = DateTime.UtcNow;
    private readonly object _lock = new();

    // ── IDeviceAdapter ──────────────────────────────────────────────────────

    public string AdapterName => "Simulated FFB";
    public bool IsOpen => _profile is not null;
    public TelemetryMode AvailableTelemetryMode => TelemetryMode.DirectFull;

    public Task<IReadOnlyList<Device>> EnumerateDevicesAsync()
    {
        IReadOnlyList<Device> devices = Catalog.Select((c, i) => new Device
        {
            DeviceId        = c.Id,
            Name            = c.Name,
            VendorId        = 0xFFFF,
            ProductId       = i + 1,
            InterfaceType   = "Simulated",
            DriverVersion   = "1.0 (virtual)",
            PollingRateHz   = 500,
            TelemetryMode   = TelemetryMode.DirectFull,
            MaxForcNm       = c.Profile.MaxForcNm,
            SteeringRangeDeg = c.Profile.SteeringRangeDeg,
        }).ToList();

        return Task.FromResult(devices);
    }

    public Task OpenDeviceAsync(Device device)
    {
        var entry = Catalog.FirstOrDefault(c => c.Id == device.DeviceId);
        // Fall back to first profile if DeviceId not found (should not happen)
        var profile = entry.Profile ?? Catalog[0].Profile;

        lock (_lock)
        {
            _profile  = profile;
            _pos      = 0;
            _vel      = 0;
            _cmdForce = 0;
            _lastTick = DateTime.UtcNow;

            // Latency queue pre-filled with zeros (samples at 500 Hz)
            int samples = Math.Max(1, (int)Math.Round(profile.LatencyMs * 500.0 / 1000.0));
            _delay = new Queue<double>(Enumerable.Repeat(0.0, samples));
        }

        return Task.CompletedTask;
    }

    public Task CloseDeviceAsync()
    {
        lock (_lock) { _profile = null; }
        return Task.CompletedTask;
    }

    public void EmergencyStop()
    {
        lock (_lock)
        {
            _cmdForce = 0;
            _vel     *= 0.05; // rapid damping – simulates hardware cut-off
        }
    }

    public void SetForce(double normalizedForce)
    {
        lock (_lock)
        {
            _cmdForce = Math.Clamp(normalizedForce, -1.0, 1.0);
        }
    }

    public WheelTelemetry ReadTelemetry()
    {
        if (_profile is null)
            return new WheelTelemetry { Timestamp = DateTime.UtcNow };

        lock (_lock)
        {
            var now = DateTime.UtcNow;
            // Clamp dt to avoid instability after pauses
            double dt = Math.Clamp((now - _lastTick).TotalSeconds, 1e-4, 0.02);
            _lastTick = now;

            // Apply latency: dequeue oldest sample, enqueue current command
            double delayedForce = _delay!.Dequeue();
            _delay.Enqueue(_cmdForce);

            // Euler integration of second-order system
            // a = ( F·gain  −  b·v  −  k·pos ) / m
            double acc = (delayedForce * _profile.Gain
                         - _profile.Damping * _vel
                         - _profile.Spring  * _pos)
                         / _profile.Mass;

            _vel += acc * dt;
            _pos += _vel * dt;

            // Mechanical hard stops
            if (_pos >  1.0) { _pos =  1.0; _vel = Math.Min(0, _vel); }
            if (_pos < -1.0) { _pos = -1.0; _vel = Math.Max(0, _vel); }

            // Torque estimate proportional to commanded force and device rating
            double torqueNm = delayedForce * _profile.MaxForcNm;

            // Convert normalised velocity to deg/s
            double velDegS = _vel * (_profile.SteeringRangeDeg / 2.0);

            return new WheelTelemetry
            {
                Position     = _pos,
                VelocityDegS = velDegS,
                TorqueNm     = torqueNm,
                Timestamp    = now,
            };
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_lock) { _profile = null; }
        return ValueTask.CompletedTask;
    }
}
