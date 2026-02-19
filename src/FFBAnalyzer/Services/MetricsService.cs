using FFBAnalyzer.Models;

namespace FFBAnalyzer.Services;

/// <summary>
/// Computes all objective metrics from a run's time-series data.
/// Pure static service – no dependencies, fully unit-testable.
/// </summary>
public static class MetricsService
{
    // Thresholds for semantic interpretation
    private const double FilteredCutoffThresholdHz = 25.0;
    private const double HighDampingSettlingMs = 500.0;
    private const double HighLatencyMs = 15.0;
    private const double HighResonanceDb = 3.0;
    private const double ClippingThreshold = 0.97;

    /// <summary>Compute metrics for a completed run, based on available data.</summary>
    public static MetricResult Compute(Run run)
    {
        var result = new MetricResult
        {
            RunId = run.RunId,
            TestType = run.TestDefinition.Type
        };

        var data = run.Data;
        if (data.Samples.Count < 10)
            return result; // not enough data

        switch (run.TestDefinition.Type)
        {
            case TestType.StepResponse:
                result.Metrics.AddRange(ComputeStepMetrics(run.TestDefinition, data));
                break;
            case TestType.SineSweep:
            case TestType.Chirp:
                result.Metrics.AddRange(ComputeSweepMetrics(run.TestDefinition, data));
                break;
            case TestType.SquareWave:
                result.Metrics.AddRange(ComputeSquareMetrics(run.TestDefinition, data));
                break;
            case TestType.Impulse:
                result.Metrics.AddRange(ComputeImpulseMetrics(run.TestDefinition, data));
                break;
            case TestType.ConstantTorque:
                result.Metrics.AddRange(ComputeConstantMetrics(run.TestDefinition, data));
                break;
            case TestType.FrictionEmulation:
                result.Metrics.AddRange(ComputeFrictionMetrics(run.TestDefinition, data));
                break;
        }

        // Clipping detection (universal)
        if (DetectClipping(data))
        {
            result.Metrics.Add(new Metric
            {
                Key = "clipping",
                DisplayName = "Clipping",
                Value = 1,
                Unit = "bool",
                Interpretation = InterpretationLabel.Clipping
            });
        }

        return result;
    }

    // ── Step Response ──────────────────────────────────────────────────────

    private static IEnumerable<Metric> ComputeStepMetrics(TestDefinition def, TimeSeries data)
    {
        // Use commanded as reference if no measured position
        double[] times = data.GetTimeArray();
        double[] response = data.GetPositionArray() ?? data.GetCommandedArray();
        double[] commanded = data.GetCommandedArray();

        double rampEnd = def.RampMs / 1000.0;
        double stepStart = rampEnd;
        double stepEnd = stepStart + def.HoldSec;
        double targetLevel = def.Intensity;

        // Find 10% and 90% crossing times
        int stepStartIdx = IndexAtTime(times, stepStart);
        int stepEndIdx = IndexAtTime(times, stepEnd);

        if (stepEndIdx <= stepStartIdx)
            yield break;

        var stepSlice = response[stepStartIdx..stepEndIdx];
        var timeSlice = times[stepStartIdx..stepEndIdx];
        double peak = stepSlice.Max();

        double t10 = CrossingTime(timeSlice, stepSlice, targetLevel * 0.10);
        double t90 = CrossingTime(timeSlice, stepSlice, targetLevel * 0.90);
        double riseTimeMs = (t90 - t10) * 1000.0;

        yield return new Metric
        {
            Key = "rise_time_ms",
            DisplayName = "Rise Time (10→90%)",
            Value = Math.Max(0, riseTimeMs),
            Unit = "ms",
            Description = "Time to rise from 10% to 90% of target"
        };

        // Overshoot
        double overshoot = peak > targetLevel
            ? (peak - targetLevel) / targetLevel * 100.0
            : 0.0;

        yield return new Metric
        {
            Key = "overshoot_pct",
            DisplayName = "Overshoot",
            Value = overshoot,
            Unit = "%",
            Description = "Peak above target as percentage of target",
            Interpretation = overshoot > 10 ? InterpretationLabel.MoreResonance : InterpretationLabel.None
        };

        // Settling time (within ±2% of target)
        double settlingBand = targetLevel * 0.02;
        double settlingTimeMs = double.NaN;
        for (int i = stepSlice.Length - 1; i >= 0; i--)
        {
            if (Math.Abs(stepSlice[i] - targetLevel) > settlingBand)
            {
                settlingTimeMs = i < timeSlice.Length - 1
                    ? (timeSlice[i + 1] - timeSlice[0]) * 1000.0
                    : (timeSlice[i] - timeSlice[0]) * 1000.0;
                break;
            }
        }
        if (double.IsNaN(settlingTimeMs)) settlingTimeMs = 0;

        yield return new Metric
        {
            Key = "settling_time_ms",
            DisplayName = "Settling Time (±2%)",
            Value = settlingTimeMs,
            Unit = "ms",
            Description = "Time to remain within ±2% of target",
            Interpretation = settlingTimeMs > HighDampingSettlingMs
                ? InterpretationLabel.MoreDamped
                : InterpretationLabel.LessDamped
        };

        // Steady-state error: average of last 20% of hold period
        int lastSegStart = stepStartIdx + (int)(stepSlice.Length * 0.8);
        double ssError = 0;
        if (lastSegStart < stepEndIdx)
        {
            double ssAvg = response[lastSegStart..stepEndIdx].Average();
            ssError = Math.Abs(ssAvg - targetLevel) / targetLevel;
        }
        yield return new Metric
        {
            Key = "steady_state_error",
            DisplayName = "Steady-State Error",
            Value = ssError,
            Unit = "fraction",
            Description = "Normalised deviation from target in steady state",
            Interpretation = ssError > 0.05 ? InterpretationLabel.SteadyStateError : InterpretationLabel.None
        };
    }

