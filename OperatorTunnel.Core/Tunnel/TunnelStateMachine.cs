namespace OperatorTunnel.Core.Tunnel;

public enum TunnelState
{
    Offline,
    Connecting,
    Connected,
    Disconnecting,
    Error
}

public sealed record TunnelTransitionResult(
    bool Accepted,
    TunnelState State,
    string? Error = null);

/// <summary>
/// Owns legal tunnel lifecycle transitions. It does not talk to WireGuard;
/// the service adapter will drive the transition completion later.
/// </summary>
public sealed class TunnelStateMachine
{
    public TunnelState State { get; private set; } = TunnelState.Offline;

    public TunnelTransitionResult BeginConnect(bool hasValidatedProfile)
    {
        if (!hasValidatedProfile)
            return Reject("A validated profile is required before connecting.");

        if (State is not (TunnelState.Offline or TunnelState.Error))
            return Reject($"Cannot connect while tunnel state is {State}.");

        State = TunnelState.Connecting;
        return Accept();
    }

    public TunnelTransitionResult CompleteConnect()
    {
        if (State != TunnelState.Connecting)
            return Reject("Connect completion is only valid while connecting.");

        State = TunnelState.Connected;
        return Accept();
    }

    public TunnelTransitionResult BeginDisconnect()
    {
        if (State != TunnelState.Connected)
            return Reject("Disconnect is only valid while connected.");

        State = TunnelState.Disconnecting;
        return Accept();
    }

    public TunnelTransitionResult CompleteDisconnect()
    {
        if (State != TunnelState.Disconnecting)
            return Reject("Disconnect completion is only valid while disconnecting.");

        State = TunnelState.Offline;
        return Accept();
    }

    public TunnelTransitionResult Fail(string reason)
    {
        State = TunnelState.Error;
        return new(false, State, reason);
    }

    private TunnelTransitionResult Accept() => new(true, State);

    private TunnelTransitionResult Reject(string error) => new(false, State, error);
}

