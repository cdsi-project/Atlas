namespace CDSI.Agent.Core.Git;

public sealed record GitProfile(
    Guid Id,
    string DisplayName,
    GitHostingProvider Provider,
    string RepositoryUrl,
    string AccountName,
    string DefaultBranch,
    bool IsDefault,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
