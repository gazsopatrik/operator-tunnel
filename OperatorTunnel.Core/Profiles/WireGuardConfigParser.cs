namespace OperatorTunnel.Core.Profiles;

public sealed record ConfigParseIssue(int Line, string Code, string Message);

public sealed record ConfigParseResult(
    WireGuardProfile? Profile,
    IReadOnlyList<ConfigParseIssue> Issues)
{
    public bool IsValid => Profile is not null && Issues.Count == 0;
}

/// <summary>
/// Parses the deliberately small client-side subset of the WireGuard INI format.
/// Unsupported directives are rejected instead of being silently ignored.
/// </summary>
public sealed class WireGuardConfigParser
{
    private static readonly HashSet<string> InterfaceKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Address", "PrivateKey", "DNS"
    };

    private static readonly HashSet<string> PeerKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "PublicKey", "PresharedKey", "AllowedIPs", "Endpoint", "PersistentKeepalive"
    };

    public ConfigParseResult Parse(string text, string profileName = "Imported profile")
    {
        ArgumentNullException.ThrowIfNull(text);

        var issues = new List<ConfigParseIssue>();
        var address = string.Empty;
        var privateKey = string.Empty;
        var dnsServers = new List<string>();
        var peers = new List<WireGuardPeerBuilder>();
        WireGuardPeerBuilder? currentPeer = null;
        string? section = null;
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var line = lines[index].Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                seenKeys.Clear();
                if (section.Equals("Interface", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentPeer is not null)
                    {
                        peers.Add(currentPeer);
                        currentPeer = null;
                    }
                }
                else if (section.Equals("Peer", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentPeer is not null)
                        peers.Add(currentPeer);
                    currentPeer = new WireGuardPeerBuilder();
                }
                else
                {
                    issues.Add(new(lineNumber, "section.unsupported", $"Unsupported section '{section}'."));
                }

                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                issues.Add(new(lineNumber, "syntax.key_value_required", "Expected a key=value assignment."));
                continue;
            }

            if (section is null)
            {
                issues.Add(new(lineNumber, "syntax.section_required", "A key=value assignment must be inside a section."));
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (!seenKeys.Add(key))
            {
                issues.Add(new(lineNumber, "key.duplicate", $"Duplicate key '{key}' in [{section}]."));
                continue;
            }

            if (section.Equals("Interface", StringComparison.OrdinalIgnoreCase))
            {
                if (!InterfaceKeys.Contains(key))
                {
                    issues.Add(new(lineNumber, "key.unsupported", $"Unsupported interface key '{key}'."));
                    continue;
                }

                switch (key.ToUpperInvariant())
                {
                    case "ADDRESS": address = value; break;
                    case "PRIVATEKEY": privateKey = value; break;
                    case "DNS": dnsServers.AddRange(SplitList(value)); break;
                }
            }
            else if (section.Equals("Peer", StringComparison.OrdinalIgnoreCase) && currentPeer is not null)
            {
                if (!PeerKeys.Contains(key))
                {
                    issues.Add(new(lineNumber, "key.unsupported", $"Unsupported peer key '{key}'."));
                    continue;
                }

                switch (key.ToUpperInvariant())
                {
                    case "PUBLICKEY": currentPeer.PublicKey = value; break;
                    case "PRESHAREDKEY": currentPeer.PresharedKey = value; break;
                    case "ALLOWEDIPS": currentPeer.AllowedIps.AddRange(SplitList(value)); break;
                    case "ENDPOINT": currentPeer.Endpoint = value; break;
                    case "PERSISTENTKEEPALIVE":
                        if (int.TryParse(value, out var keepalive)) currentPeer.PersistentKeepaliveSeconds = keepalive;
                        else issues.Add(new(lineNumber, "peer.keepalive.invalid", "PersistentKeepalive must be an integer."));
                        break;
                }
            }
        }

        if (currentPeer is not null)
            peers.Add(currentPeer);

        if (!lines.Any(line => line.Trim().Equals("[Interface]", StringComparison.OrdinalIgnoreCase)))
            issues.Add(new(0, "interface.required", "An [Interface] section is required."));
        if (string.IsNullOrWhiteSpace(address))
            issues.Add(new(0, "interface.address.required", "Interface Address is required."));
        if (string.IsNullOrWhiteSpace(privateKey))
            issues.Add(new(0, "interface.private_key.required", "Interface PrivateKey is required."));
        if (peers.Count == 0)
            issues.Add(new(0, "peer.required", "At least one [Peer] section is required."));

        foreach (var peer in peers)
        {
            if (string.IsNullOrWhiteSpace(peer.PublicKey))
                issues.Add(new(0, "peer.public_key.required", "Each peer requires PublicKey."));
            if (peer.AllowedIps.Count == 0)
                issues.Add(new(0, "peer.allowed_ips.required", "Each peer requires AllowedIPs."));
        }

        if (issues.Count > 0)
            return new ConfigParseResult(null, issues);

        return new ConfigParseResult(
            new WireGuardProfile(
                profileName,
                address,
                privateKey,
                dnsServers,
                peers.Select(peer => peer.Build()).ToArray()),
            issues);
    }

    private static IEnumerable<string> SplitList(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private sealed class WireGuardPeerBuilder
    {
        public string PublicKey { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public List<string> AllowedIps { get; } = [];
        public int? PersistentKeepaliveSeconds { get; set; }
        public string? PresharedKey { get; set; }

        public WireGuardPeer Build() => new(PublicKey, Endpoint, AllowedIps, PersistentKeepaliveSeconds, PresharedKey);
    }
}

