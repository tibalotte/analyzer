namespace FFBAnalyzer.Models;

/// <summary>
/// A session groups related runs for one comparison scenario
/// (e.g. "Damping comparison – Fanatec DD1").
/// </summary>
public class Session
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Session";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Format version for forward/backward compatibility checks.</summary>
    public string FormatVersion { get; set; } = "1.0";

    /// <summary>Opaque anonymous user ID (opt-in, generated locally).</summary>
    public string? AnonymousUserId { get; set; }

    public List<Run> Runs { get; set; } = new();

    // ── Convenience queries ────────────────────────────────────────────────

    public Run? Baseline => Runs.FirstOrDefault(r => r.IsBaseline && !r.WasAborted);

    public IEnumerable<Run> CompletedRuns =>
        Runs.Where(r => !r.WasAborted).OrderBy(r => r.Timestamp);

    public IEnumerable<Run> RunsForTest(Guid testId) =>
        CompletedRuns.Where(r => r.TestId == testId);
}
