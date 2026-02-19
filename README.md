# FFB Analyzer – Wheel Comparison Tool

A Windows desktop application for **measuring, comparing, and sharing Force Feedback (FFB) responses** from sim racing steering wheels.

## Purpose

Different sim racing wheels produce different FFB sensations for the same settings. FFB Analyzer lets you:

1. Run **standardised signal tests** (step response, sine sweep, square wave, etc.) outside the game
2. **Compare before/after** driver setting changes with objective graphs and metrics
3. **Share results** with friends to compare wheels across brands (Fanatec, Simagic, Moza, Simucube, etc.)

---

## Features

### Tests
| Test | Description |
|------|-------------|
| **Step Response** | 0 → target intensity, hold, return. Measures rise time, overshoot, settling |
| **Sine Sweep** | 1–60 Hz frequency sweep. Reveals filters and resonance |
| **Chirp** | Exponential frequency sweep (5–80 Hz). Better high-frequency coverage |
| **Square Wave** | Fixed-frequency square signal. Shows smoothing and latency |
| **Impulse** | Brief pulse. Measures transient response and residual oscillation |
| **Constant Torque** | Hold a constant force. Measures steady-state error and stability |
| **Friction Emulation** | Micro-oscillations near zero. Reveals deadband and friction zone |

### Metrics (automatic)
- Rise time (10→90%), overshoot %, settling time (±2%), steady-state error
- Cutoff frequency (−3 dB), resonance peaks and frequency
- Estimated latency (cross-correlation), smoothing index
- Clipping detection

### Semantic Interpretations
- "High damping / inertia detected"
- "Strong filter / smoothing (cutoff ≈ X Hz)"
- "Resonance peak at X Hz"
- "Increased latency detected"

### Comparison
- Overlay multiple runs on the same chart
- Difference curve (B − A)
- Side-by-side metric table

### Export / Import
- **JSON** – full session with all data and metrics
- **CSV** – time-series data for a single run
- **ZIP** – JSON + individual CSVs bundled

---

## Requirements

- **Windows 10 / 11** (64-bit)
- **.NET 8 Runtime** (included in self-contained builds)
- A **DirectInput-compatible FFB wheel**

---

## Architecture

```
src/
├── FFBAnalyzer/                    # WPF application
│   ├── Models/                     # Data models (Device, Run, Session, …)
│   ├── Services/
│   │   ├── FFBSignalGenerator.cs   # Pure signal math (testable)
│   │   ├── MetricsService.cs       # Pure metrics computation (testable)
│   │   ├── TestRunnerService.cs    # Orchestrates test execution
│   │   ├── SessionStorageService.cs# SQLite persistence
│   │   └── ExportService.cs        # JSON / CSV / ZIP export-import
│   ├── Adapters/
│   │   ├── IDeviceAdapter.cs       # Contract for all device backends
│   │   └── DirectInputAdapter.cs   # Generic Windows DirectInput adapter
│   ├── ViewModels/                 # MVVM ViewModels (CommunityToolkit.Mvvm)
│   ├── Views/                      # WPF UserControls
│   └── Converters/                 # WPF value converters
└── FFBAnalyzer.Tests/              # xUnit unit tests
    ├── MetricsServiceTests.cs
    └── ExportServiceTests.cs
```

### Telemetry Modes

| Mode | Available data | Typical wheels |
|------|---------------|----------------|
| **A – Full** | Position + velocity + torque | SDK-equipped (SimuCube, VRS) |
| **B – Partial** | Position only (HID) | Most DirectInput wheels |
| **C – Software only** | None | Very basic HID devices |

---

## Test Batteries

| Battery | Duration | Tests |
|---------|----------|-------|
| Quick | ~2 min | Step, Sweep, Square |
| Standard | ~5 min | Step ×2, Sweep, Chirp, Constant |
| Deep | ~12 min | All 7 test types |

---

## Safety

- Emergency **STOP** button always visible during a test
- Configurable **maximum intensity limit** (default 80%)
- **Countdown** (3…2…1) before each test
- **Watchdog**: FFB is forced off if the UI freezes
- Test ramp option to avoid sudden jolts

---

## Development

```bash
# Build
dotnet build FFBAnalyzer.sln

# Run tests
dotnet test src/FFBAnalyzer.Tests/FFBAnalyzer.Tests.csproj

# Run application (Windows required)
dotnet run --project src/FFBAnalyzer/FFBAnalyzer.csproj
```

### Technology Stack
- **C# 12 / .NET 8** – WPF desktop application
- **CommunityToolkit.Mvvm** – MVVM pattern
- **SharpDX.DirectInput** – FFB device communication
- **LiveChartsCore** – real-time charts
- **Microsoft.Data.Sqlite + Dapper** – local storage
- **Newtonsoft.Json** – serialization
- **SharpZipLib** – ZIP packaging
- **xUnit + FluentAssertions** – unit tests

---

## Protocol Recommendation

For reliable comparisons between users:

1. **Calibrate** the wheel before each session
2. Use the **Standard battery** as the baseline
3. Change **one setting at a time** and run a new batch
4. Export the ZIP and share – the recipient imports and can overlay your curves with theirs

---

## Roadmap (v2)

- **Settings assistant** – automatic suggestions ("reduce damping", "lower filter")
- **Community mode** – aggregate data per wheel model
- **In-game telemetry** – capture live FFB during a race lap
- **Simucube / VRS SDK adapters** – full torque telemetry
