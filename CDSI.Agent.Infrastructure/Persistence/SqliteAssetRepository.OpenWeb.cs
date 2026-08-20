using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.OpenWeb;

namespace CDSI.Agent.Infrastructure.Persistence;

public sealed partial class SqliteAssetRepository : IOpenWebSettingsRepository
{
    private const string OpenWebOriginDomainKey = "openweb.origin_domain";
    private const string OpenWebWordPressUsernameKey = "openweb.wordpress_username";

    public async Task<OpenWebSettings> GetOpenWebSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT setting_key, setting_value, updated_at
            FROM agent_settings
            WHERE setting_key IN ($origin_domain_key, $username_key);
            """;
        command.Parameters.AddWithValue(
            "$origin_domain_key",
            OpenWebOriginDomainKey);
        command.Parameters.AddWithValue(
            "$username_key",
            OpenWebWordPressUsernameKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        string? originDomain = null;
        string? username = null;
        DateTimeOffset? updatedAt = null;
        while (await reader.ReadAsync(cancellationToken))
        {
            var key = reader.GetString(0);
            if (string.Equals(key, OpenWebOriginDomainKey, StringComparison.Ordinal))
            {
                originDomain = reader.GetString(1);
            }
            else if (string.Equals(
                         key,
                         OpenWebWordPressUsernameKey,
                         StringComparison.Ordinal))
            {
                username = reader.GetString(1);
            }

            var itemUpdatedAt = ParseTimestamp(reader.GetString(2));
            if (updatedAt is null || itemUpdatedAt > updatedAt)
            {
                updatedAt = itemUpdatedAt;
            }
        }

        return new OpenWebSettings(
            originDomain,
            username,
            HasApplicationPassword: false,
            updatedAt);
    }

    public async Task SaveOpenWebSettingsAsync(
        OpenWebSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (settings.OriginDomain is null)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)transaction;
            command.CommandText =
                """
                DELETE FROM agent_settings
                WHERE setting_key IN ($origin_domain_key, $username_key);
                """;
            command.Parameters.AddWithValue(
                "$origin_domain_key",
                OpenWebOriginDomainKey);
            command.Parameters.AddWithValue(
                "$username_key",
                OpenWebWordPressUsernameKey);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(settings.WordPressUsername);
            var updatedAt = (settings.UpdatedAt ?? DateTimeOffset.UtcNow).ToString("O");
            await UpsertSettingAsync(
                connection,
                (Microsoft.Data.Sqlite.SqliteTransaction)transaction,
                OpenWebOriginDomainKey,
                settings.OriginDomain,
                updatedAt,
                cancellationToken);
            await UpsertSettingAsync(
                connection,
                (Microsoft.Data.Sqlite.SqliteTransaction)transaction,
                OpenWebWordPressUsernameKey,
                settings.WordPressUsername,
                updatedAt,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task UpsertSettingAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        string key,
        string value,
        string updatedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO agent_settings(setting_key, setting_value, updated_at)
            VALUES($setting_key, $setting_value, $updated_at)
            ON CONFLICT(setting_key) DO UPDATE SET
                setting_value = excluded.setting_value,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$setting_key", key);
        command.Parameters.AddWithValue("$setting_value", value);
        command.Parameters.AddWithValue("$updated_at", updatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
