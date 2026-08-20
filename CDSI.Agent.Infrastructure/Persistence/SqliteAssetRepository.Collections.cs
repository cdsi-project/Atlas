using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Collections;
using CDSI.Agent.Core.Metadata;

namespace CDSI.Agent.Infrastructure.Persistence;

public sealed partial class SqliteAssetRepository : IAssetCollectionRepository
{
    public async Task<bool> CreateAssetCollectionAsync(
        AssetCollection collection,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT OR IGNORE INTO asset_collections (
                id, name, type, created_at, updated_at)
            VALUES (
                $id, $name, $type, $created_at, $updated_at);
            """;
        command.Parameters.AddWithValue("$id", collection.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", collection.Name);
        command.Parameters.AddWithValue("$type", collection.Type.ToString());
        command.Parameters.AddWithValue("$created_at", collection.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated_at", collection.UpdatedAt.ToString("O"));
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<AssetCollection?> GetAssetCollectionAsync(
        Guid collectionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, name, type, created_at, updated_at
            FROM asset_collections
            WHERE id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", collectionId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadAssetCollection(reader)
            : null;
    }

    public async Task<IReadOnlyList<AssetCollectionSummary>> ListAssetCollectionsAsync(
        CancellationToken cancellationToken = default)
    {
        var collections = new List<AssetCollectionSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                c.id,
                c.name,
                c.type,
                COUNT(ci.asset_id),
                COALESCE(SUM(a.size), 0),
                COALESCE(SUM(CASE WHEN EXISTS (
                    SELECT 1
                    FROM object_storage_locations osl
                    WHERE osl.asset_id = a.id
                      AND osl.status = 'Healthy'
                ) THEN 1 ELSE 0 END), 0),
                c.updated_at
            FROM asset_collections c
            LEFT JOIN asset_collection_items ci ON ci.collection_id = c.id
            LEFT JOIN assets a ON a.id = ci.asset_id
            GROUP BY c.id, c.name, c.type, c.updated_at
            ORDER BY c.updated_at DESC, c.name;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            collections.Add(new AssetCollectionSummary(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                Enum.Parse<AssetCollectionType>(reader.GetString(2)),
                reader.GetInt32(3),
                reader.GetInt64(4),
                reader.GetInt32(5),
                ParseTimestamp(reader.GetString(6))));
        }

        return collections;
    }

    public async Task<int> AddAssetsToCollectionAsync(
        Guid collectionId,
        IReadOnlyCollection<Guid> assetIds,
        DateTimeOffset addedAt,
        CancellationToken cancellationToken = default)
    {
        if (assetIds.Count == 0)
        {
            return 0;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction =
            (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync(
                cancellationToken);
        var added = 0;
        foreach (var assetId in assetIds.Distinct())
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT OR IGNORE INTO asset_collection_items (
                    collection_id, asset_id, added_at)
                VALUES ($collection_id, $asset_id, $added_at);
                """;
            command.Parameters.AddWithValue("$collection_id", collectionId.ToString("D"));
            command.Parameters.AddWithValue("$asset_id", assetId.ToString("D"));
            command.Parameters.AddWithValue("$added_at", addedAt.ToString("O"));
            added += await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (added > 0)
        {
            await UpdateAssetCollectionTimestampAsync(
                connection,
                transaction,
                collectionId,
                addedAt,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return added;
    }

    public async Task<int> RemoveAssetsFromCollectionAsync(
        Guid collectionId,
        IReadOnlyCollection<Guid> assetIds,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default)
    {
        if (assetIds.Count == 0)
        {
            return 0;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction =
            (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync(
                cancellationToken);
        var removed = 0;
        foreach (var assetId in assetIds.Distinct())
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                DELETE FROM asset_collection_items
                WHERE collection_id = $collection_id
                  AND asset_id = $asset_id;
                """;
            command.Parameters.AddWithValue("$collection_id", collectionId.ToString("D"));
            command.Parameters.AddWithValue("$asset_id", assetId.ToString("D"));
            removed += await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (removed > 0)
        {
            await UpdateAssetCollectionTimestampAsync(
                connection,
                transaction,
                collectionId,
                updatedAt,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return removed;
    }

    public async Task<IReadOnlyList<AssetCollectionMember>> ListAssetCollectionMembersAsync(
        Guid collectionId,
        CancellationToken cancellationToken = default)
    {
        var members = new List<AssetCollectionMember>();
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
                a.sha256,
                a.modified_at,
                a.discovered_at,
                l.path,
                l.ownership,
                l.status,
                a.status,
                EXISTS (
                    SELECT 1
                    FROM object_storage_locations osl
                    WHERE osl.asset_id = a.id
                      AND osl.status = 'Healthy'
                ) AS has_healthy_backup,
                m.extractor_name,
                m.pipeline_version,
                m.status,
                m.source_size,
                m.source_modified_at,
                m.metadata_json,
                m.error_message,
                m.extracted_at,
                COALESCE((
                    SELECT json_group_array(tag_name)
                    FROM (
                        SELECT t.name AS tag_name
                        FROM asset_tag_links atl
                        INNER JOIN asset_tags t ON t.id = atl.tag_id
                        WHERE atl.asset_id = a.id
                        ORDER BY t.name COLLATE NOCASE, t.id
                    )
                ), '[]') AS tags_json,
                ci.added_at
            FROM asset_collection_items ci
            INNER JOIN assets a ON a.id = ci.asset_id
            INNER JOIN asset_locations l ON l.id = (
                SELECT l2.id
                FROM asset_locations l2
                WHERE l2.asset_id = a.id
                  AND l2.location_type = 'Local'
                ORDER BY
                    CASE l2.status
                        WHEN 'Available' THEN 0
                        WHEN 'Unverified' THEN 1
                        ELSE 2
                    END,
                    l2.last_seen_at DESC
                LIMIT 1
            )
            LEFT JOIN asset_metadata m
                ON m.asset_id = a.id
               AND m.pipeline_version = $metadata_pipeline_version
               AND m.source_size = a.size
               AND m.source_modified_at = a.modified_at
            WHERE ci.collection_id = $collection_id
            ORDER BY ci.added_at, a.original_filename;
            """;
        command.Parameters.AddWithValue("$collection_id", collectionId.ToString("D"));
        command.Parameters.AddWithValue(
            "$metadata_pipeline_version",
            MetadataPipeline.CurrentVersion);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var assetId = Guid.Parse(reader.GetString(0));
            var asset = new AssetListItem(
                assetId,
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt64(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                ParseTimestamp(reader.GetString(6)),
                ParseTimestamp(reader.GetString(7)),
                reader.GetString(8),
                Enum.Parse<AssetLocationOwnership>(reader.GetString(9)),
                Enum.Parse<AssetLocationStatus>(reader.GetString(10)),
                Enum.Parse<AssetStatus>(reader.GetString(11)),
                reader.GetInt64(12) != 0,
                ReadMetadata(reader, assetId, 13))
            {
                Tags = ReadAssetTags(reader, 21)
            };
            members.Add(new AssetCollectionMember(
                collectionId,
                asset,
                ParseTimestamp(reader.GetString(22))));
        }

        return members;
    }

    private static AssetCollection ReadAssetCollection(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new AssetCollection(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            Enum.Parse<AssetCollectionType>(reader.GetString(2)),
            ParseTimestamp(reader.GetString(3)),
            ParseTimestamp(reader.GetString(4)));
    }

    private static async Task UpdateAssetCollectionTimestampAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        Guid collectionId,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE asset_collections
            SET updated_at = $updated_at
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$updated_at", updatedAt.ToString("O"));
        command.Parameters.AddWithValue("$id", collectionId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
