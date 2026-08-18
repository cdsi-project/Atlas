using System.Globalization;
using System.Text.Json;
using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Duplicates;
using CDSI.Agent.Core.Fingerprints;
using CDSI.Agent.Core.Metadata;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Core.Text;
using Microsoft.Data.Sqlite;

namespace CDSI.Agent.Infrastructure.Persistence;

public sealed partial class SqliteAssetRepository : IAssetRepository
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

    public async Task<IReadOnlyList<RegisteredLocalAsset>> RegisterLocalFilesAsync(
        string deviceId,
        IReadOnlyCollection<DiscoveredFile> files,
        DateTimeOffset discoveredAt,
        CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
        {
            return [];
        }

        var registered = new List<RegisteredLocalAsset>(files.Count);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedPath = NormalizePath(file.FullPath);
            var pathKey = CreatePathKey(normalizedPath);
            var existingAsset = await FindAssetAsync(
                connection,
                (SqliteTransaction)transaction,
                deviceId,
                pathKey,
                cancellationToken);
            var assetId = existingAsset?.Id;
            var requiresFingerprint = existingAsset is null ||
                existingAsset.Size != file.Size ||
                existingAsset.ModifiedAt != file.ModifiedAt ||
                string.IsNullOrWhiteSpace(existingAsset.Sha256);

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

            registered.Add(new RegisteredLocalAsset(
                assetId.Value,
                file with { FullPath = normalizedPath },
                requiresFingerprint));
        }

        await transaction.CommitAsync(cancellationToken);
        return registered;
    }

    public async Task<bool> SaveSha256Async(
        Guid assetId,
        long expectedSize,
        DateTimeOffset expectedModifiedAt,
        string sha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        if (sha256.Length != 64)
        {
            throw new ArgumentException("A SHA-256 value must contain 64 hexadecimal characters.", nameof(sha256));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE assets
            SET sha256 = $sha256
            WHERE id = $id
              AND size = $expected_size
              AND modified_at = $expected_modified_at;
            """;
        command.Parameters.AddWithValue("$sha256", sha256.ToLowerInvariant());
        command.Parameters.AddWithValue("$id", assetId.ToString("D"));
        command.Parameters.AddWithValue("$expected_size", expectedSize);
        command.Parameters.AddWithValue(
            "$expected_modified_at",
            expectedModifiedAt.ToString("O"));

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<FingerprintWorkSummary> GetFingerprintWorkSummaryAsync(
        FingerprintMode mode,
        CancellationToken cancellationToken = default)
    {
        var modeFilter = GetFingerprintModeFilter(mode);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT COUNT(*), COALESCE(SUM(a.size), 0)
            FROM assets a
            WHERE a.sha256 IS NULL
              AND EXISTS (
                  SELECT 1
                  FROM asset_locations l
                  WHERE l.asset_id = a.id
                    AND l.location_type = 'Local'
                    AND l.status = 'Available'
              )
              {modeFilter};
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new FingerprintWorkSummary(0, 0);
        }

        return new FingerprintWorkSummary(reader.GetInt32(0), reader.GetInt64(1));
    }

    public async Task<IReadOnlyList<FingerprintCandidate>> ListFingerprintCandidatesAsync(
        FingerprintMode mode,
        Guid? afterAssetId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var modeFilter = GetFingerprintModeFilter(mode);
        var candidates = new List<FingerprintCandidate>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT
                a.id,
                a.original_filename,
                a.extension,
                a.mime_type,
                a.size,
                a.created_at,
                a.modified_at,
                MIN(l.path)
            FROM assets a
            INNER JOIN asset_locations l ON l.asset_id = a.id
            WHERE a.sha256 IS NULL
              AND l.location_type = 'Local'
              AND l.status = 'Available'
              AND ($after_asset_id IS NULL OR a.id > $after_asset_id)
              {modeFilter}
            GROUP BY
                a.id,
                a.original_filename,
                a.extension,
                a.mime_type,
                a.size,
                a.created_at,
                a.modified_at
            ORDER BY a.id
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue(
            "$after_asset_id",
            afterAssetId is null ? DBNull.Value : afterAssetId.Value.ToString("D"));
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(new FingerprintCandidate(
                Guid.Parse(reader.GetString(0)),
                new DiscoveredFile(
                    reader.GetString(7),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetInt64(4),
                    ParseTimestamp(reader.GetString(5)),
                    ParseTimestamp(reader.GetString(6)))));
        }

        return candidates;
    }

    public async Task<MetadataWorkSummary> GetMetadataWorkSummaryAsync(
        int pipelineVersion,
        CancellationToken cancellationToken = default)
    {
        if (pipelineVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pipelineVersion));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM assets a
            WHERE EXISTS (
                SELECT 1
                FROM asset_locations l
                WHERE l.asset_id = a.id
                  AND l.location_type = 'Local'
                  AND l.status = 'Available'
            )
              AND NOT EXISTS (
                  SELECT 1
                  FROM asset_metadata m
                  WHERE m.asset_id = a.id
                    AND m.pipeline_version = $pipeline_version
                    AND m.source_size = a.size
                    AND m.source_modified_at = a.modified_at
              );
            """;
        command.Parameters.AddWithValue("$pipeline_version", pipelineVersion);

        var count = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        return new MetadataWorkSummary(count);
    }

    public async Task<IReadOnlyList<MetadataCandidate>> ListMetadataCandidatesAsync(
        int pipelineVersion,
        Guid? afterAssetId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (pipelineVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pipelineVersion));
        }

        if (limit is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var candidates = new List<MetadataCandidate>();
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
                a.created_at,
                a.modified_at,
                MIN(l.path)
            FROM assets a
            INNER JOIN asset_locations l ON l.asset_id = a.id
            WHERE l.location_type = 'Local'
              AND l.status = 'Available'
              AND ($after_asset_id IS NULL OR a.id > $after_asset_id)
              AND NOT EXISTS (
                  SELECT 1
                  FROM asset_metadata m
                  WHERE m.asset_id = a.id
                    AND m.pipeline_version = $pipeline_version
                    AND m.source_size = a.size
                    AND m.source_modified_at = a.modified_at
              )
            GROUP BY
                a.id,
                a.original_filename,
                a.extension,
                a.mime_type,
                a.size,
                a.created_at,
                a.modified_at
            ORDER BY a.id
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$pipeline_version", pipelineVersion);
        command.Parameters.AddWithValue(
            "$after_asset_id",
            afterAssetId is null ? DBNull.Value : afterAssetId.Value.ToString("D"));
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(new MetadataCandidate(
                Guid.Parse(reader.GetString(0)),
                new DiscoveredFile(
                    reader.GetString(7),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetInt64(4),
                    ParseTimestamp(reader.GetString(5)),
                    ParseTimestamp(reader.GetString(6)))));
        }

        return candidates;
    }

    public async Task<bool> SaveMetadataAsync(
        AssetMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (metadata.PipelineVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO asset_metadata(
                asset_id, extractor_name, pipeline_version, status,
                source_size, source_modified_at, metadata_json,
                error_message, extracted_at)
            SELECT
                $asset_id, $extractor_name, $pipeline_version, $status,
                $source_size, $source_modified_at, $metadata_json,
                $error_message, $extracted_at
            WHERE EXISTS (
                SELECT 1
                FROM assets
                WHERE id = $asset_id
                  AND size = $source_size
                  AND modified_at = $source_modified_at
            )
            ON CONFLICT(asset_id) DO UPDATE SET
                extractor_name = excluded.extractor_name,
                pipeline_version = excluded.pipeline_version,
                status = excluded.status,
                source_size = excluded.source_size,
                source_modified_at = excluded.source_modified_at,
                metadata_json = excluded.metadata_json,
                error_message = excluded.error_message,
                extracted_at = excluded.extracted_at;
            """;
        AddMetadataParameters(command, metadata);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<AssetMetadata?> GetMetadataAsync(
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                asset_id, extractor_name, pipeline_version, status,
                source_size, source_modified_at, metadata_json,
                error_message, extracted_at
            FROM asset_metadata
            WHERE asset_id = $asset_id;
            """;
        command.Parameters.AddWithValue("$asset_id", assetId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadMetadata(reader, Guid.Parse(reader.GetString(0)), 1)
            : null;
    }

    public async Task<AssetStatistics> GetLocalAssetStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        long fileCount;
        long totalSizeBytes;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using (var summaryCommand = connection.CreateCommand())
        {
            summaryCommand.CommandText =
                """
                SELECT COUNT(*), COALESCE(SUM(a.size), 0)
                FROM asset_locations l
                INNER JOIN assets a ON a.id = l.asset_id
                WHERE l.location_type = 'Local'
                  AND l.status = 'Available';
                """;

            await using var reader = await summaryCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Unable to calculate local asset statistics.");
            }

            fileCount = reader.GetInt64(0);
            totalSizeBytes = reader.GetInt64(1);
        }

        long videoFileCount = 0;
        long videoDurationMilliseconds = 0;
        await using var metadataCommand = connection.CreateCommand();
        metadataCommand.CommandText =
            """
            SELECT
                a.id,
                m.extractor_name,
                m.pipeline_version,
                m.status,
                m.source_size,
                m.source_modified_at,
                m.metadata_json,
                m.error_message,
                m.extracted_at
            FROM asset_locations l
            INNER JOIN assets a ON a.id = l.asset_id
            INNER JOIN asset_metadata m
                ON m.asset_id = a.id
               AND m.pipeline_version = $pipeline_version
               AND m.source_size = a.size
               AND m.source_modified_at = a.modified_at
            WHERE l.location_type = 'Local'
              AND l.status = 'Available'
              AND m.status = $metadata_status;
            """;
        metadataCommand.Parameters.AddWithValue(
            "$pipeline_version",
            MetadataPipeline.CurrentVersion);
        metadataCommand.Parameters.AddWithValue(
            "$metadata_status",
            MetadataExtractionStatus.Extracted.ToString());

        await using var metadataReader =
            await metadataCommand.ExecuteReaderAsync(cancellationToken);
        while (await metadataReader.ReadAsync(cancellationToken))
        {
            var metadata = ReadMetadata(
                metadataReader,
                Guid.Parse(metadataReader.GetString(0)),
                1);
            if (metadata?.Content?.Kind != AssetMediaKind.Video)
            {
                continue;
            }

            videoFileCount++;
            var duration = metadata.Content.DurationMilliseconds;
            if (duration is not > 0)
            {
                continue;
            }

            videoDurationMilliseconds =
                long.MaxValue - videoDurationMilliseconds < duration.Value
                    ? long.MaxValue
                    : videoDurationMilliseconds + duration.Value;
        }

        return new AssetStatistics(
            fileCount,
            totalSizeBytes,
            videoFileCount,
            videoDurationMilliseconds);
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
                a.status,
                m.extractor_name,
                m.pipeline_version,
                m.status,
                m.source_size,
                m.source_modified_at,
                m.metadata_json,
                m.error_message,
                m.extracted_at,
                t.extractor_name,
                t.pipeline_version,
                t.status,
                t.source_size,
                t.source_modified_at,
                t.title,
                t.plain_text,
                t.headings_json,
                t.encoding_name,
                t.is_truncated,
                t.error_message,
                t.extracted_at
            FROM assets a
            INNER JOIN asset_locations l ON l.asset_id = a.id
            LEFT JOIN asset_metadata m
                ON m.asset_id = a.id
               AND m.pipeline_version = $metadata_pipeline_version
               AND m.source_size = a.size
               AND m.source_modified_at = a.modified_at
            LEFT JOIN asset_text t
                ON t.asset_id = a.id
               AND t.pipeline_version = $text_pipeline_version
               AND t.source_size = a.size
               AND t.source_modified_at = a.modified_at
            WHERE l.location_type = 'Local'
            ORDER BY a.discovered_at DESC, a.original_filename
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue(
            "$metadata_pipeline_version",
            MetadataPipeline.CurrentVersion);
        command.Parameters.AddWithValue("$text_pipeline_version", TextPipeline.CurrentVersion);

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
                Enum.Parse<AssetStatus>(reader.GetString(8)),
                ReadMetadata(reader, Guid.Parse(reader.GetString(0)), 9),
                ReadText(reader, Guid.Parse(reader.GetString(0)), 17)));
        }

        return assets;
    }

    public async Task<IReadOnlyList<ExactDuplicateGroup>> ListExactDuplicateGroupsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var groups = new List<ExactDuplicateGroup>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var groupCommand = connection.CreateCommand();
        groupCommand.CommandText =
            """
            SELECT sha256, size
            FROM assets
            WHERE sha256 IS NOT NULL
            GROUP BY sha256, size
            HAVING COUNT(*) > 1
            ORDER BY COUNT(*) DESC, size DESC
            LIMIT $limit;
            """;
        groupCommand.Parameters.AddWithValue("$limit", limit);

        var candidates = new List<(string Sha256, long Size)>();
        await using (var reader = await groupCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                candidates.Add((reader.GetString(0), reader.GetInt64(1)));
            }
        }

        foreach (var candidate in candidates)
        {
            await using var itemCommand = connection.CreateCommand();
            itemCommand.CommandText =
                """
                SELECT
                    a.id,
                    a.original_filename,
                    l.path,
                    a.modified_at,
                    l.status
                FROM assets a
                INNER JOIN asset_locations l ON l.asset_id = a.id
                WHERE a.sha256 = $sha256
                  AND a.size = $size
                  AND l.location_type = 'Local'
                ORDER BY l.path;
                """;
            itemCommand.Parameters.AddWithValue("$sha256", candidate.Sha256);
            itemCommand.Parameters.AddWithValue("$size", candidate.Size);

            var items = new List<DuplicateAssetItem>();
            await using var itemReader = await itemCommand.ExecuteReaderAsync(cancellationToken);
            while (await itemReader.ReadAsync(cancellationToken))
            {
                items.Add(new DuplicateAssetItem(
                    Guid.Parse(itemReader.GetString(0)),
                    itemReader.GetString(1),
                    itemReader.GetString(2),
                    ParseTimestamp(itemReader.GetString(3)),
                    Enum.Parse<AssetLocationStatus>(itemReader.GetString(4))));
            }

            if (items.Count > 1)
            {
                groups.Add(new ExactDuplicateGroup(candidate.Sha256, candidate.Size, items));
            }
        }

        return groups;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<ExistingAsset?> FindAssetAsync(
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
            SELECT a.id, a.size, a.modified_at, a.sha256
            FROM asset_locations l
            INNER JOIN assets a ON a.id = l.asset_id
            WHERE l.device_id = $device_id AND l.path_key = $path_key
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$device_id", deviceId);
        command.Parameters.AddWithValue("$path_key", pathKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ExistingAsset(
            Guid.Parse(reader.GetString(0)),
            reader.GetInt64(1),
            ParseTimestamp(reader.GetString(2)),
            reader.IsDBNull(3) ? null : reader.GetString(3));
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

    private static void AddMetadataParameters(
        SqliteCommand command,
        AssetMetadata metadata)
    {
        command.Parameters.AddWithValue("$asset_id", metadata.AssetId.ToString("D"));
        command.Parameters.AddWithValue("$extractor_name", metadata.ExtractorName);
        command.Parameters.AddWithValue("$pipeline_version", metadata.PipelineVersion);
        command.Parameters.AddWithValue("$status", metadata.Status.ToString());
        command.Parameters.AddWithValue("$source_size", metadata.SourceSize);
        command.Parameters.AddWithValue(
            "$source_modified_at",
            metadata.SourceModifiedAt.ToString("O"));
        command.Parameters.AddWithValue(
            "$metadata_json",
            metadata.Content is null
                ? DBNull.Value
                : JsonSerializer.Serialize(metadata.Content));
        command.Parameters.AddWithValue(
            "$error_message",
            (object?)metadata.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$extracted_at", metadata.ExtractedAt.ToString("O"));
    }

    private static AssetMetadata? ReadMetadata(
        SqliteDataReader reader,
        Guid assetId,
        int offset)
    {
        if (reader.IsDBNull(offset))
        {
            return null;
        }

        var content = reader.IsDBNull(offset + 5)
            ? null
            : JsonSerializer.Deserialize<AssetMetadataContent>(reader.GetString(offset + 5));

        return new AssetMetadata(
            assetId,
            reader.GetString(offset),
            reader.GetInt32(offset + 1),
            Enum.Parse<MetadataExtractionStatus>(reader.GetString(offset + 2)),
            reader.GetInt64(offset + 3),
            ParseTimestamp(reader.GetString(offset + 4)),
            content,
            ParseTimestamp(reader.GetString(offset + 7)),
            reader.IsDBNull(offset + 6) ? null : reader.GetString(offset + 6));
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

    private static string GetFingerprintModeFilter(FingerprintMode mode)
    {
        return mode switch
        {
            FingerprintMode.Complete => string.Empty,
            FingerprintMode.DuplicateCandidates =>
                """
                AND a.size IN (
                    SELECT a2.size
                    FROM assets a2
                    WHERE EXISTS (
                        SELECT 1
                        FROM asset_locations l2
                        WHERE l2.asset_id = a2.id
                          AND l2.location_type = 'Local'
                          AND l2.status = 'Available'
                    )
                    GROUP BY a2.size
                    HAVING COUNT(*) > 1
                )
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        return DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
    }

    private sealed record ExistingAsset(
        Guid Id,
        long Size,
        DateTimeOffset ModifiedAt,
        string? Sha256);
}
