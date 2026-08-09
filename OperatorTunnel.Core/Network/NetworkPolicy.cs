using System.Net;

namespace OperatorTunnel.Core.Network;

public enum KillSwitchMode
{
    Disabled,
    BlockOutsideTunnel
}

public sealed record DnsPolicy(
    bool ForceTunnelDns,
    IReadOnlyList<string> Servers);

public sealed record NetworkPolicy(
    KillSwitchMode KillSwitch,
    DnsPolicy Dns);

public sealed record NetworkPolicyIssue(string Code, string Message);

public sealed record NetworkPolicyValidationResult(IReadOnlyList<NetworkPolicyIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public sealed class NetworkPolicyValidator
{
    public NetworkPolicyValidationResult Validate(NetworkPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var issues = new List<NetworkPolicyIssue>();

        if (policy.Dns.ForceTunnelDns && policy.Dns.Servers.Count == 0)
            issues.Add(new("dns.servers.required", "ForceTunnelDns requires at least one DNS server."));

        foreach (var server in policy.Dns.Servers)
        {
            if (!IPAddress.TryParse(server, out var address) || IPAddress.IsLoopback(address))
                issues.Add(new("dns.server.invalid", $"DNS server '{server}' is not a valid non-loopback IP address."));
        }

        if (policy.KillSwitch == KillSwitchMode.BlockOutsideTunnel && !policy.Dns.ForceTunnelDns)
            issues.Add(new("policy.dns_required", "A blocking kill switch requires tunnel DNS enforcement."));

        return new NetworkPolicyValidationResult(issues);
    }
}

public sealed record NetworkPolicyEvaluation(
    bool DnsReady,
    bool KillSwitchReady,
    IReadOnlyList<string> Warnings);

public sealed class NetworkPolicyEvaluator
{
    public NetworkPolicyEvaluation Evaluate(NetworkPolicy policy, bool tunnelConnected)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var warnings = new List<string>();
        var validation = new NetworkPolicyValidator().Validate(policy);
        warnings.AddRange(validation.Issues.Select(issue => issue.Message));

        var dnsReady = validation.IsValid && (!policy.Dns.ForceTunnelDns || tunnelConnected);
        var killSwitchReady = validation.IsValid && policy.KillSwitch == KillSwitchMode.Disabled ||
                              validation.IsValid && policy.KillSwitch == KillSwitchMode.BlockOutsideTunnel && tunnelConnected;

        if (policy.Dns.ForceTunnelDns && !tunnelConnected)
            warnings.Add("Tunnel DNS is not active while the tunnel is offline.");
        if (policy.KillSwitch == KillSwitchMode.BlockOutsideTunnel && !tunnelConnected)
            warnings.Add("Kill switch policy is armed but the tunnel is offline.");

        return new NetworkPolicyEvaluation(dnsReady, killSwitchReady, warnings);
    }
}

