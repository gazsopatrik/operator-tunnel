using OperatorTunnel.Core.Network;

namespace OperatorTunnel.Core.Tests.Network;

public sealed class NetworkPolicyTests
{
    [Fact]
    public void BlockingKillSwitchRequiresTunnelDns()
    {
        var policy = new NetworkPolicy(KillSwitchMode.BlockOutsideTunnel, new(false, []));

        var result = new NetworkPolicyValidator().Validate(policy);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "policy.dns_required");
    }

    [Fact]
    public void LoopbackDnsIsRejected()
    {
        var policy = new NetworkPolicy(KillSwitchMode.Disabled, new(true, ["127.0.0.1"]));

        var result = new NetworkPolicyValidator().Validate(policy);

        Assert.Contains(result.Issues, issue => issue.Code == "dns.server.invalid");
    }

    [Fact]
    public void EnforcedPolicyIsNotReadyBeforeTunnelConnects()
    {
        var policy = new NetworkPolicy(KillSwitchMode.BlockOutsideTunnel, new(true, ["10.0.0.1"]));

        var result = new NetworkPolicyEvaluator().Evaluate(policy, tunnelConnected: false);

        Assert.False(result.DnsReady);
        Assert.False(result.KillSwitchReady);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void DisabledKillSwitchDoesNotBlockOfflineState()
    {
        var policy = new NetworkPolicy(KillSwitchMode.Disabled, new(false, []));

        var result = new NetworkPolicyEvaluator().Evaluate(policy, tunnelConnected: false);

        Assert.True(result.DnsReady);
        Assert.True(result.KillSwitchReady);
        Assert.Empty(result.Warnings);
    }
}

