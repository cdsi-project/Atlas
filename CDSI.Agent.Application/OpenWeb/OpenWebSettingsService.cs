using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.OpenWeb;

namespace CDSI.Agent.Application.OpenWeb;

public sealed class OpenWebSettingsService
{
    private const string ApplicationPasswordSecretKey = "openweb-wordpress";
    private readonly IOpenWebSettingsRepository _repository;
    private readonly ISecretStore _secretStore;

    public OpenWebSettingsService(
        IOpenWebSettingsRepository repository,
        ISecretStore secretStore)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(secretStore);
        _repository = repository;
        _secretStore = secretStore;
    }

    public async Task<OpenWebSettings> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetOpenWebSettingsAsync(cancellationToken);
        var hasApplicationPassword = await _secretStore.ExistsAsync(
            ApplicationPasswordSecretKey,
            cancellationToken);
        return settings with
        {
            HasApplicationPassword = hasApplicationPassword
        };
    }

    public async Task<OpenWebSettings> SaveAsync(
        string? originDomain,
        string? wordPressUsername,
        string? applicationPassword,
        CancellationToken cancellationToken = default)
    {
        if (!OpenWebOriginDomain.TryNormalize(
                originDomain,
                out var normalizedDomain,
                out var errorMessage))
        {
            throw new ArgumentException(errorMessage, nameof(originDomain));
        }

        var normalizedUsername = NormalizeUsername(wordPressUsername);
        var normalizedPassword = NormalizeApplicationPassword(applicationPassword);
        if (normalizedDomain is null)
        {
            if (normalizedUsername is not null || normalizedPassword is not null)
            {
                throw new ArgumentException(
                    "清除 OpenWeb 配置时，WordPress 用户名和应用程序密码也必须留空。");
            }

            var cleared = new OpenWebSettings(null, null, false, null);
            await _repository.SaveOpenWebSettingsAsync(cleared, cancellationToken);
            await _secretStore.DeleteAsync(
                ApplicationPasswordSecretKey,
                cancellationToken);
            return cleared;
        }

        if (normalizedUsername is null)
        {
            throw new ArgumentException("必须填写 WordPress 用户名。", nameof(wordPressUsername));
        }

        var hasStoredPassword = await _secretStore.ExistsAsync(
            ApplicationPasswordSecretKey,
            cancellationToken);
        if (normalizedPassword is null && !hasStoredPassword)
        {
            throw new ArgumentException(
                "首次配置时必须填写 WordPress 应用程序密码。",
                nameof(applicationPassword));
        }

        var settings = new OpenWebSettings(
            normalizedDomain,
            normalizedUsername,
            normalizedPassword is not null || hasStoredPassword,
            DateTimeOffset.UtcNow);
        await _repository.SaveOpenWebSettingsAsync(settings, cancellationToken);
        if (normalizedPassword is not null)
        {
            await _secretStore.StoreAsync(
                ApplicationPasswordSecretKey,
                normalizedPassword,
                cancellationToken);
        }

        return settings;
    }

    public async Task<OpenWebConnection> GetConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await GetAsync(cancellationToken);
        if (settings.OriginDomain is null ||
            settings.WordPressUsername is null ||
            !settings.HasApplicationPassword)
        {
            throw new InvalidOperationException(
                "OpenWeb 的 WordPress 连接配置不完整，请先在设置中保存域名、用户名和应用程序密码。");
        }

        var applicationPassword = await _secretStore.RetrieveAsync(
            ApplicationPasswordSecretKey,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(applicationPassword))
        {
            throw new InvalidOperationException(
                "WordPress 应用程序密码不可用，请在 OpenWeb 设置中重新保存。");
        }

        return new OpenWebConnection(
            settings.OriginDomain,
            settings.WordPressUsername,
            applicationPassword);
    }

    private static string? NormalizeUsername(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var username = value.Trim();
        if (username.Length > 100 || username.Contains(':'))
        {
            throw new ArgumentException(
                "WordPress 用户名最多 100 个字符，且不能包含冒号。",
                nameof(value));
        }

        return username;
    }

    private static string? NormalizeApplicationPassword(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var password = string.Concat(value.Where(character => !char.IsWhiteSpace(character)));
        if (password.Length > 512)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "WordPress 应用程序密码过长。");
        }

        return password;
    }
}
