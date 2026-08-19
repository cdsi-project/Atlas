using CDSI.Agent.Application.Fingerprints;
using CDSI.Agent.Application.Metadata;
using CDSI.Agent.Application.Scanning;
using CDSI.Agent.Application.Storage;
using CDSI.Agent.Application.Text;
using CDSI.Agent.Application.Workspaces;
using CDSI.Agent.Infrastructure.FileSystem;
using CDSI.Agent.Infrastructure.Fingerprints;
using CDSI.Agent.Infrastructure.Metadata;
using CDSI.Agent.Infrastructure.Persistence;
using CDSI.Agent.Infrastructure.Security;
using CDSI.Agent.Infrastructure.Text;

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
        var fingerprintEngine = new Sha256FileFingerprintService();
        var scanService = new ScanApplicationService(new FileSystemScanner(), repository);
        var workspaceService = new WorkspaceApplicationService(
            repository,
            new WorkspaceProvisioner());
        var scanRootService = new ScanRootManagementService(repository);
        var storageService = new ObjectStorageProfileService(
            repository,
            new WindowsCredentialSecretStore());
        var fingerprintService = new FingerprintApplicationService(
            fingerprintEngine,
            repository);
        var metadataService = new MetadataExtractionApplicationService(
            [
                new TagLibMetadataExtractor(),
                new GenericMetadataExtractor()
            ],
            repository);
        var textService = new TextExtractionApplicationService(
            [
                new MarkdownTextExtractor(),
                new PlainTextExtractor(),
                new GenericTextExtractor()
            ],
            repository);
        System.Windows.Forms.Application.Run(new MainForm(
            scanService,
            fingerprintService,
            metadataService,
            textService,
            workspaceService,
            scanRootService,
            storageService,
            dataDirectory));
    }
}
