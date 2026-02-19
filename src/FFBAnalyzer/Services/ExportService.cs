using System.IO;
using System.Text;
using FFBAnalyzer.Models;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FFBAnalyzer.Services;

/// <summary>
/// Handles export (JSON, CSV, ZIP) and import of sessions / runs.
/// All file I/O is async. No WPF dependency – fully unit-testable.
/// </summary>
public sealed class ExportService
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented,
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        DateFormatHandling = DateFormatHandling.IsoDateFormat
    };

    // ── Export ─────────────────────────────────────────────────────────────

    /// <summary>Export a full session to a JSON file.</summary>
    public async Task ExportSessionJsonAsync(Session session, string filePath)
    {
        // Strip absolute user paths and optionally PII
        var sanitised = Sanitise(session);
        string json = JsonConvert.SerializeObject(sanitised, JsonSettings);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
    }

    /// <summary>Export the time-series data of a single run to CSV.</summary>
    public async Task ExportRunCsvAsync(Run run, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("time_s,commanded_force,measured_position,measured_force,measured_velocity");

        foreach (var s in run.Data.Samples)
        {
            sb.AppendLine(string.Join(',',
                F(s.TimeS),
                F(s.CommandedForce),
                F(s.MeasuredPosition),
                F(s.MeasuredForce),
                F(s.MeasuredVelocity)));
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>Export a session as a ZIP containing JSON + one CSV per run.</summary>
    public async Task ExportSessionZipAsync(Session session, string zipPath)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(
            JsonConvert.SerializeObject(Sanitise(session), JsonSettings));

        await using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write);
        using var zip = new ZipOutputStream(fileStream);
        zip.SetLevel(6);

        // session.json
        var entry = new ZipEntry("session.json") { DateTime = DateTime.Now };
        zip.PutNextEntry(entry);
        await zip.WriteAsync(jsonBytes);
        zip.CloseEntry();

        // CSV per run
        foreach (var run in session.Runs)
        {
            var csvBytes = Encoding.UTF8.GetBytes(BuildCsv(run));
            var csvEntry = new ZipEntry($"runs/{run.RunId:N}.csv") { DateTime = DateTime.Now };
            zip.PutNextEntry(csvEntry);
            await zip.WriteAsync(csvBytes);
            zip.CloseEntry();
        }

        zip.Finish();
    }

    // ── Import ─────────────────────────────────────────────────────────────

    /// <summary>Import a JSON file as a session. Returns null on version mismatch.</summary>
    public async Task<Session?> ImportSessionJsonAsync(string filePath)
    {
        string json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        var session = JsonConvert.DeserializeObject<Session>(json, JsonSettings);
        if (session == null) return null;

        // Version gate
        if (!string.IsNullOrEmpty(session.FormatVersion) &&
            session.FormatVersion != "1.0")
        {
            throw new NotSupportedException(
                $"Session format version '{session.FormatVersion}' is not supported by this version (1.0).");
        }

        // Reassign new IDs to avoid clashes with locally stored data
        session.SessionId = Guid.NewGuid();
        foreach (var run in session.Runs)
        {
            run.RunId = Guid.NewGuid();
            run.SessionId = session.SessionId;
        }

        return session;
    }

    /// <summary>Import a ZIP session package.</summary>
    public async Task<Session?> ImportSessionZipAsync(string zipPath)
    {
        using var zip = new ZipFile(zipPath);
        var jsonEntry = zip.Cast<ZipEntry>().FirstOrDefault(e => e.Name == "session.json");
        if (jsonEntry == null)
            throw new InvalidDataException("No session.json found in the ZIP package.");

        using var stream = zip.GetInputStream(jsonEntry);
        using var reader = new StreamReader(stream);
        string json = await reader.ReadToEndAsync();

        var session = JsonConvert.DeserializeObject<Session>(json, JsonSettings);
        if (session == null) return null;

        session.SessionId = Guid.NewGuid();
        foreach (var run in session.Runs)
        {
            run.RunId = Guid.NewGuid();
            run.SessionId = session.SessionId;
        }
        return session;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static Session Sanitise(Session session)
    {
        // Deep clone via JSON round-trip then strip PII
        var clone = JsonConvert.DeserializeObject<Session>(
            JsonConvert.SerializeObject(session, JsonSettings), JsonSettings)!;
        clone.AnonymousUserId ??= "anon";
        return clone;
    }

    private static string BuildCsv(Run run)
    {
        var sb = new StringBuilder();
        sb.AppendLine("time_s,commanded_force,measured_position,measured_force,measured_velocity");
        foreach (var s in run.Data.Samples)
            sb.AppendLine(string.Join(',',
                F(s.TimeS), F(s.CommandedForce),
                F(s.MeasuredPosition), F(s.MeasuredForce), F(s.MeasuredVelocity)));
        return sb.ToString();
    }

    private static string F(double v) => v.ToString("G6");
    private static string F(double? v) => v.HasValue ? v.Value.ToString("G6") : string.Empty;
}
