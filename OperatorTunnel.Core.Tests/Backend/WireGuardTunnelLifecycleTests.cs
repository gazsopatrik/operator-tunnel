using OperatorTunnel.Core.Backend;

namespace OperatorTunnel.Core.Tests.Backend;

public sealed class WireGuardTunnelLifecycleTests
{
    [Fact]
    public async Task Connect_InstallsStartsAndWaitsForRunning()
    {
        var backend = new RecordingBackend([WireGuardServiceStatus.StartPending, WireGuardServiceStatus.Running]);
        var lifecycle = new WireGuardTunnelLifecycle(backend, TimeSpan.Zero, 3);

        var result = await lifecycle.ConnectAsync("demo", "C:\\controlled\\demo.conf");

        Assert.True(result.Succeeded);
        Assert.Equal(["install", "start", "status", "status"], backend.Operations);
        Assert.Equal("C:\\controlled\\demo.conf", backend.ConfigPath);
    }

    [Fact]
    public async Task Connect_DoesNotStartWhenInstallFails()
    {
        var backend = new RecordingBackend([], BackendOperationResult.Failure("install failed"));
        var lifecycle = new WireGuardTunnelLifecycle(backend, TimeSpan.Zero);

        var result = await lifecycle.ConnectAsync("demo", "C:\\controlled\\demo.conf");

        Assert.False(result.Succeeded);
        Assert.Equal(["install"], backend.Operations);
    }

    [Fact]
    public async Task Disconnect_StopsWaitsAndUninstalls()
    {
        var backend = new RecordingBackend([WireGuardServiceStatus.StopPending, WireGuardServiceStatus.Stopped]);
        var lifecycle = new WireGuardTunnelLifecycle(backend, TimeSpan.Zero, 3);

        var result = await lifecycle.DisconnectAsync("demo");

        Assert.True(result.Succeeded);
        Assert.Equal(["stop", "status", "status", "uninstall"], backend.Operations);
    }

    private sealed class RecordingBackend(
        IReadOnlyList<WireGuardServiceStatus> statuses,
        BackendOperationResult? installResult = null) : IWireGuardBackend
    {
        private int _statusIndex;
        private readonly BackendOperationResult _installResult = installResult ?? BackendOperationResult.Success();
        public List<string> Operations { get; } = [];
        public string? ConfigPath { get; private set; }

        public Task<BackendOperationResult> InstallAsync(string tunnelName, string configPath, CancellationToken cancellationToken = default)
        {
            Operations.Add("install");
            ConfigPath = configPath;
            return Task.FromResult(_installResult);
        }

        public Task<BackendOperationResult> StartAsync(string tunnelName, CancellationToken cancellationToken = default)
        {
            Operations.Add("start");
            return Task.FromResult(BackendOperationResult.Success());
        }

        public Task<BackendOperationResult> StopAsync(string tunnelName, CancellationToken cancellationToken = default)
        {
            Operations.Add("stop");
            return Task.FromResult(BackendOperationResult.Success());
        }

        public Task<BackendOperationResult> UninstallAsync(string tunnelName, CancellationToken cancellationToken = default)
        {
            Operations.Add("uninstall");
            return Task.FromResult(BackendOperationResult.Success());
        }

        public Task<BackendOperationResult> QueryAsync(string tunnelName, CancellationToken cancellationToken = default) =>
            Task.FromResult(BackendOperationResult.Success());

        public Task<WireGuardServiceStatusResult> QueryStatusAsync(string tunnelName, CancellationToken cancellationToken = default)
        {
            Operations.Add("status");
            var status = statuses.Count == 0 ? WireGuardServiceStatus.Unknown : statuses[Math.Min(_statusIndex++, statuses.Count - 1)];
            return Task.FromResult(new WireGuardServiceStatusResult(true, status));
        }

        public Task<BackendStatisticsResult> QueryStatisticsAsync(string tunnelName, CancellationToken cancellationToken = default) =>
            Task.FromResult(BackendStatisticsResult.Success(new WireGuardStatisticsParseResult([], [])));
    }
}