    // ── Sweep / Chirp ──────────────────────────────────────────────────────

    private static IEnumerable<Metric> ComputeSweepMetrics(TestDefinition def, TimeSeries data)
    {
        double[] times = data.GetTimeArray();
        double[] commanded = data.GetCommandedArray();
        double[] response = data.GetPositionArray() ?? commanded;

        int sampleRate = (int)Math.Round(data.SampleRateHz);
        if (sampleRate < 2) yield break;

        // Simple windowed RMS gain estimation per frequency band
        var (freqs, gains) = EstimateFrequencyResponse(times, commanded, response,
            def.FreqStartHz, def.FreqEndHz, def.DurationSec, sampleRate);

        if (freqs.Length == 0) yield break;

        // Reference gain = gain at lowest frequency (or mean of first 10%)
        int refCount = Math.Max(1, freqs.Length / 10);
        double refGain = gains[..refCount].Average();

        // Cutoff frequency (-3 dB point)
        double cutoffThreshold = refGain / Math.Sqrt(2);
        double cutoffHz = freqs[^1]; // default to max
        for (int i = 0; i < freqs.Length; i++)
        {
            if (gains[i] < cutoffThreshold)
            {
                cutoffHz = freqs[i];
                break;
            }
        }

        yield return new Metric
        {
            Key = "cutoff_freq_hz",
            DisplayName = "Cutoff Frequency (-3 dB)",
            Value = cutoffHz,
            Unit = "Hz",
            Description = "Frequency at which gain drops to 70.7% of low-frequency reference",
            Interpretation = cutoffHz < FilteredCutoffThresholdHz
                ? InterpretationLabel.MoreFiltered
                : InterpretationLabel.LessFiltered
        };

        // Peak resonance
        double maxGain = 0; double maxFreq = 0;
        for (int i = 1; i < freqs.Length - 1; i++)
        {
            if (gains[i] > maxGain && gains[i] > gains[i - 1] && gains[i] > gains[i + 1])
            {
                maxGain = gains[i]; maxFreq = freqs[i];
            }
        }

        double resonanceDb = refGain > 0 ? 20 * Math.Log10(maxGain / refGain) : 0;
        yield return new Metric
        {
            Key = "peak_resonance_hz",
            DisplayName = "Peak Resonance Frequency",
            Value = maxFreq,
            Unit = "Hz",
            Description = "Frequency of highest gain peak"
        };
        yield return new Metric
        {
            Key = "peak_resonance_db",
            DisplayName = "Peak Resonance (dB)",
            Value = resonanceDb,
            Unit = "dB",
            Interpretation = resonanceDb > HighResonanceDb
                ? InterpretationLabel.MoreResonance
                : InterpretationLabel.None
        };
    }

    // ── Square Wave ────────────────────────────────────────────────────────

