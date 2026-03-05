using System.IO;
using Dapper;
using FFBAnalyzer.Models;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace FFBAnalyzer.Services;

/// <summary>
/// Persists sessions and runs to a local SQLite database.
/// Session metadata is stored in relational tables; raw time-series data
/// is stored as JSON blobs (avoids schema migrations for data columns).
/// </summary>
public sealed class SessionStorageService : IDisposable
{
    private readonly string _dbPath;
    private SqliteConnection? _conn;

    public SessionStorageService(string dbPath)
    {
        _dbPath = dbPath;
        EnsureDatabase();
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public async Task SaveSessionAsync(Session session)
    {
        await using var tx = (SqliteTransaction)await Connection.BeginTransactionAsync();
        try
        {
            await Connection.ExecuteAsync(
                """
                INSERT INTO sessions (session_id, name, description, created_at, updated_at,
                                      format_version, anonymous_user_id)
                VALUES (@SessionId, @Name, @Description, @CreatedAt, @UpdatedAt,
                        @FormatVersion, @AnonymousUserId)
                ON CONFLICT(session_id) DO UPDATE SET
                    name = excluded.name,
                    description = excluded.description,
                    updated_at = excluded.updated_at,
                    anonymous_user_id = excluded.anonymous_user_id
                """,
                new
                {
                    session.SessionId,
                    session.Name,
                    session.Description,
                    session.CreatedAt,
                    session.UpdatedAt,
                    session.FormatVersion,
                    session.AnonymousUserId
                }, tx);

            foreach (var run in session.Runs)
                await UpsertRunAsync(run, tx);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<Session>> LoadAllSessionsAsync()
    {
        var rows = await Connection.QueryAsync(
            "SELECT * FROM sessions ORDER BY created_at DESC");

        var sessions = new List<Session>();
        foreach (var r in rows)
        {
            var s = MapSession(r);
            var runs = await LoadRunsForSessionAsync(s.SessionId);
            s.Runs.AddRange(runs);
            sessions.Add(s);
        }
        return sessions;
    }

    public async Task<Session?> LoadSessionAsync(Guid sessionId)
    {
        var row = await Connection.QuerySingleOrDefaultAsync(
            "SELECT * FROM sessions WHERE session_id = @SessionId",
            new { SessionId = sessionId.ToString() });

        if (row == null) return null;
        var s = MapSession(row);
        var runs = await LoadRunsForSessionAsync(s.SessionId);
        s.Runs.AddRange(runs);
        return s;
    }

    public async Task DeleteSessionAsync(Guid sessionId)
    {
        await Connection.ExecuteAsync(
            "DELETE FROM runs WHERE session_id = @SessionId; DELETE FROM sessions WHERE session_id = @SessionId",
            new { SessionId = sessionId.ToString() });
    }

    // ── Internals ──────────────────────────────────────────────────────────

    private SqliteConnection Connection => _conn ??= OpenConnection();

    private SqliteConnection OpenConnection()
    {
        var c = new SqliteConnection($"Data Source={_dbPath}");
        c.Open();
        c.Execute("PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;");
        return c;
    }

    private void EnsureDatabase()
    {
        string dir = Path.GetDirectoryName(_dbPath)!;
        Directory.CreateDirectory(dir);

        using var c = new SqliteConnection($"Data Source={_dbPath}");
        c.Open();
        c.Execute("""
            CREATE TABLE IF NOT EXISTS sessions (
                session_id          TEXT PRIMARY KEY,
                name                TEXT NOT NULL,
                description         TEXT,
                created_at          TEXT NOT NULL,
                updated_at          TEXT NOT NULL,
                format_version      TEXT NOT NULL DEFAULT '1.0',
                anonymous_user_id   TEXT
            );
            CREATE TABLE IF NOT EXISTS runs (
                run_id              TEXT PRIMARY KEY,
                session_id          TEXT NOT NULL,
                test_id             TEXT NOT NULL,
                timestamp           TEXT NOT NULL,
                is_baseline         INTEGER NOT NULL DEFAULT 0,
                label               TEXT,
                notes               TEXT,
                was_aborted         INTEGER NOT NULL DEFAULT 0,
                clipping_detected   INTEGER NOT NULL DEFAULT 0,
                device_json         TEXT NOT NULL,
                test_def_json       TEXT NOT NULL,
                driver_settings_json TEXT,
                setting_changes_json TEXT,
                game_ffb_profile    TEXT,
                data_json           TEXT,
                metrics_json        TEXT,
                FOREIGN KEY(session_id) REFERENCES sessions(session_id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_runs_session ON runs(session_id);
            """);
    }

    private async Task UpsertRunAsync(Run run, SqliteTransaction tx)
    {
        await Connection.ExecuteAsync(
            """
            INSERT INTO runs (run_id, session_id, test_id, timestamp, is_baseline, label, notes,
                              was_aborted, clipping_detected, device_json, test_def_json,
                              driver_settings_json, setting_changes_json, game_ffb_profile,
                              data_json, metrics_json)
            VALUES (@RunId, @SessionId, @TestId, @Timestamp, @IsBaseline, @Label, @Notes,
                    @WasAborted, @ClippingDetected, @DeviceJson, @TestDefJson,
                    @DriverSettingsJson, @SettingChangesJson, @GameFfbProfile,
                    @DataJson, @MetricsJson)
            ON CONFLICT(run_id) DO UPDATE SET
                label = excluded.label, notes = excluded.notes,
                was_aborted = excluded.was_aborted,
                clipping_detected = excluded.clipping_detected,
                data_json = excluded.data_json,
                metrics_json = excluded.metrics_json
            """,
            new
            {
                RunId = run.RunId.ToString(),
                SessionId = run.SessionId.ToString(),
                TestId = run.TestId.ToString(),
                Timestamp = run.Timestamp.ToString("O"),
                run.IsBaseline,
                run.Label,
                run.Notes,
                run.WasAborted,
                run.ClippingDetected,
                DeviceJson = Serialize(run.Device),
                TestDefJson = Serialize(run.TestDefinition),
                DriverSettingsJson = Serialize(run.DriverSettings),
                SettingChangesJson = Serialize(run.SettingChanges),
                GameFfbProfile = run.GameOrFFBProfile,
                DataJson = Serialize(run.Data),
                MetricsJson = Serialize(run.Metrics)
            }, tx);
    }

    private async Task<IReadOnlyList<Run>> LoadRunsForSessionAsync(Guid sessionId)
    {
        var rows = await Connection.QueryAsync(
            "SELECT * FROM runs WHERE session_id = @SessionId ORDER BY timestamp",
            new { SessionId = sessionId.ToString() });

        return rows.Select(MapRun).ToList();
    }

    private static Session MapSession(dynamic r) => new()
    {
        SessionId = Guid.Parse((string)r.session_id),
        Name = (string)r.name,
        Description = (string?)r.description,
        CreatedAt = DateTime.Parse((string)r.created_at),
        UpdatedAt = DateTime.Parse((string)r.updated_at),
        FormatVersion = (string)r.format_version,
        AnonymousUserId = (string?)r.anonymous_user_id
    };

    private static Run MapRun(dynamic r) => new()
    {
        RunId = Guid.Parse((string)r.run_id),
        SessionId = Guid.Parse((string)r.session_id),
        TestId = Guid.Parse((string)r.test_id),
        Timestamp = DateTime.Parse((string)r.timestamp),
        IsBaseline = (int)r.is_baseline != 0,
        Label = (string?)r.label,
        Notes = (string?)r.notes,
        WasAborted = (int)r.was_aborted != 0,
        ClippingDetected = (int)r.clipping_detected != 0,
        Device = Deserialize<Device>((string)r.device_json) ?? new Device(),
        TestDefinition = Deserialize<TestDefinition>((string)r.test_def_json) ?? new TestDefinition(),
        DriverSettings = Deserialize<Dictionary<string, string>>((string?)r.driver_settings_json) ?? new(),
        SettingChanges = Deserialize<List<DriverSettingChange>>((string?)r.setting_changes_json) ?? new(),
        GameOrFFBProfile = (string?)r.game_ffb_profile,
        Data = Deserialize<TimeSeries>((string?)r.data_json) ?? new TimeSeries(),
        Metrics = Deserialize<MetricResult>((string?)r.metrics_json)
    };

    private static string Serialize<T>(T obj) =>
        JsonConvert.SerializeObject(obj, Formatting.None);

    private static T? Deserialize<T>(string? json) =>
        json == null ? default : JsonConvert.DeserializeObject<T>(json);

    public void Dispose()
    {
        _conn?.Close();
        _conn?.Dispose();
        _conn = null;
    }
}
