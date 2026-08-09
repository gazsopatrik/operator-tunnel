using OperatorTunnel.Core.Tunnel;

namespace OperatorTunnel.Core.Tests.Tunnel;

public sealed class TunnelStateMachineTests
{
    [Fact]
    public void ConnectWithoutValidatedProfile_IsRejected()
    {
        var machine = new TunnelStateMachine();

        var result = machine.BeginConnect(hasValidatedProfile: false);

        Assert.False(result.Accepted);
        Assert.Equal(TunnelState.Offline, result.State);
    }

    [Fact]
    public void ValidLifecycle_ReachesOfflineAgain()
    {
        var machine = new TunnelStateMachine();

        Assert.True(machine.BeginConnect(hasValidatedProfile: true).Accepted);
        Assert.Equal(TunnelState.Connecting, machine.State);
        Assert.True(machine.CompleteConnect().Accepted);
        Assert.Equal(TunnelState.Connected, machine.State);
        Assert.True(machine.BeginDisconnect().Accepted);
        Assert.True(machine.CompleteDisconnect().Accepted);
        Assert.Equal(TunnelState.Offline, machine.State);
    }

    [Fact]
    public void InvalidTransition_IsRejectedWithoutChangingState()
    {
        var machine = new TunnelStateMachine();

        var result = machine.CompleteConnect();

        Assert.False(result.Accepted);
        Assert.Equal(TunnelState.Offline, machine.State);
    }

    [Fact]
    public void FailureMovesMachineToError()
    {
        var machine = new TunnelStateMachine();

        var result = machine.Fail("backend unavailable");

        Assert.False(result.Accepted);
        Assert.Equal(TunnelState.Error, machine.State);
        Assert.Equal("backend unavailable", result.Error);
    }
}

