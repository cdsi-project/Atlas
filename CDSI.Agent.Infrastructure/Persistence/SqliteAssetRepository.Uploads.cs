using System.Text.Json;
using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.Storage;
using Microsoft.Data.Sqlite;

namespace CDSI.Agent.Infrastructure.Persistence;

public sealed partial class SqliteAssetRepository : IObjectStorageUploadRepository
{
    public async Task CreateUploadJobAsync(
        ObjectStorageUploadJob job,
        IReadOnlyCollection<ObjectStorageUploadItem> items,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText =
                """
                INSERT INTO upload_jobs(
                    id, storage_profile_id, status, started_at, finished_at,
                    total_items, completed_items, failed_items,
                    total_bytes, uploaded_bytes, error_message)
                VALUES(
                    $id, $storage_profile_id, $status, $started_at, $finished_at,
                    $total_items, $completed_items, $failed_items,
                    $total_bytes, $uploaded_bytes, $error_message);
                """;
            AddUploadJobParameters(command, job);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var item in items)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText =
                """
                INSERT INTO upload_items(
                    id, job_id, asset_id, source_path, object_key,
                    status, size, uploaded_bytes, etag, error_message, finished_at)
                VALUES(
                    $id, $job_id, $asset_id, $source_path, $object_key,
                    $status, $size, $uploaded_bytes, $etag, $error_message, $finished_at);
                """;
            AddUploadItemParameters(command, item);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveUploadItemAsync(
        ObjectStorageUploadItem item,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE upload_items
            SET status = $status,
                uploaded_bytes = $uploaded_bytes,
                etag = $etag,
                error_message = $error_message,
                finished_at = $finished_at
            WHERE id = $id AND job_id = $job_id;
            """;
        AddUploadItemParameters(command, item);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateUploadJobAsync(
        ObjectStorageUploadJob job,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE upload_jobs
            SET status = $status,
                finished_at = $finished_at,
                total_items = $total_items,
                completed_items = $completed_items,
                failed_items = $failed_items,
                total_bytes = $total_bytes,
                uploaded_bytes = $uploaded_bytes,
                error_message = $error_message
            WHERE id = $id;
            """;
        AddUploadJobParameters(command, job);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ObjectStorageUploadAudit?> GetUploadJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        ObjectStorageUploadJob? job;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT
                    id, storage_profile_id, status, started_at, finished_at,
                    total_items, completed_items, failed_items,
                    total_bytes, uploaded_bytes, error_message
                FROM upload_jobs
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$id", jobId.ToString("D"));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            job = await reader.ReadAsync(cancellationToken)
                ? ReadUploadJob(reader)
                : null;
        }

        if (job is null)
        {
            return null;
        }

        var items = new List<ObjectStorageUploadItem>();
        await using var itemCommand = connection.CreateCommand();
        itemCommand.CommandText =
            """
            SELECT
                id, job_id, asset_id, source_path, object_key,
                status, size, uploaded_bytes, etag, error_message, finished_at
            FROM upload_items
            WHERE job_id = $job_id
            ORDER BY rowid;
            """;
        itemCommand.Parameters.AddWithValue("$job_id", jobId.ToString("D"));
        await using var itemReader =
            await itemCommand.ExecuteReaderAsync(cancellationToken);
        while (await itemReader.ReadAsync(cancellationToken))
        {
            items.Add(ReadUploadItem(itemReader));
        }

