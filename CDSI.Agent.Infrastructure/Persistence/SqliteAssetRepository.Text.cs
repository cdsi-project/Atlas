using System.Globalization;
using System.Text.Json;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Core.Text;
using Microsoft.Data.Sqlite;

namespace CDSI.Agent.Infrastructure.Persistence;

public sealed partial class SqliteAssetRepository
{
    public async Task<TextWorkSummary> GetTextWorkSummaryAsync(
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
                  FROM asset_text t
                  WHERE t.asset_id = a.id
                    AND t.pipeline_version = $pipeline_version
                    AND t.source_size = a.size
                    AND t.source_modified_at = a.modified_at
              );
            """;
        command.Parameters.AddWithValue("$pipeline_version", pipelineVersion);

        var count = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        return new TextWorkSummary(count);
    }

    public async Task<IReadOnlyList<TextCandidate>> ListTextCandidatesAsync(
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

        var candidates = new List<TextCandidate>();
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
                  FROM asset_text t
                  WHERE t.asset_id = a.id
                    AND t.pipeline_version = $pipeline_version
                    AND t.source_size = a.size
                    AND t.source_modified_at = a.modified_at
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
            candidates.Add(new TextCandidate(
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

    public async Task<bool> SaveTextAsync(
        AssetText text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.PipelineVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(text));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO asset_text(
                asset_id, extractor_name, pipeline_version, status,
                source_size, source_modified_at, title, plain_text,
                headings_json, encoding_name, is_truncated,
                error_message, extracted_at)
            SELECT
                $asset_id, $extractor_name, $pipeline_version, $status,
                $source_size, $source_modified_at, $title, $plain_text,
                $headings_json, $encoding_name, $is_truncated,
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
                title = excluded.title,
                plain_text = excluded.plain_text,
                headings_json = excluded.headings_json,
                encoding_name = excluded.encoding_name,
                is_truncated = excluded.is_truncated,
                error_message = excluded.error_message,
                extracted_at = excluded.extracted_at;
            """;
        AddTextParameters(command, text);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<AssetText?> GetTextAsync(
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                asset_id, extractor_name, pipeline_version, status,
                source_size, source_modified_at, title, plain_text,
                headings_json, encoding_name, is_truncated,
                error_message, extracted_at
            FROM asset_text
            WHERE asset_id = $asset_id;
            """;
        command.Parameters.AddWithValue("$asset_id", assetId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadText(reader, Guid.Parse(reader.GetString(0)), 1)
            : null;
    }

    private static void AddTextParameters(SqliteCommand command, AssetText text)
    {
        command.Parameters.AddWithValue("$asset_id", text.AssetId.ToString("D"));
        command.Parameters.AddWithValue("$extractor_name", text.ExtractorName);
        command.Parameters.AddWithValue("$pipeline_version", text.PipelineVersion);
        command.Parameters.AddWithValue("$status", text.Status.ToString());
        command.Parameters.AddWithValue("$source_size", text.SourceSize);
        command.Parameters.AddWithValue(
            "$source_modified_at",
            text.SourceModifiedAt.ToString("O"));
        command.Parameters.AddWithValue(
            "$title",
            (object?)text.Content?.Title ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$plain_text",
            (object?)text.Content?.PlainText ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$headings_json",
            text.Content is null
                ? DBNull.Value
                : JsonSerializer.Serialize(text.Content.Headings));
        command.Parameters.AddWithValue(
            "$encoding_name",
            (object?)text.Content?.EncodingName ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$is_truncated",
            text.Content is null
                ? DBNull.Value
                : text.Content.IsTruncated ? 1 : 0);
        command.Parameters.AddWithValue(
            "$error_message",
            (object?)text.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$extracted_at", text.ExtractedAt.ToString("O"));
    }

    private static AssetText? ReadText(
        SqliteDataReader reader,
        Guid assetId,
        int offset)
    {
        if (reader.IsDBNull(offset))
        {
            return null;
        }

        AssetTextContent? content = null;
        if (!reader.IsDBNull(offset + 6) && !reader.IsDBNull(offset + 8))
        {
            var headings = reader.IsDBNull(offset + 7)
                ? []
                : JsonSerializer.Deserialize<string[]>(reader.GetString(offset + 7)) ?? [];
            content = new AssetTextContent(
                reader.IsDBNull(offset + 5) ? null : reader.GetString(offset + 5),
                reader.GetString(offset + 6),
                headings,
                reader.GetString(offset + 8),
                !reader.IsDBNull(offset + 9) && reader.GetInt64(offset + 9) != 0);
        }

        return new AssetText(
            assetId,
            reader.GetString(offset),
            reader.GetInt32(offset + 1),
            Enum.Parse<TextExtractionStatus>(reader.GetString(offset + 2)),
            reader.GetInt64(offset + 3),
            ParseTimestamp(reader.GetString(offset + 4)),
            content,
            ParseTimestamp(reader.GetString(offset + 11)),
            reader.IsDBNull(offset + 10) ? null : reader.GetString(offset + 10));
    }
}
