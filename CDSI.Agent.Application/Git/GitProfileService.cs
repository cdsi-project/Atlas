using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.Git;

namespace CDSI.Agent.Application.Git;

public sealed class GitProfileService
{
    private readonly IGitProfileRepository _repository;
    private readonly ISecretStore _secretStore;

    public GitProfileService(
        IGitProfileRepository repository,
        ISecretStore secretStore)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(secretStore);
        _repository = repository;
        _secretStore = secretStore;
    }

    public async Task<IReadOnlyList<ConfiguredGitProfile>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var profiles = await _repository.ListGitProfilesAsync(cancellationToken);
        var configured = new List<ConfiguredGitProfile>(profiles.Count);
        foreach (var profile in profiles)
        {
            configured.Add(new ConfiguredGitProfile(
                profile,
                await _secretStore.ExistsAsync(
                    CreateSecretKey(profile.Id),
                    cancellationToken)));
        }

        return configured;
    }

    public async Task<ConfiguredGitProfile> SaveAsync(
        SaveGitProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Provider))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "不支持该 Git 托管平台。");
        }

        var profiles = await _repository.ListGitProfilesAsync(cancellationToken);
        var existing = request.Id is null
            ? null
            : profiles.SingleOrDefault(profile => profile.Id == request.Id.Value);
        if (request.Id is not null && existing is null)
        {
            throw new InvalidOperationException("Git 配置不存在或已被删除。");
        }

        if (!GitRepositoryAddress.TryNormalize(
                request.Provider,
                request.RepositoryUrl,
                out var repositoryUrl,
                out var errorMessage) ||
            repositoryUrl is null)
        {
            throw new ArgumentException(
                errorMessage ?? "Git 仓库地址无效。",
                nameof(request));
        }

        if (profiles.Any(profile =>
                profile.Id != request.Id &&
                profile.Provider == request.Provider &&
                string.Equals(
                    profile.RepositoryUrl,
                    repositoryUrl,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("该 Git 仓库已经配置。", nameof(request));
        }

        var id = existing?.Id ?? Guid.NewGuid();
        var secretKey = CreateSecretKey(id);
        var accessToken = NormalizeAccessToken(request.AccessToken);
        var hasStoredToken = await _secretStore.ExistsAsync(
            secretKey,
            cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var profile = new GitProfile(
            id,
            RequireValue(request.DisplayName, "配置名称", 100),
            request.Provider,
            repositoryUrl,
            RequireValue(request.AccountName, "账号", 100),
            NormalizeBranch(request.DefaultBranch),
            request.IsDefault || existing?.IsDefault == true || profiles.Count == 0,
            existing?.CreatedAt ?? now,
            now);

        if (existing is null && accessToken is not null)
        {
            await _secretStore.StoreAsync(secretKey, accessToken, cancellationToken);
            try
            {
                await _repository.SaveGitProfileAsync(profile, cancellationToken);
            }
            catch (Exception saveException)
            {
                try
                {
                    await _secretStore.DeleteAsync(secretKey, CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    throw new InvalidOperationException(
                        "Git 配置保存失败，且临时凭据未能清理。",
                        new AggregateException(saveException, cleanupException));
                }

                throw;
            }

            return new ConfiguredGitProfile(profile, true);
        }

        await _repository.SaveGitProfileAsync(profile, cancellationToken);
        if (accessToken is not null)
        {
            await _secretStore.StoreAsync(secretKey, accessToken, cancellationToken);
            return new ConfiguredGitProfile(profile, true);
        }

        return new ConfiguredGitProfile(profile, hasStoredToken);
    }

    public Task SetDefaultAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        return _repository.SetDefaultGitProfileAsync(profileId, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        var secretKey = CreateSecretKey(profileId);
        var previousToken = await _secretStore.RetrieveAsync(
            secretKey,
            cancellationToken);
        await _secretStore.DeleteAsync(secretKey, cancellationToken);
        try
        {
            await _repository.DeleteGitProfileAsync(profileId, cancellationToken);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(previousToken))
            {
                await _secretStore.StoreAsync(
                    secretKey,
                    previousToken,
                    CancellationToken.None);
            }

            throw;
        }
    }

    public async Task<string?> GetAccessTokenAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        var profileExists = (await _repository.ListGitProfilesAsync(cancellationToken))
            .Any(profile => profile.Id == profileId);
        if (!profileExists)
        {
            throw new InvalidOperationException("Git 配置不存在或已被删除。");
        }

        return await _secretStore.RetrieveAsync(
            CreateSecretKey(profileId),
            cancellationToken);
    }

    private static string NormalizeBranch(string? value)
    {
        var branch = RequireValue(value, "默认分支", 255);
        if (branch.Any(char.IsWhiteSpace) ||
            branch.StartsWith('.') ||
            branch.EndsWith('.') ||
            branch.StartsWith('/') ||
            branch.EndsWith('/') ||
            branch.Contains("..", StringComparison.Ordinal) ||
            branch.IndexOfAny(['~', '^', ':', '?', '*', '[', '\\']) >= 0)
        {
            throw new ArgumentException("默认分支名称无效。", nameof(value));
        }

        return branch;
    }

    private static string? NormalizeAccessToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var token = value.Trim();
        if (token.Length > 1024 || token.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("访问令牌格式无效。", nameof(value));
        }

        return token;
    }

    private static string RequireValue(
        string? value,
        string fieldName,
        int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                fieldName,
                $"{fieldName}最多允许 {maximumLength} 个字符。");
        }

        return normalized;
    }

    private static string CreateSecretKey(Guid profileId)
    {
        return $"git-access-token-{profileId:N}";
    }
}

public sealed record SaveGitProfileRequest(
    Guid? Id,
    string DisplayName,
    GitHostingProvider Provider,
    string RepositoryUrl,
    string AccountName,
    string DefaultBranch,
    string? AccessToken,
    bool IsDefault);

public sealed record ConfiguredGitProfile(
    GitProfile Profile,
    bool HasAccessToken);
