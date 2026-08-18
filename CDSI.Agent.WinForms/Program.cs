using CDSI.Agent.Application.Scanning;
using CDSI.Agent.Infrastructure.FileSystem;
using CDSI.Agent.Infrastructure.Fingerprints;
using CDSI.Agent.Infrastructure.Persistence;

namespace CDSI.Agent.WinForms;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CDSI");
        var repository = new SqliteAssetRepository(Path.Combine(dataDirectory, "cdsi.db"));
        var scanService = new ScanApplicationService(
            new FileSystemScanner(),
            new Sha256FileFingerprintService(),
            repository);
        System.Windows.Forms.Application.Run(new MainForm(scanService, dataDirectory));
    }
}
