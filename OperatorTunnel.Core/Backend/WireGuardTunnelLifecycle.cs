namespace OperatorTunnel.Core.Backend;

/// <summary>
/// Orchestrates the official per-tunnel service lifecycle. It does not serialize
/// profiles or write configuration files; callers must provide a controlled path.
/// </summary>
public sealed class WireGuardTunnelLifecycle
{
    private readonly IWireGuardBackend _backend;
    private readonly TimeSpan _pollInterval;
    private readonly int _maxPollAttempts;

    public WireGuardTunnelLifecycle(
        IWireGuardBackend backend,
        TimeSpan? pollInterval = null,
        int maxPollAttempts = 10)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        if (maxPollAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPollAttempts));

        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(250);
        _maxPollAttempts = maxPollAttempts;
    }

    public async Task<BackendOperationResult> ConnectAsync(
        string tunnelName,
        string configPath,
        CancellationToken cancellationToken = default)
    {
        var install = await _backend.InstallAsync(tunnelName, configPath, cancellationToken);
        if (!install.Succeeded)
            return install;

        var start = await _backend.StartAsync(tunnelName, cancellationToken);
        if (!start.Succeeded)
            return start;

        return await WaitForStatusAsync(tunnelName, WireGuardServiceStatus.Running, cancellationToken);
    }

    public async Task<BackendOperationResult> DisconnectAsync(
        string tunnelName,
        CancellationToken cancellationToken = default)
    {
        var stop = await _backend.StopAsync(tunnelName, cancellationToken);
        if (!stop.Succeeded)
            return stop;

        var stopped = await WaitForStatusAsync(tunnelName, WireGuardServiceStatus.Stopped, cancellationToken);
        if (!stopped.Succeeded)
            return stopped;

        return await _backend.UninstallAsync(tunnelName, cancellationToken);
    }

    private async Task<BackendOperationResult> WaitForStatusAsync(
        string tunnelName,
        WireGuardServiceStatus expected,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < _maxPollAttempts; attempt++)
        {
            var status = await _backend.QueryStatusAsync(tunnelName, cancellationToken);
            if (status.Succeeded && status.Status == expected)
                return BackendOperationResult.Success();

            if (attempt + 1 < _maxPollAttempts && _pollInterval > TimeSpan.Zero)
                await Task.Delay(_pollInterval, cancellationToken);
        }

        return BackendOperationResult.Failure($"WireGuard service did not reach {expected} state in time.");
    }
}
