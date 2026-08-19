using Microsoft.Data.Sqlite;

namespace CDSI.Agent.Infrastructure.Persistence;

internal static class DatabaseMigrator
{
    public static async Task MigrateAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER NOT NULL PRIMARY KEY,
                applied_at TEXT NOT NULL
            );
            """,
            cancellationToken);

        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
        var currentVersion = Convert.ToInt32(
            await versionCommand.ExecuteScalarAsync(cancellationToken));

        if (currentVersion < 1)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var migrationCommand = connection.CreateCommand();
            migrationCommand.Transaction = (SqliteTransaction)transaction;
            migrationCommand.CommandText =
                """
            CREATE TABLE devices (
                id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                platform TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE scan_roots (
                id TEXT NOT NULL PRIMARY KEY,
                path TEXT NOT NULL,
                path_key TEXT NOT NULL UNIQUE,
                enabled INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                last_scanned_at TEXT NULL
            );

            CREATE TABLE scan_jobs (
                id TEXT NOT NULL PRIMARY KEY,
                scan_root_id TEXT NOT NULL,
                status TEXT NOT NULL,
                started_at TEXT NOT NULL,
                finished_at TEXT NULL,
                files_discovered INTEGER NOT NULL,
                files_processed INTEGER NOT NULL,
                errors INTEGER NOT NULL,
                error_message TEXT NULL,
                FOREIGN KEY (scan_root_id) REFERENCES scan_roots(id)
            );

            CREATE TABLE assets (
                id TEXT NOT NULL PRIMARY KEY,
                original_filename TEXT NOT NULL,
                mime_type TEXT NULL,
                extension TEXT NOT NULL,
                size INTEGER NOT NULL,
                sha256 TEXT NULL,
                created_at TEXT NOT NULL,
                modified_at TEXT NOT NULL,
                discovered_at TEXT NOT NULL,
                status TEXT NOT NULL
            );

            CREATE TABLE asset_locations (
                id TEXT NOT NULL PRIMARY KEY,
                asset_id TEXT NOT NULL,
                location_type TEXT NOT NULL,
                device_id TEXT NOT NULL,
                path TEXT NOT NULL,
                path_key TEXT NOT NULL,
                status TEXT NOT NULL,
                last_seen_at TEXT NOT NULL,
                last_verified_at TEXT NULL,
                FOREIGN KEY (asset_id) REFERENCES assets(id),
                FOREIGN KEY (device_id) REFERENCES devices(id),
                UNIQUE (device_id, path_key)
            );

            CREATE INDEX ix_assets_sha256 ON assets(sha256);
            CREATE INDEX ix_assets_discovered_at ON assets(discovered_at DESC);
            CREATE INDEX ix_asset_locations_asset_id ON asset_locations(asset_id);
            CREATE INDEX ix_scan_jobs_scan_root_id ON scan_jobs(scan_root_id);

            INSERT INTO schema_migrations(version, applied_at)
            VALUES (1, $applied_at);
            """;
            migrationCommand.Parameters.AddWithValue(
                "$applied_at",
                DateTimeOffset.UtcNow.ToString("O"));
            await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (currentVersion < 2)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var migrationCommand = connection.CreateCommand();
            migrationCommand.Transaction = (SqliteTransaction)transaction;
            migrationCommand.CommandText =
                """
                CREATE TABLE asset_metadata (
                    asset_id TEXT NOT NULL PRIMARY KEY,
                    extractor_name TEXT NOT NULL,
                    pipeline_version INTEGER NOT NULL,
                    status TEXT NOT NULL,
                    source_size INTEGER NOT NULL,
                    source_modified_at TEXT NOT NULL,
                    metadata_json TEXT NULL,
                    error_message TEXT NULL,
                    extracted_at TEXT NOT NULL,
                    FOREIGN KEY (asset_id) REFERENCES assets(id) ON DELETE CASCADE
                );

                CREATE INDEX ix_asset_metadata_status
                ON asset_metadata(status);

                INSERT INTO schema_migrations(version, applied_at)
                VALUES (2, $applied_at);
                """;
            migrationCommand.Parameters.AddWithValue(
                "$applied_at",
                DateTimeOffset.UtcNow.ToString("O"));
            await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (currentVersion < 3)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var migrationCommand = connection.CreateCommand();
            migrationCommand.Transaction = (SqliteTransaction)transaction;
            migrationCommand.CommandText =
                """
                CREATE TABLE asset_text (
                    asset_id TEXT NOT NULL PRIMARY KEY,
                    extractor_name TEXT NOT NULL,
                    pipeline_version INTEGER NOT NULL,
                    status TEXT NOT NULL,
                    source_size INTEGER NOT NULL,
                    source_modified_at TEXT NOT NULL,
                    title TEXT NULL,
                    plain_text TEXT NULL,
                    headings_json TEXT NULL,
                    encoding_name TEXT NULL,
                    is_truncated INTEGER NULL,
                    error_message TEXT NULL,
                    extracted_at TEXT NOT NULL,
                    FOREIGN KEY (asset_id) REFERENCES assets(id) ON DELETE CASCADE
                );

                CREATE INDEX ix_asset_text_status
                ON asset_text(status);

                INSERT INTO schema_migrations(version, applied_at)
                VALUES (3, $applied_at);
                """;
            migrationCommand.Parameters.AddWithValue(
                "$applied_at",
                DateTimeOffset.UtcNow.ToString("O"));
            await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (currentVersion < 4)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var migrationCommand = connection.CreateCommand();
            migrationCommand.Transaction = (SqliteTransaction)transaction;
            migrationCommand.CommandText =
                """
                ALTER TABLE scan_roots
                    ADD COLUMN mode TEXT NOT NULL DEFAULT 'Readonly';
                ALTER TABLE scan_roots
                    ADD COLUMN status TEXT NOT NULL DEFAULT 'Active';
                ALTER TABLE scan_roots
                    ADD COLUMN updated_at TEXT NULL;
                ALTER TABLE scan_roots
                    ADD COLUMN removed_at TEXT NULL;

                UPDATE scan_roots
                SET updated_at = created_at
                WHERE updated_at IS NULL;

                CREATE TABLE managed_workspaces (
                    id TEXT NOT NULL PRIMARY KEY,
                    device_id TEXT NOT NULL UNIQUE,
                    path TEXT NOT NULL,
                    path_key TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    FOREIGN KEY (device_id) REFERENCES devices(id)
                );

                INSERT INTO schema_migrations(version, applied_at)
                VALUES (4, $applied_at);
                """;
            migrationCommand.Parameters.AddWithValue(
                "$applied_at",
                DateTimeOffset.UtcNow.ToString("O"));
            await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (currentVersion < 5)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var migrationCommand = connection.CreateCommand();
            migrationCommand.Transaction = (SqliteTransaction)transaction;
            migrationCommand.CommandText =
                """
                CREATE TABLE storage_profiles (
                    id TEXT NOT NULL PRIMARY KEY,
                    display_name TEXT NOT NULL,
                    provider TEXT NOT NULL,
                    endpoint TEXT NOT NULL,
                    bucket_name TEXT NOT NULL,
                    region TEXT NULL,
                    use_https INTEGER NOT NULL,
                    access_key_id TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );

                INSERT INTO schema_migrations(version, applied_at)
                VALUES (5, $applied_at);
                """;
            migrationCommand.Parameters.AddWithValue(
                "$applied_at",
                DateTimeOffset.UtcNow.ToString("O"));
            await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (currentVersion < 6)
        {
            var assetLocationsExist = await TableExistsAsync(
                connection,
                "asset_locations",
                cancellationToken);
            var ownershipColumnExists =
                await AssetLocationOwnershipColumnExistsAsync(
                    connection,
                    cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            if (assetLocationsExist && !ownershipColumnExists)
            {
                await using var locationMigrationCommand = connection.CreateCommand();
                locationMigrationCommand.Transaction = (SqliteTransaction)transaction;
                locationMigrationCommand.CommandText =
                    """
                    ALTER TABLE asset_locations
                        ADD COLUMN ownership TEXT NOT NULL DEFAULT 'External';
                    """;
                await locationMigrationCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var migrationCommand = connection.CreateCommand();
            migrationCommand.Transaction = (SqliteTransaction)transaction;
            migrationCommand.CommandText =
                """
                CREATE TABLE file_operations (
                    id TEXT NOT NULL PRIMARY KEY,
                    action TEXT NOT NULL,
                    status TEXT NOT NULL,
                    started_at TEXT NOT NULL,
                    finished_at TEXT NULL,
                    total_items INTEGER NOT NULL,
                    completed_items INTEGER NOT NULL,
                    failed_items INTEGER NOT NULL,
                    error_message TEXT NULL
                );

                CREATE TABLE file_operation_items (
                    id TEXT NOT NULL PRIMARY KEY,
                    operation_id TEXT NOT NULL,
                    asset_id TEXT NOT NULL,
                    source_path TEXT NOT NULL,
                    target_path TEXT NULL,
                    status TEXT NOT NULL,
                    source_deleted INTEGER NOT NULL,
                    sha256 TEXT NULL,
                    error_message TEXT NULL,
                    finished_at TEXT NULL,
                    FOREIGN KEY (operation_id) REFERENCES file_operations(id) ON DELETE CASCADE,
                    FOREIGN KEY (asset_id) REFERENCES assets(id)
                );

                CREATE INDEX ix_file_operation_items_operation_id
                ON file_operation_items(operation_id);

                INSERT INTO schema_migrations(version, applied_at)
                VALUES (6, $applied_at);
                """;
            migrationCommand.Parameters.AddWithValue(
                "$applied_at",
                DateTimeOffset.UtcNow.ToString("O"));
            await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (currentVersion < 7)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var migrationCommand = connection.CreateCommand();
            migrationCommand.Transaction = (SqliteTransaction)transaction;
            migrationCommand.CommandText =
                """
                CREATE TABLE object_storage_locations (
                    id TEXT NOT NULL PRIMARY KEY,
                    asset_id TEXT NOT NULL,
                    storage_profile_id TEXT NOT NULL,
                    object_key TEXT NOT NULL,
                    status TEXT NOT NULL,
                    size INTEGER NOT NULL,
                    sha256 TEXT NULL,
                    etag TEXT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    last_verified_at TEXT NULL,
                    FOREIGN KEY (asset_id) REFERENCES assets(id),
                    UNIQUE (storage_profile_id, object_key)
                );

                CREATE TABLE upload_jobs (
                    id TEXT NOT NULL PRIMARY KEY,
                    storage_profile_id TEXT NOT NULL,
                    status TEXT NOT NULL,
                    started_at TEXT NOT NULL,
                    finished_at TEXT NULL,
                    total_items INTEGER NOT NULL,
                    completed_items INTEGER NOT NULL,
                    failed_items INTEGER NOT NULL,
                    total_bytes INTEGER NOT NULL,
                    uploaded_bytes INTEGER NOT NULL,
                    error_message TEXT NULL
                );

                CREATE TABLE upload_items (
                    id TEXT NOT NULL PRIMARY KEY,
                    job_id TEXT NOT NULL,
                    asset_id TEXT NOT NULL,
                    source_path TEXT NOT NULL,
                    object_key TEXT NOT NULL,
                    status TEXT NOT NULL,
                    size INTEGER NOT NULL,
                    uploaded_bytes INTEGER NOT NULL,
                    etag TEXT NULL,
                    error_message TEXT NULL,
                    finished_at TEXT NULL,
                    FOREIGN KEY (job_id) REFERENCES upload_jobs(id) ON DELETE CASCADE,
                    FOREIGN KEY (asset_id) REFERENCES assets(id)
                );

                CREATE TABLE multipart_upload_sessions (
                    storage_profile_id TEXT NOT NULL,
                    object_key TEXT NOT NULL,
                    asset_id TEXT NOT NULL,
                    source_path TEXT NOT NULL,
                    upload_id TEXT NOT NULL,
                    part_size INTEGER NOT NULL,
                    source_size INTEGER NOT NULL,
                    source_modified_at TEXT NOT NULL,
                    parts_json TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    PRIMARY KEY (storage_profile_id, object_key),
                    FOREIGN KEY (asset_id) REFERENCES assets(id)
                );

                CREATE INDEX ix_object_storage_locations_asset_id
                ON object_storage_locations(asset_id);
                CREATE INDEX ix_upload_items_job_id
                ON upload_items(job_id);
                CREATE INDEX ix_multipart_upload_sessions_asset_id
                ON multipart_upload_sessions(asset_id);

                INSERT INTO schema_migrations(version, applied_at)
                VALUES (7, $applied_at);
                """;
            migrationCommand.Parameters.AddWithValue(
                "$applied_at",
                DateTimeOffset.UtcNow.ToString("O"));
            await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (currentVersion < 8)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var migrationCommand = connection.CreateCommand();
            migrationCommand.Transaction = (SqliteTransaction)transaction;
            migrationCommand.CommandText =
                """
                CREATE TABLE asset_collections (
                    id TEXT NOT NULL PRIMARY KEY,
                    name TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    type TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );

                CREATE TABLE asset_collection_items (
                    collection_id TEXT NOT NULL,
                    asset_id TEXT NOT NULL,
                    added_at TEXT NOT NULL,
                    PRIMARY KEY (collection_id, asset_id),
                    FOREIGN KEY (collection_id)
                        REFERENCES asset_collections(id) ON DELETE CASCADE,
                    FOREIGN KEY (asset_id)
                        REFERENCES assets(id) ON DELETE CASCADE
                );

                CREATE INDEX ix_asset_collection_items_asset_id
                ON asset_collection_items(asset_id);

                INSERT INTO schema_migrations(version, applied_at)
                VALUES (8, $applied_at);
                """;
            migrationCommand.Parameters.AddWithValue(
                "$applied_at",
                DateTimeOffset.UtcNow.ToString("O"));
            await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT EXISTS(
                SELECT 1
                FROM sqlite_master
                WHERE type = 'table' AND name = $table_name);
            """;
        command.Parameters.AddWithValue("$table_name", tableName);
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken)) != 0;
    }

    private static async Task<bool> AssetLocationOwnershipColumnExistsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT EXISTS(
                SELECT 1
                FROM pragma_table_info('asset_locations')
                WHERE name = 'ownership');
            """;
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken)) != 0;
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