    private static IEnumerable<Metric> ComputeSquareMetrics(TestDefinition def, TimeSeries data)
    {
        double[] times = data.GetTimeArray();
        double[] commanded = data.GetCommandedArray();
        double[] response = data.GetPositionArray() ?? commanded;

        // Estimate latency by cross-correlation lag
        double latencyMs = EstimateLatencyMs(times, commanded, response);
        yield return new Metric
        {
            Key = "latency_ms",
            DisplayName = "Estimated Latency",
            Value = latencyMs,
            Unit = "ms",
            Description = "Lag between command and response (cross-correlation peak)",
            Interpretation = latencyMs > HighLatencyMs
                ? InterpretationLabel.HigherLatency
                : InterpretationLabel.LowerLatency
        };

        // Smoothing index: ratio of HF content in response vs. command
        double smoothing = ComputeSmoothingIndex(commanded, response);
        yield return new Metric
        {
            Key = "smoothing_index",
            DisplayName = "Smoothing Index",
            Value = smoothing,
            Unit = "",
            Description = "0 = no smoothing, 1 = fully smoothed",
            Interpretation = smoothing > 0.5 ? InterpretationLabel.MoreFiltered : InterpretationLabel.None
        };
    }

    // ── Impulse ───────────────────────────────────────────────────────────

    private static IEnumerable<Metric> ComputeImpulseMetrics(TestDefinition def, TimeSeries data)
    {
        double[] times = data.GetTimeArray();
        double[] commanded = data.GetCommandedArray();
        double[] response = data.GetPositionArray() ?? commanded;

        double latencyMs = EstimateLatencyMs(times, commanded, response);
        yield return new Metric
        {
            Key = "latency_ms",
            DisplayName = "Estimated Latency",
            Value = latencyMs,
            Unit = "ms",
            Interpretation = latencyMs > HighLatencyMs
                ? InterpretationLabel.HigherLatency
                : InterpretationLabel.LowerLatency
        };

        // Oscillation after pulse: RMS in the 100ms window following the pulse
        int pulseEndIdx = IndexAtTime(times, def.DurationSec);
        int windowEnd = IndexAtTime(times, def.DurationSec + 0.1);
        double residual = 0;
        if (windowEnd > pulseEndIdx && pulseEndIdx < response.Length)
        {
            var postPulse = response[pulseEndIdx..Math.Min(windowEnd, response.Length)];
            residual = Math.Sqrt(postPulse.Average(v => v * v));
        }
        yield return new Metric
        {
            Key = "residual_oscillation",
            DisplayName = "Residual Oscillation (RMS)",
            Value = residual,
            Unit = "",
            Description = "RMS of response 0–100 ms after impulse ends"
        };
    }

    // ── Constant Torque ───────────────────────────────────────────────────

    private static IEnumerable<Metric> ComputeConstantMetrics(TestDefinition def, TimeSeries data)
    {
        double[] response = data.GetPositionArray() ?? data.GetCommandedArray();
        int n = response.Length;

        // Stability: std-dev in last 50% of run
        var tail = response[(n / 2)..];
        double mean = tail.Average();
        double std = Math.Sqrt(tail.Average(v => (v - mean) * (v - mean)));

        yield return new Metric
        {
            Key = "constant_std",
            DisplayName = "Stability (Std-Dev)",
            Value = std,
            Unit = "",
            Description = "Standard deviation in final 50% of run – lower is more stable"
        };

        double ssError = Math.Abs(mean - def.Intensity) / def.Intensity;
        yield return new Metric
        {
            Key = "steady_state_error",
            DisplayName = "Steady-State Error",
            Value = ssError,
            Unit = "fraction",
            Interpretation = ssError > 0.05 ? InterpretationLabel.SteadyStateError : InterpretationLabel.None
        };
    }

    // ── Friction Emulation ────────────────────────────────────────────────

