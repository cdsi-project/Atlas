using CDSI.Agent.WinForms;

namespace CDSI.Agent.WinForms.Tests;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public void SecondCoordinator_SignalsThePrimaryInstanceInsteadOfAcquiringTheLock()
    {
        var applicationId = $"CDSI.Atlas.Tests.{Guid.NewGuid():N}";
        using var activationReceived = new ManualResetEventSlim();
        using var primary = new SingleInstanceCoordinator(applicationId);
        using var secondary = new SingleInstanceCoordinator(applicationId);

        Assert.True(primary.IsPrimaryInstance);
        Assert.False(secondary.IsPrimaryInstance);
        primary.StartListening(activationReceived.Set);

        secondary.SignalPrimaryInstance();

        Assert.True(activationReceived.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void DisposingThePrimaryCoordinator_AllowsTheNextInstanceToStart()
    {
        var applicationId = $"CDSI.Atlas.Tests.{Guid.NewGuid():N}";
        var primary = new SingleInstanceCoordinator(applicationId);
        Assert.True(primary.IsPrimaryInstance);

        primary.Dispose();

        using var replacement = new SingleInstanceCoordinator(applicationId);
        Assert.True(replacement.IsPrimaryInstance);
    }

    [Fact]
    public void SecondaryCoordinator_CannotRegisterAnActivationListener()
    {
        var applicationId = $"CDSI.Atlas.Tests.{Guid.NewGuid():N}";
        using var primary = new SingleInstanceCoordinator(applicationId);
        using var secondary = new SingleInstanceCoordinator(applicationId);

        Assert.Throws<InvalidOperationException>(() =>
            secondary.StartListening(() => { }));
    }
}
