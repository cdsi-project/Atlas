using CDSI.Agent.Infrastructure.Security;

namespace CDSI.Agent.Infrastructure.Tests.Security;

public sealed class WindowsCredentialSecretStoreTests
{
    [Fact]
    public async Task ExistsAndDeleteAsync_HandleAnUnknownCredentialWithoutCreatingState()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store = new WindowsCredentialSecretStore();
        var key = $"test-{Guid.NewGuid():N}";

        Assert.False(await store.ExistsAsync(key));
        await store.DeleteAsync(key);
        Assert.False(await store.ExistsAsync(key));
    }

    [Fact]
    public async Task StoreAsync_RejectsInvalidKeysBeforeCallingTheOperatingSystem()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store = new WindowsCredentialSecretStore();

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.StoreAsync("invalid/key", "not-a-real-secret"));
    }
}
