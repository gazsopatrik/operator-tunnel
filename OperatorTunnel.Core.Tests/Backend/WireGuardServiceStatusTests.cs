using OperatorTunnel.Core.Backend;

namespace OperatorTunnel.Core.Tests.Backend;

public sealed class WireGuardServiceStatusTests
{
    [Theory]
    [InlineData("        STATE              : 4  RUNNING", WireGuardServiceStatus.Running)]
    [InlineData("        STATE              : 1  STOPPED", WireGuardServiceStatus.Stopped)]
    [InlineData("        STATE              : 2  START_PENDING", WireGuardServiceStatus.StartPending)]
    [InlineData("        STATE              : 3  STOP_PENDING", WireGuardServiceStatus.StopPending)]
    public void ParsesKnownServiceStates(string output, WireGuardServiceStatus expected)
    {
        var parsed = WireGuardServiceStatusParser.TryParse(output, out var status);

        Assert.True(parsed);
        Assert.Equal(expected, status);
    }

    [Fact]
    public void RejectsMissingOrUnknownState()
    {
        Assert.False(WireGuardServiceStatusParser.TryParse("SERVICE_NAME: WireGuardTunnel$demo", out _));
        Assert.False(WireGuardServiceStatusParser.TryParse("STATE : 9 UNKNOWN", out _));
    }

    [Fact]
    public async Task WindowsBackendReturnsParsedStatus()
    {
        var runner = new StatusProcessRunner(new(0, "STATE : 4 RUNNING", string.Empty));
        var backend = new WireGuardWindowsBackend(runner);

        var result = await backend.QueryStatusAsync("demo");

        Assert.True(result.Succeeded);
        Assert.Equal(WireGuardServiceStatus.Running, result.Status);
        Assert.Equal(["query", "WireGuardTunnel$demo"], runner.Command?.Arguments);
    }

    private sealed class StatusProcessRunner(ProcessExecutionResult result) : IProcessRunner
    {
        public ExternalProcessCommand? Command { get; private set; }

        public Task<ProcessExecutionResult> RunAsync(ExternalProcessCommand command, CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(result);
        }
    }
}
