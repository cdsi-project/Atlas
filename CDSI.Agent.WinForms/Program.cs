using CDSI.Agent.Application.Collections;
using CDSI.Agent.Application.Fingerprints;
using CDSI.Agent.Application.Metadata;
using CDSI.Agent.Application.OpenWeb;
using CDSI.Agent.Application.Scanning;
using CDSI.Agent.Application.Storage;
using CDSI.Agent.Application.Transfers;
using CDSI.Agent.Application.Workspaces;
using CDSI.Agent.Infrastructure.FileSystem;
using CDSI.Agent.Infrastructure.Fingerprints;
using CDSI.Agent.Infrastructure.Metadata;
using CDSI.Agent.Infrastructure.OpenWeb;
using CDSI.Agent.Infrastructure.Persistence;
using CDSI.Agent.Infrastructure.Security;
using CDSI.Agent.Infrastructure.Storage;

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
        var workspaceProvisioner = new WorkspaceProvisioner();
        var workspaceService = new WorkspaceApplicationService(
            repository,
            workspaceProvisioner);
        var scanRootService = new ScanRootManagementService(repository);
        var volumeReconciliationService = new LocalVolumeReconciliationService(
            new WindowsLocalVolumeProvider(),
            repository);
        var secretStore = new WindowsCredentialSecretStore();
        var storageService = new ObjectStorageProfileService(
            repository,
            secretStore);
        var openWebSettingsService = new OpenWebSettingsService(
            repository,
            secretStore);
        var openWebPublishingService = new OpenWebArticlePublishingService(
            openWebSettingsService,
            repository,
            new LocalOpenWebArticleContentReader(),
            new WordPressArticlePublisher(new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            }));
        var objectStorageBackupService = new ObjectStorageBackupService(
            repository,
            repository,
            storageService,
            fingerprintEngine,
            [new AliyunOssStorageAdapter()]);
        var transferService = new ManagedAssetTransferService(
            repository,
            workspaceProvisioner,
            new VerifiedManagedFileTransfer());
        var assetCollectionService = new AssetCollectionService(repository);
        var fingerprintService = new FingerprintApplicationService(
            fingerprintEngine,
            repository);
        var metadataService = new MetadataExtractionApplicationService(
            [
                new TagLibMetadataExtractor(),
                new GenericMetadataExtractor()
            ],
            repository);
        System.Windows.Forms.Application.Run(new MainForm(
            scanService,
            fingerprintService,
            metadataService,
            workspaceService,
            scanRootService,
            volumeReconciliationService,
            storageService,
            openWebSettingsService,
            openWebPublishingService,
            objectStorageBackupService,
            assetCollectionService,
            transferService,
            dataDirectory));
    }
}
