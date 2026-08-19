using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.OpenWeb;

namespace CDSI.Agent.Infrastructure.Persistence;

public sealed partial class SqliteAssetRepository : IOpenWebSettingsRepository
{
    private const string OpenWebOriginDomainKey = "openweb.origin_domain";

    public async Task<OpenWebSettings> GetOpenWebSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT setting_value, updated_at
            FROM agent_settings
            WHERE setting_key = $setting_key;
            """;
        command.Parameters.AddWithValue(
            "$setting_key",
            OpenWebOriginDomainKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new OpenWebSettings(
                reader.GetString(0),
                ParseTimestamp(reader.GetString(1)))
            : new OpenWebSettings(null, null);
    }

    public async Task SaveOpenWebSettingsAsync(
        OpenWebSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        if (settings.OriginDomain is null)
        {
            command.CommandText =
                """
                DELETE FROM agent_settings
                WHERE setting_key = $setting_key;
                """;
            command.Parameters.AddWithValue(
                "$setting_key",
                OpenWebOriginDomainKey);
        }
        else
        {
            command.CommandText =
                """
                INSERT INTO agent_settings(
                    setting_key, setting_value, updated_at)
                VALUES(
                    $setting_key, $setting_value, $updated_at)
                ON CONFLICT(setting_key) DO UPDATE SET
                    setting_value = excluded.setting_value,
                    updated_at = excluded.updated_at;
                """;
            command.Parameters.AddWithValue(
                "$setting_key",
                OpenWebOriginDomainKey);
            command.Parameters.AddWithValue(
                "$setting_value",
                settings.OriginDomain);
            command.Parameters.AddWithValue(
                "$updated_at",
                (settings.UpdatedAt ?? DateTimeOffset.UtcNow).ToString("O"));
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