        return new ObjectStorageUploadAudit(job, items);
    }

    public async Task<MultipartUploadSession?> GetMultipartUploadSessionAsync(
        Guid storageProfileId,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                storage_profile_id, asset_id, object_key, source_path,
                upload_id, part_size, source_size, source_modified_at,
                parts_json, updated_at
            FROM multipart_upload_sessions
            WHERE storage_profile_id = $storage_profile_id
              AND object_key = $object_key;
            """;
        command.Parameters.AddWithValue(
            "$storage_profile_id",
            storageProfileId.ToString("D"));
        command.Parameters.AddWithValue("$object_key", objectKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var parts = JsonSerializer.Deserialize<List<MultipartUploadPart>>(
            reader.GetString(8)) ?? [];
        return new MultipartUploadSession(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt64(5),
            reader.GetInt64(6),
            ParseTimestamp(reader.GetString(7)),
            parts,
            ParseTimestamp(reader.GetString(9)));
    }

    public async Task SaveMultipartUploadSessionAsync(
        MultipartUploadSession session,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO multipart_upload_sessions(
                storage_profile_id, object_key, asset_id, source_path,
                upload_id, part_size, source_size, source_modified_at,
                parts_json, updated_at)
            VALUES(
                $storage_profile_id, $object_key, $asset_id, $source_path,
                $upload_id, $part_size, $source_size, $source_modified_at,
                $parts_json, $updated_at)
            ON CONFLICT(storage_profile_id, object_key) DO UPDATE SET
                asset_id = excluded.asset_id,
                source_path = excluded.source_path,
                upload_id = excluded.upload_id,
                part_size = excluded.part_size,
                source_size = excluded.source_size,
                source_modified_at = excluded.source_modified_at,
                parts_json = excluded.parts_json,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue(
            "$storage_profile_id",
            session.StorageProfileId.ToString("D"));
        command.Parameters.AddWithValue("$object_key", session.ObjectKey);
        command.Parameters.AddWithValue("$asset_id", session.AssetId.ToString("D"));
        command.Parameters.AddWithValue("$source_path", session.SourcePath);
        command.Parameters.AddWithValue("$upload_id", session.UploadId);
        command.Parameters.AddWithValue("$part_size", session.PartSize);
        command.Parameters.AddWithValue("$source_size", session.SourceSize);
        command.Parameters.AddWithValue(
            "$source_modified_at",
            session.SourceModifiedAt.ToString("O"));
        command.Parameters.AddWithValue(
            "$parts_json",
            JsonSerializer.Serialize(session.Parts));
        command.Parameters.AddWithValue("$updated_at", session.UpdatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteMultipartUploadSessionAsync(
        Guid storageProfileId,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM multipart_upload_sessions
            WHERE storage_profile_id = $storage_profile_id
              AND object_key = $object_key;
            """;
        command.Parameters.AddWithValue(
            "$storage_profile_id",
            storageProfileId.ToString("D"));
        command.Parameters.AddWithValue("$object_key", objectKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveObjectStorageLocationAsync(
        ObjectStorageLocation location,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO object_storage_locations(
                id, asset_id, storage_profile_id, object_key, status,
                size, sha256, etag, created_at, updated_at, last_verified_at)
            VALUES(
                $id, $asset_id, $storage_profile_id, $object_key, $status,
                $size, $sha256, $etag, $created_at, $updated_at, $last_verified_at)
            ON CONFLICT(storage_profile_id, object_key) DO UPDATE SET
                asset_id = excluded.asset_id,
                status = excluded.status,
                size = excluded.size,
                sha256 = excluded.sha256,
                etag = excluded.etag,
                updated_at = excluded.updated_at,
                last_verified_at = excluded.last_verified_at;
            """;
        command.Parameters.AddWithValue("$id", location.Id.ToString("D"));
        command.Parameters.AddWithValue("$asset_id", location.AssetId.ToString("D"));
        command.Parameters.AddWithValue(
            "$storage_profile_id",
            location.StorageProfileId.ToString("D"));
        command.Parameters.AddWithValue("$object_key", location.ObjectKey);
        command.Parameters.AddWithValue("$status", location.Status.ToString());
        command.Parameters.AddWithValue("$size", location.Size);
        command.Parameters.AddWithValue(
            "$sha256",
            (object?)location.Sha256 ?? DBNull.Value);
        command.Parameters.AddWithValue("$etag", (object?)location.ETag ?? DBNull.Value);
        command.Parameters.AddWithValue("$created_at", location.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated_at", location.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue(
            "$last_verified_at",
            location.LastVerifiedAt is null
                ? DBNull.Value
                : location.LastVerifiedAt.Value.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ObjectStorageLocation?> GetObjectStorageLocationAsync(
        Guid assetId,
        Guid storageProfileId,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id, asset_id, storage_profile_id, object_key, status,
                size, sha256, etag, created_at, updated_at, last_verified_at
            FROM object_storage_locations
            WHERE asset_id = $asset_id
              AND storage_profile_id = $storage_profile_id
              AND object_key = $object_key;
            """;
        command.Parameters.AddWithValue("$asset_id", assetId.ToString("D"));
        command.Parameters.AddWithValue(
            "$storage_profile_id",
            storageProfileId.ToString("D"));
        command.Parameters.AddWithValue("$object_key", objectKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadObjectStorageLocation(reader)
            : null;
    }

    private static void AddUploadJobParameters(
        SqliteCommand command,
        ObjectStorageUploadJob job)
    {
        command.Parameters.AddWithValue("$id", job.Id.ToString("D"));
        command.Parameters.AddWithValue(
            "$storage_profile_id",
            job.StorageProfileId.ToString("D"));
        command.Parameters.AddWithValue("$status", job.Status.ToString());
        command.Parameters.AddWithValue("$started_at", job.StartedAt.ToString("O"));
        command.Parameters.AddWithValue(
            "$finished_at",
            job.FinishedAt is null
                ? DBNull.Value
                : job.FinishedAt.Value.ToString("O"));
        command.Parameters.AddWithValue("$total_items", job.TotalItems);
        command.Parameters.AddWithValue("$completed_items", job.CompletedItems);
        command.Parameters.AddWithValue("$failed_items", job.FailedItems);
        command.Parameters.AddWithValue("$total_bytes", job.TotalBytes);
        command.Parameters.AddWithValue("$uploaded_bytes", job.UploadedBytes);
        command.Parameters.AddWithValue(
            "$error_message",
            (object?)job.ErrorMessage ?? DBNull.Value);
    }

    private static void AddUploadItemParameters(
        SqliteCommand command,
        ObjectStorageUploadItem item)
    {
        command.Parameters.AddWithValue("$id", item.Id.ToString("D"));
        command.Parameters.AddWithValue("$job_id", item.JobId.ToString("D"));
        command.Parameters.AddWithValue("$asset_id", item.AssetId.ToString("D"));
        command.Parameters.AddWithValue("$source_path", item.SourcePath);
        command.Parameters.AddWithValue("$object_key", item.ObjectKey);
        command.Parameters.AddWithValue("$status", item.Status.ToString());
        command.Parameters.AddWithValue("$size", item.Size);
        command.Parameters.AddWithValue("$uploaded_bytes", item.UploadedBytes);
        command.Parameters.AddWithValue("$etag", (object?)item.ETag ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$error_message",
            (object?)item.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$finished_at",
            item.FinishedAt is null
                ? DBNull.Value
                : item.FinishedAt.Value.ToString("O"));
    }

    private static ObjectStorageUploadJob ReadUploadJob(SqliteDataReader reader)
    {
        return new ObjectStorageUploadJob(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            Enum.Parse<UploadJobStatus>(reader.GetString(2)),
            ParseTimestamp(reader.GetString(3)),
            reader.IsDBNull(4) ? null : ParseTimestamp(reader.GetString(4)),
            reader.GetInt32(5),
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetInt64(8),
            reader.GetInt64(9),
            reader.IsDBNull(10) ? null : reader.GetString(10));
    }

    private static ObjectStorageUploadItem ReadUploadItem(SqliteDataReader reader)
    {
        return new ObjectStorageUploadItem(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            Guid.Parse(reader.GetString(2)),
            reader.GetString(3),
            reader.GetString(4),
            Enum.Parse<UploadItemStatus>(reader.GetString(5)),
            reader.GetInt64(6),
            reader.GetInt64(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : ParseTimestamp(reader.GetString(10)));
    }

    private static ObjectStorageLocation ReadObjectStorageLocation(
        SqliteDataReader reader)
    {
        return new ObjectStorageLocation(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            Guid.Parse(reader.GetString(2)),
            reader.GetString(3),
            Enum.Parse<StorageVerificationStatus>(reader.GetString(4)),
            reader.GetInt64(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            ParseTimestamp(reader.GetString(8)),
            ParseTimestamp(reader.GetString(9)),
            reader.IsDBNull(10) ? null : ParseTimestamp(reader.GetString(10)));
    }
}
