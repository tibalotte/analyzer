using System.IO;
using FFBAnalyzer.Models;
using FFBAnalyzer.Services;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace FFBAnalyzer.Tests;

public class ExportServiceTests
{
    private static Session BuildTestSession()
    {
        var session = new Session
        {
            SessionId = Guid.NewGuid(),
            Name = "Test Session",
            Description = "Unit test session"
        };

        var run = new Run
        {
            RunId = Guid.NewGuid(),
            SessionId = session.SessionId,
            TestId = Guid.NewGuid(),
            IsBaseline = true,
            TestDefinition = TestDefinition.StepResponse(0.20),
            Device = new Device { Name = "Test Wheel", VendorId = 0x046D, ProductId = 0xC262 },
            Timestamp = DateTime.UtcNow
        };

        // Add some fake time-series
        for (int i = 0; i < 50; i++)
        {
            run.Data.Samples.Add(new Sample
            {
                TimeS = i * 0.002,
                CommandedForce = 0.20 * Math.Sin(i * 0.1),
                MeasuredPosition = 0.18 * Math.Sin(i * 0.1),
                MeasuredForce = null
            });
        }

        session.Runs.Add(run);
        return session;
    }

    [Fact]
    public async Task ExportJson_ThenImportJson_ProducesEquivalentSession()
    {
        var svc = new ExportService();
        var original = BuildTestSession();

        string path = Path.GetTempFileName() + ".json";
        try
        {
            await svc.ExportSessionJsonAsync(original, path);
            File.Exists(path).Should().BeTrue();

            var imported = await svc.ImportSessionJsonAsync(path);

            imported.Should().NotBeNull();
            imported!.Name.Should().Be(original.Name);
            imported.Runs.Should().HaveCount(original.Runs.Count);
            imported.Runs[0].TestDefinition.Type
                .Should().Be(original.Runs[0].TestDefinition.Type);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportJson_ProducedFileIsValidJson()
    {
        var svc = new ExportService();
        var session = BuildTestSession();

        string path = Path.GetTempFileName() + ".json";
        try
        {
            await svc.ExportSessionJsonAsync(session, path);
            string content = await File.ReadAllTextAsync(path);

            Action parse = () => JsonConvert.DeserializeObject(content);
            parse.Should().NotThrow("exported file must be valid JSON");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportCsv_ContainsHeaderAndRows()
    {
        var svc = new ExportService();
        var session = BuildTestSession();
        var run = session.Runs[0];

        string path = Path.GetTempFileName() + ".csv";
        try
        {
            await svc.ExportRunCsvAsync(run, path);
            var lines = await File.ReadAllLinesAsync(path);

            lines[0].Should().Contain("time_s");
            lines[0].Should().Contain("commanded_force");
            lines[0].Should().Contain("measured_position");
            lines.Length.Should().Be(run.Data.Samples.Count + 1, // header + rows
                because: "CSV should have one row per sample plus header");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportJson_WithIncompatibleVersion_ThrowsNotSupported()
    {
        var svc = new ExportService();
        var session = BuildTestSession();
        session.FormatVersion = "99.0"; // incompatible

        string path = Path.GetTempFileName() + ".json";
        try
        {
            await svc.ExportSessionJsonAsync(session, path);

            // ImportSessionJsonAsync should throw because version != 1.0
            Func<Task> act = () => svc.ImportSessionJsonAsync(path);
            await act.Should().ThrowAsync<NotSupportedException>();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Import_AssignsNewIds_ToAvoidLocalConflicts()
    {
        var svc = new ExportService();
        var original = BuildTestSession();

        string path = Path.GetTempFileName() + ".json";
        try
        {
            await svc.ExportSessionJsonAsync(original, path);
            var imported = await svc.ImportSessionJsonAsync(path);

            imported!.SessionId.Should().NotBe(original.SessionId,
                because: "import should assign a new session ID");
            imported.Runs[0].RunId.Should().NotBe(original.Runs[0].RunId,
                because: "import should assign new run IDs");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
