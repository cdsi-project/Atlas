using CDSI.Agent.Core.OpenWeb;

namespace CDSI.Agent.Core.Abstractions;

public interface IOpenWebSettingsRepository
{
    Task<OpenWebSettings> GetOpenWebSettingsAsync(
        CancellationToken cancellationToken = default);

    Task SaveOpenWebSettingsAsync(
        OpenWebSettings settings,
        CancellationToken cancellationToken = default);
}
