using System.Text;

namespace CDSI.Agent.WinForms;

public sealed class RuntimeLogService
{
    private readonly object _writeLock = new();

    public RuntimeLogService(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        LogDirectory = Path.Combine(Path.GetFullPath(dataDirectory), "Logs");
        CurrentLogPath = Path.Combine(
            LogDirectory,
            $"runtime-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}.log");

        WriteInformation(
            $"CDSI Beacon v{MainForm.GetApplicationVersion()} 启动；" +
            $"OS={Environment.OSVersion}；Runtime={Environment.Version}");
    }

    public string LogDirectory { get; }

    public string CurrentLogPath { get; }

    public void WriteInformation(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        WriteEntry("INFO", message);
    }

    public void WriteError(string context, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        ArgumentNullException.ThrowIfNull(exception);
        WriteEntry("ERROR", $"{context}{Environment.NewLine}{exception}");
    }

    public IReadOnlyList<string> GetLogFiles()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            return Directory
                .EnumerateFiles(LogDirectory, "*.log", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ThenByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    public string ReadLogFile(string logPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);
        var fullPath = Path.GetFullPath(logPath);
        var relativePath = Path.GetRelativePath(LogDirectory, fullPath);
        if (Path.IsPathRooted(relativePath) ||
            relativePath.Equals("..", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("只能读取 Beacon 日志目录中的文件。");
        }

        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private void WriteEntry(string level, string message)
    {
        try
        {
            var redacted = StartupFailureReporter.RedactSensitiveText(message);
            var entry = $"{DateTimeOffset.Now:O} [{level}] {redacted}{Environment.NewLine}";
            lock (_writeLock)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(CurrentLogPath, entry, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never prevent Beacon from starting or completing an operation.
        }
    }
}
