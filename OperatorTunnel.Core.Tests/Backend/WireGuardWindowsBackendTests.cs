using OperatorTunnel.Core.Backend;

namespace OperatorTunnel.Core.Tests.Backend;

public sealed class WireGuardWindowsBackendTests
{
    [Fact]
    public async Task StartAsync_DelegatesTheValidatedCommand()
    {
        var runner = new RecordingProcessRunner(new(0, "SERVICE_RUNNING", string.Empty));
        var backend = new WireGuardWindowsBackend(runner);

        var result = await backend.StartAsync("demo");

        Assert.True(result.Succeeded);
        Assert.Equal("sc.exe", runner.LastCommand?.FileName);
        Assert.Equal(["start", "WireGuardTunnel$demo"], runner.LastCommand?.Arguments);
    }

    [Fact]
    public async Task FailedProcess_ReturnsRedactedError()
    {
        var runner = new RecordingProcessRunner(new(5, string.Empty, "contains sensitive service detail"));
        var backend = new WireGuardWindowsBackend(runner);

        var result = await backend.StopAsync("demo");

        Assert.False(result.Succeeded);
        Assert.Equal("WireGuard command failed with exit code 5.", result.Error);
        Assert.DoesNotContain("sensitive", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InstallAsync_PassesConfigPathAsSingleArgument()
    {
        var runner = new RecordingProcessRunner(new(0, string.Empty, string.Empty));
        var backend = new WireGuardWindowsBackend(runner, "C:\\WireGuard\\wireguard.exe");

        await backend.InstallAsync("demo", "C:\\Test User\\demo.conf");

        Assert.Equal("C:\\WireGuard\\wireguard.exe", runner.LastCommand?.FileName);
        Assert.Equal(["/installtunnelservice", "C:\\Test User\\demo.conf"], runner.LastCommand?.Arguments);
    }

    [Fact]
    public async Task QueryStatisticsAsync_ParsesBothWireGuardOutputs()
    {
        var runner = new QueueProcessRunner(
        [
            new(0, "peer-key\t1700000000", string.Empty),
            new(0, "peer-key\t1024\t2048", string.Empty)
        ]);
        var backend = new WireGuardWindowsBackend(runner, "wg.exe");

        var result = await backend.QueryStatisticsAsync("demo");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Statistics);
        Assert.Equal(1024UL, result.Statistics!.Peers[0].ReceiveBytes);
        Assert.Equal(2048UL, result.Statistics.Peers[0].TransmitBytes);
        Assert.Equal(["show", "demo", "latest-handshakes"], runner.Commands[0].Arguments);
        Assert.Equal(["show", "demo", "transfer"], runner.Commands[1].Arguments);
    }

    [Fact]
    public async Task QueryStatisticsAsync_DoesNotExposeProcessErrorOutput()
    {
        var runner = new QueueProcessRunner([new(7, string.Empty, "private detail")]);
        var backend = new WireGuardWindowsBackend(runner);

        var result = await backend.QueryStatisticsAsync("demo");

        Assert.False(result.Succeeded);
        Assert.Equal("WireGuard statistics command failed with exit code 7.", result.Error);
        Assert.DoesNotContain("private detail", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingProcessRunner(ProcessExecutionResult result) : IProcessRunner
    {
        public ExternalProcessCommand? LastCommand { get; private set; }

        public Task<ProcessExecutionResult> RunAsync(ExternalProcessCommand command, CancellationToken cancellationToken = default)
        {
            LastCommand = command;
            return Task.FromResult(result);
        }
    }

    private sealed class QueueProcessRunner(IReadOnlyList<ProcessExecutionResult> results) : IProcessRunner
    {
        private int _index;
        public List<ExternalProcessCommand> Commands { get; } = [];

        public Task<ProcessExecutionResult> RunAsync(ExternalProcessCommand command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Task.FromResult(results[_index++]);
        }
    }
}
