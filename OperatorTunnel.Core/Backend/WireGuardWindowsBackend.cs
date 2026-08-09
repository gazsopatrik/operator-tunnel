namespace OperatorTunnel.Core.Backend;

public sealed record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError);

public interface IProcessRunner
{
    Task<ProcessExecutionResult> RunAsync(ExternalProcessCommand command, CancellationToken cancellationToken = default);
}

public sealed record BackendOperationResult(bool Succeeded, string? Error = null)
{
    public static BackendOperationResult Success() => new(true);
    public static BackendOperationResult Failure(string error) => new(false, error);
}

public interface IWireGuardBackend
{
    Task<BackendOperationResult> InstallAsync(string tunnelName, string configPath, CancellationToken cancellationToken = default);
    Task<BackendOperationResult> StartAsync(string tunnelName, CancellationToken cancellationToken = default);
    Task<BackendOperationResult> StopAsync(string tunnelName, CancellationToken cancellationToken = default);
    Task<BackendOperationResult> UninstallAsync(string tunnelName, CancellationToken cancellationToken = default);
    Task<BackendOperationResult> QueryAsync(string tunnelName, CancellationToken cancellationToken = default);
}

public sealed class DemoWireGuardBackend : IWireGuardBackend
{
    public Task<BackendOperationResult> InstallAsync(string tunnelName, string configPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(BackendOperationResult.Success());

    public Task<BackendOperationResult> StartAsync(string tunnelName, CancellationToken cancellationToken = default) =>
        Task.FromResult(BackendOperationResult.Success());

    public Task<BackendOperationResult> StopAsync(string tunnelName, CancellationToken cancellationToken = default) =>
        Task.FromResult(BackendOperationResult.Success());

    public Task<BackendOperationResult> UninstallAsync(string tunnelName, CancellationToken cancellationToken = default) =>
        Task.FromResult(BackendOperationResult.Success());

    public Task<BackendOperationResult> QueryAsync(string tunnelName, CancellationToken cancellationToken = default) =>
        Task.FromResult(BackendOperationResult.Success());
}

/// <summary>
/// Thin adapter around the official WireGuard Windows tunnel service commands.
/// It deliberately receives an injected process runner so unit tests never touch
/// Windows services or the local network.
/// </summary>
public sealed class WireGuardWindowsBackend : IWireGuardBackend
{
    private readonly IProcessRunner _processRunner;
    private readonly string _wireGuardExecutable;

    public WireGuardWindowsBackend(IProcessRunner processRunner, string wireGuardExecutable = "wireguard.exe")
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        ArgumentException.ThrowIfNullOrWhiteSpace(wireGuardExecutable);
        _wireGuardExecutable = wireGuardExecutable;
    }

    public Task<BackendOperationResult> InstallAsync(string tunnelName, string configPath, CancellationToken cancellationToken = default) =>
        ExecuteAsync(WireGuardServiceCommands.InstallTunnel(_wireGuardExecutable, configPath), cancellationToken);

    public Task<BackendOperationResult> StartAsync(string tunnelName, CancellationToken cancellationToken = default) =>
        ExecuteAsync(WireGuardServiceCommands.StartTunnel(tunnelName), cancellationToken);

    public Task<BackendOperationResult> StopAsync(string tunnelName, CancellationToken cancellationToken = default) =>
        ExecuteAsync(WireGuardServiceCommands.StopTunnel(tunnelName), cancellationToken);

    public Task<BackendOperationResult> UninstallAsync(string tunnelName, CancellationToken cancellationToken = default) =>
        ExecuteAsync(WireGuardServiceCommands.UninstallTunnel(_wireGuardExecutable, tunnelName), cancellationToken);

    public Task<BackendOperationResult> QueryAsync(string tunnelName, CancellationToken cancellationToken = default) =>
        ExecuteAsync(WireGuardServiceCommands.QueryTunnel(tunnelName), cancellationToken);

    private async Task<BackendOperationResult> ExecuteAsync(ExternalProcessCommand command, CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunAsync(command, cancellationToken);
        return result.ExitCode == 0
            ? BackendOperationResult.Success()
            : BackendOperationResult.Failure($"WireGuard command failed with exit code {result.ExitCode}.");
    }
}