    private static IEnumerable<Metric> ComputeFrictionMetrics(TestDefinition def, TimeSeries data)
    {
        double[] commanded = data.GetCommandedArray();
        double[] response = data.GetPositionArray() ?? commanded;

        // Deadband: estimated as the commanded amplitude at which response first appears
        // Simplified: compare RMS of response vs command
        double cmdRms = Math.Sqrt(commanded.Average(v => v * v));
        double rspRms = Math.Sqrt(response.Average(v => v * v));
        double gain = cmdRms > 0 ? rspRms / cmdRms : 0;

        yield return new Metric
        {
            Key = "friction_gain",
            DisplayName = "Friction Zone Gain",
            Value = gain,
            Unit = "",
            Description = "Response / Command ratio in micro-oscillation zone; < 1 indicates deadband/friction"
        };

        // Latency
        double latencyMs = EstimateLatencyMs(data.GetTimeArray(), commanded, response);
        yield return new Metric
        {
            Key = "latency_ms",
            DisplayName = "Estimated Latency",
            Value = latencyMs,
            Unit = "ms",
            Interpretation = latencyMs > HighLatencyMs
                ? InterpretationLabel.HigherLatency
                : InterpretationLabel.LowerLatency
        };
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static int IndexAtTime(double[] times, double t)
    {
        for (int i = 0; i < times.Length; i++)
            if (times[i] >= t) return i;
        return times.Length - 1;
    }

    private static double CrossingTime(double[] times, double[] values, double threshold)
    {
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i - 1] < threshold && values[i] >= threshold)
            {
                double frac = (threshold - values[i - 1]) / (values[i] - values[i - 1]);
                return times[i - 1] + frac * (times[i] - times[i - 1]);
            }
        }
        return times[0];
    }

    private static bool DetectClipping(TimeSeries data)
    {
        var arr = data.GetPositionArray() ?? data.GetForceArray();
        if (arr == null) return false;
        return arr.Any(v => Math.Abs(v) >= ClippingThreshold);
    }

    /// <summary>
    /// Estimates frequency response (gain vs frequency) using a sliding window DFT.
    /// Returns parallel arrays of frequencies and gains.
    /// </summary>
    private static (double[] Freqs, double[] Gains) EstimateFrequencyResponse(
        double[] times, double[] commanded, double[] response,
        double freqStart, double freqEnd, double duration, int sampleRate)
    {
        int numBins = 32;
        double[] freqs = new double[numBins];
        double[] gains = new double[numBins];

        int windowSamples = Math.Min(sampleRate, commanded.Length);

        for (int b = 0; b < numBins; b++)
        {
            double freq = freqStart + (freqEnd - freqStart) * b / (numBins - 1);
            freqs[b] = freq;

            // Estimate time when this frequency appears in a linear sweep
            double tFreq = duration * (freq - freqStart) / (freqEnd - freqStart);
            int centerIdx = IndexAtTime(times, tFreq);
            int halfWin = windowSamples / 4;
            int startIdx = Math.Max(0, centerIdx - halfWin);
            int endIdx = Math.Min(commanded.Length - 1, centerIdx + halfWin);

            double cmdRms = 0, rspRms = 0;
            int count = endIdx - startIdx + 1;
            for (int i = startIdx; i <= endIdx; i++)
            {
                cmdRms += commanded[i] * commanded[i];
                rspRms += response[i] * response[i];
            }
            cmdRms = Math.Sqrt(cmdRms / count);
            rspRms = Math.Sqrt(rspRms / count);
            gains[b] = cmdRms > 1e-6 ? rspRms / cmdRms : 0;
        }

        return (freqs, gains);
    }

    /// <summary>Cross-correlation based latency estimate between two signals.</summary>
    private static double EstimateLatencyMs(double[] times, double[] a, double[] b)
    {
        if (a.Length < 2 || b.Length < 2) return 0;
        int n = Math.Min(a.Length, b.Length);
        int maxLag = Math.Min(n / 4, 500); // max ±lag samples

        double bestCorr = double.MinValue;
        int bestLag = 0;

        for (int lag = 0; lag <= maxLag; lag++)
        {
            double sum = 0;
            int cnt = n - lag;
            for (int i = 0; i < cnt; i++)
                sum += a[i] * b[i + lag];
            if (sum > bestCorr) { bestCorr = sum; bestLag = lag; }
        }

        double dt = times.Length > 1 ? times[1] - times[0] : 0.002;
        return bestLag * dt * 1000.0;
    }

    /// <summary>
    /// Smoothing index: 1 - (HF energy of response / HF energy of command).
    /// HF = upper 25% of Nyquist band (simple finite difference proxy).
    /// </summary>
    private static double ComputeSmoothingIndex(double[] commanded, double[] response)
    {
        int n = Math.Min(commanded.Length, response.Length);
        if (n < 4) return 0;

        double cmdHf = 0, rspHf = 0;
        for (int i = 1; i < n; i++)
        {
            double dc = commanded[i] - commanded[i - 1];
            double dr = response[i] - response[i - 1];
            cmdHf += dc * dc;
            rspHf += dr * dr;
        }
        return cmdHf > 1e-9 ? Math.Clamp(1.0 - rspHf / cmdHf, 0, 1) : 0;
    }
}
