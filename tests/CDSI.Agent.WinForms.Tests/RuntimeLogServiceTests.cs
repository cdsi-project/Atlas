namespace CDSI.Agent.WinForms.Tests;

public sealed class RuntimeLogServiceTests
{
    [Fact]
    public void WriteError_CreatesReadableRedactedSessionLog()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var service = new RuntimeLogService(testRoot);

            service.WriteError(
                "backup failed",
                new InvalidOperationException(
                    "AccessKeySecret=very-secret https://example.com/a?token=hidden"));

            var content = service.ReadLogFile(service.CurrentLogPath);
            Assert.Contains("[INFO]", content);
            Assert.Contains("[ERROR]", content);
            Assert.Contains("backup failed", content);
            Assert.Contains("AccessKeySecret=[REDACTED]", content);
            Assert.DoesNotContain("very-secret", content);
            Assert.DoesNotContain("token=hidden", content);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public void GetLogFiles_IncludesSessionAndStartupLogsNewestFirst()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var service = new RuntimeLogService(testRoot);
            var startupLog = Path.Combine(service.LogDirectory, "startup-error-old.log");
            File.WriteAllText(startupLog, "old log");
            File.SetLastWriteTimeUtc(startupLog, DateTime.UtcNow.AddMinutes(-5));

            var files = service.GetLogFiles();

            Assert.Equal(service.CurrentLogPath, files[0]);
            Assert.Contains(startupLog, files);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public void ReadLogFile_RejectsFilesOutsideLogDirectory()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var service = new RuntimeLogService(testRoot);
            var outsidePath = Path.Combine(testRoot, "outside.log");
            File.WriteAllText(outsidePath, "outside");

            Assert.Throws<InvalidOperationException>(
                () => service.ReadLogFile(outsidePath));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public void RuntimeLogForm_LoadsTheCurrentSessionLog()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var service = new RuntimeLogService(testRoot);
            service.WriteInformation("visible message");
            using var form = new RuntimeLogForm(service);

            form.ReloadLogFiles();

            Assert.True(form.LogFileCount >= 1);
            Assert.Contains("visible message", form.LogContent);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static string CreateTestRoot()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            "cdsi-agent-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        return testRoot;
    }
}
