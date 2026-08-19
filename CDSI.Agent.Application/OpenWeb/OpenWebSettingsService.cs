using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.OpenWeb;

namespace CDSI.Agent.Application.OpenWeb;

public sealed class OpenWebSettingsService
{
    private readonly IOpenWebSettingsRepository _repository;

    public OpenWebSettingsService(IOpenWebSettingsRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public Task<OpenWebSettings> GetAsync(
        CancellationToken cancellationToken = default)
    {
        return _repository.GetOpenWebSettingsAsync(cancellationToken);
    }

    public async Task<OpenWebSettings> SaveAsync(
        string? originDomain,
        CancellationToken cancellationToken = default)
    {
        if (!OpenWebOriginDomain.TryNormalize(
                originDomain,
                out var normalizedDomain,
                out var errorMessage))
        {
            throw new ArgumentException(errorMessage, nameof(originDomain));
        }

        var settings = new OpenWebSettings(
            normalizedDomain,
            DateTimeOffset.UtcNow);
        await _repository.SaveOpenWebSettingsAsync(settings, cancellationToken);
        return settings;
    }
}
