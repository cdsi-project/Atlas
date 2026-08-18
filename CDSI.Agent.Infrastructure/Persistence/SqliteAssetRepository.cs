using System.Globalization;
using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Scanning;
using Microsoft.Data.Sqlite;

namespace CDSI.Agent.Infrastructure.Persistence;

public sealed class SqliteAssetRepository : IAssetRepository
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public SqliteAssetRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        _databasePath = Path.GetFullPath(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_databasePath)
            ?? throw new InvalidOperationException("Database path has no parent directory.");
        Directory.CreateDirectory(directory);
        await DatabaseMigrator.MigrateAsync(_connectionString, cancellationToken);
    }

    public async Task<string> GetOrCreateDeviceIdAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var findCommand = connection.CreateCommand();
        findCommand.CommandText = "SELECT id FROM devices ORDER BY created_at LIMIT 1;";

        var existingId = await findCommand.ExecuteScalarAsync(cancellationToken) as string;
        if (!string.IsNullOrWhiteSpace(existingId))
        {
            return existingId;
        }

        var deviceId = Guid.NewGuid().ToString("D");
        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText =
            """
            INSERT INTO devices(id, name, platform, created_at)
            VALUES ($id, $name, $platform, $created_at);
            """;
        insertCommand.Parameters.AddWithValue("$id", deviceId);
        insertCommand.Parameters.AddWithValue("$name", Environment.MachineName);
        insertCommand.Parameters.AddWithValue("$platform", Environment.OSVersion.Platform.ToString());
        insertCommand.Parameters.AddWithValue("$created_at", DateTimeOffset.UtcNow.ToString("O"));
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);

        return deviceId;
    }

    public async Task<ScanRoot> GetOrCreateScanRootAsync(
        string path,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizePath(path);
        var pathKey = CreatePathKey(normalizedPath);
        var proposedId = Guid.NewGuid();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.CommandText =
                """
                INSERT OR IGNORE INTO scan_roots(
                    id, path, path_key, enabled, created_at, last_scanned_at)
                VALUES ($id, $path, $path_key, 1, $created_at, NULL);
                """;
            insertCommand.Parameters.AddWithValue("$id", proposedId.ToString("D"));
            insertCommand.Parameters.AddWithValue("$path", normalizedPath);
            insertCommand.Parameters.AddWithValue("$path_key", pathKey);
            insertCommand.Parameters.AddWithValue("$created_at", now.ToString("O"));
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var selectCommand = connection.CreateCommand();
        selectCommand.CommandText =
            """
            SELECT id, path, enabled, created_at, last_scanned_at
            FROM scan_roots
            WHERE path_key = $path_key;
            """;
        selectCommand.Parameters.AddWithValue("$path_key", pathKey);

        await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Unable to load the scan root after registration.");
        }

        return new ScanRoot(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetInt64(2) != 0,
            ParseTimestamp(reader.GetString(3)),
            reader.IsDBNull(4) ? null : ParseTimestamp(reader.GetString(4)));
    }

    public async Task CreateScanJobAsync(
        ScanJob job,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO scan_jobs(
                id, scan_root_id, status, started_at, finished_at,
                files_discovered, files_processed, errors, error_message)
            VALUES (
                $id, $scan_root_id, $status, $started_at, $finished_at,
                $files_discovered, $files_processed, $errors, $error_message);
            """;
        AddScanJobParameters(command, job);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateScanJobAsync(
        ScanJob job,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE scan_jobs
            SET status = $status,
                finished_at = $finished_at,
                files_discovered = $files_discovered,
                files_processed = $files_processed,
                errors = $errors,
                error_message = $error_message
            WHERE id = $id;
            """;
        AddScanJobParameters(command, job);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkScanRootCompletedAsync(
        Guid scanRootId,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE scan_roots
            SET last_scanned_at = $last_scanned_at
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", scanRootId.ToString("D"));
        command.Parameters.AddWithValue("$last_scanned_at", completedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkMissingLocalLocationsAsync(
        string deviceId,
        string rootPath,
        DateTimeOffset scanStartedAt,
        CancellationToken cancellationToken = default)
    {
        var normalizedRoot = NormalizePath(rootPath);
        var rootKey = CreatePathKey(normalizedRoot);
        var rootPrefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE asset_locations
            SET status = 'Missing'
            WHERE device_id = $device_id
              AND location_type = 'Local'
              AND last_seen_at < $scan_started_at
              AND (
                  path_key = $root_key
                  OR substr(path_key, 1, length($root_prefix)) = $root_prefix
              );
            """;
        command.Parameters.AddWithValue("$device_id", deviceId);
        command.Parameters.AddWithValue("$scan_started_at", scanStartedAt.ToString("O"));
        command.Parameters.AddWithValue("$root_key", rootKey);
        command.Parameters.AddWithValue("$root_prefix", CreatePathKey(rootPrefix));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssetListItem>> RegisterLocalFilesAsync(
        string deviceId,
        IReadOnlyCollection<DiscoveredFile> files,
        DateTimeOffset discoveredAt,
        CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
        {
            return [];
        }

        var registered = new List<AssetListItem>(files.Count);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedPath = NormalizePath(file.FullPath);
            var pathKey = CreatePathKey(normalizedPath);
            var assetId = await FindAssetIdAsync(
                connection,
                (SqliteTransaction)transaction,
                deviceId,
                pathKey,
                cancellationToken);

            if (assetId is null)
            {
                assetId = Guid.NewGuid();
                await InsertAssetAsync(
                    connection,
                    (SqliteTransaction)transaction,
                    assetId.Value,
                    file,
                    discoveredAt,
                    cancellationToken);
                await InsertLocationAsync(
                    connection,
                    (SqliteTransaction)transaction,
                    Guid.NewGuid(),
                    assetId.Value,
                    deviceId,
                    normalizedPath,
                    pathKey,
                    discoveredAt,
                    cancellationToken);
            }
            else
            {
                await UpdateAssetAsync(
                    connection,
                    (SqliteTransaction)transaction,
                    assetId.Value,
                    file,
                    discoveredAt,
                    cancellationToken);
                await UpdateLocationAsync(
                    connection,
                    (SqliteTransaction)transaction,
                    deviceId,
                    normalizedPath,
                    pathKey,
                    discoveredAt,
                    cancellationToken);
            }

            registered.Add(new AssetListItem(
                assetId.Value,
                file.OriginalFilename,
                file.Extension,
                file.MimeType,
                file.Size,
                file.ModifiedAt,
                normalizedPath,
                AssetLocationStatus.Available,
                AssetStatus.Indexed));
        }

        await transaction.CommitAsync(cancellationToken);
        return registered;
    }

    public async Task<IReadOnlyList<AssetListItem>> ListAssetsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var assets = new List<AssetListItem>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                a.id,
                a.original_filename,
                a.extension,
                a.mime_type,
                a.size,
                a.modified_at,
                l.path,
                l.status,
                a.status
            FROM assets a
            INNER JOIN asset_locations l ON l.asset_id = a.id
            WHERE l.location_type = 'Local'
            ORDER BY a.discovered_at DESC, a.original_filename
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            assets.Add(new AssetListItem(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt64(4),
                ParseTimestamp(reader.GetString(5)),
                reader.GetString(6),
                Enum.Parse<AssetLocationStatus>(reader.GetString(7)),
                Enum.Parse<AssetStatus>(reader.GetString(8))));
        }

        return assets;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<Guid?> FindAssetIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string deviceId,
        string pathKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT asset_id
            FROM asset_locations
            WHERE device_id = $device_id AND path_key = $path_key
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$device_id", deviceId);
        command.Parameters.AddWithValue("$path_key", pathKey);

        var value = await command.ExecuteScalarAsync(cancellationToken) as string;
        return string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value);
    }

    private static async Task InsertAssetAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid assetId,
        DiscoveredFile file,
        DateTimeOffset discoveredAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO assets(
                id, original_filename, mime_type, extension, size, sha256,
                created_at, modified_at, discovered_at, status)
            VALUES (
                $id, $original_filename, $mime_type, $extension, $size, NULL,
                $created_at, $modified_at, $discovered_at, $status);
            """;
        AddAssetParameters(command, assetId, file, discoveredAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateAssetAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid assetId,
        DiscoveredFile file,
        DateTimeOffset discoveredAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE assets
            SET original_filename = $original_filename,
                mime_type = $mime_type,
                extension = $extension,
                sha256 = CASE
                    WHEN size = $size AND modified_at = $modified_at THEN sha256
                    ELSE NULL
                END,
                size = $size,
                created_at = $created_at,
                modified_at = $modified_at,
                discovered_at = $discovered_at,
                status = $status
            WHERE id = $id;
            """;
        AddAssetParameters(command, assetId, file, discoveredAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertLocationAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid locationId,
        Guid assetId,
        string deviceId,
        string path,
        string pathKey,
        DateTimeOffset seenAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO asset_locations(
                id, asset_id, location_type, device_id, path, path_key,
                status, last_seen_at, last_verified_at)
            VALUES (
                $id, $asset_id, 'Local', $device_id, $path, $path_key,
                'Available', $last_seen_at, NULL);
            """;
        command.Parameters.AddWithValue("$id", locationId.ToString("D"));
        command.Parameters.AddWithValue("$asset_id", assetId.ToString("D"));
        command.Parameters.AddWithValue("$device_id", deviceId);
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$path_key", pathKey);
        command.Parameters.AddWithValue("$last_seen_at", seenAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateLocationAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string deviceId,
        string path,
        string pathKey,
        DateTimeOffset seenAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE asset_locations
            SET path = $path,
                status = 'Available',
                last_seen_at = $last_seen_at
            WHERE device_id = $device_id AND path_key = $path_key;
            """;
        command.Parameters.AddWithValue("$device_id", deviceId);
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$path_key", pathKey);
        command.Parameters.AddWithValue("$last_seen_at", seenAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddAssetParameters(
        SqliteCommand command,
        Guid assetId,
        DiscoveredFile file,
        DateTimeOffset discoveredAt)
    {
        command.Parameters.AddWithValue("$id", assetId.ToString("D"));
        command.Parameters.AddWithValue("$original_filename", file.OriginalFilename);
        command.Parameters.AddWithValue("$mime_type", (object?)file.MimeType ?? DBNull.Value);
        command.Parameters.AddWithValue("$extension", file.Extension);
        command.Parameters.AddWithValue("$size", file.Size);
        command.Parameters.AddWithValue("$created_at", file.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$modified_at", file.ModifiedAt.ToString("O"));
        command.Parameters.AddWithValue("$discovered_at", discoveredAt.ToString("O"));
        command.Parameters.AddWithValue("$status", AssetStatus.Indexed.ToString());
    }

    private static void AddScanJobParameters(SqliteCommand command, ScanJob job)
    {
        command.Parameters.AddWithValue("$id", job.Id.ToString("D"));
        command.Parameters.AddWithValue("$scan_root_id", job.ScanRootId.ToString("D"));
        command.Parameters.AddWithValue("$status", job.Status.ToString());
        command.Parameters.AddWithValue("$started_at", job.StartedAt.ToString("O"));
        command.Parameters.AddWithValue(
            "$finished_at",
            job.FinishedAt is null ? DBNull.Value : job.FinishedAt.Value.ToString("O"));
        command.Parameters.AddWithValue("$files_discovered", job.FilesDiscovered);
        command.Parameters.AddWithValue("$files_processed", job.FilesProcessed);
        command.Parameters.AddWithValue("$errors", job.Errors);
        command.Parameters.AddWithValue(
            "$error_message",
            (object?)job.ErrorMessage ?? DBNull.Value);
    }

    private static string NormalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string CreatePathKey(string path)
    {
        return path.ToUpperInvariant();
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        return DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
    }
}
