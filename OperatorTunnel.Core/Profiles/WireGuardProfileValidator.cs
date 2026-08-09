namespace OperatorTunnel.Core.Profiles;

public sealed record ValidationIssue(string Code, string Message);

public sealed record ProfileValidationResult(IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public sealed class WireGuardProfileValidator
{
    public ProfileValidationResult Validate(WireGuardProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var issues = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(profile.Name))
            issues.Add(new("profile.name.required", "A profile name is required."));

        if (string.IsNullOrWhiteSpace(profile.InterfaceAddress))
            issues.Add(new("interface.address.required", "An interface address is required."));

        if (string.IsNullOrWhiteSpace(profile.PrivateKey))
            issues.Add(new("interface.private_key.required", "A private key is required."));

        if (profile.Peers.Count == 0)
            issues.Add(new("peer.required", "At least one peer is required."));

        foreach (var peer in profile.Peers)
        {
            if (string.IsNullOrWhiteSpace(peer.PublicKey))
                issues.Add(new("peer.public_key.required", "Each peer must have a public key."));

            if (peer.AllowedIps.Count == 0)
                issues.Add(new("peer.allowed_ips.required", "Each peer must define at least one allowed IP."));

            if (peer.PersistentKeepaliveSeconds is < 0 or > 65535)
                issues.Add(new("peer.keepalive.range", "Persistent keepalive must be between 0 and 65535 seconds."));
        }

        return new ProfileValidationResult(issues);
    }
}

