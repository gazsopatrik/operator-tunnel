namespace OperatorTunnel.Core.Profiles;

public sealed record WireGuardProfile(
    string Name,
    string InterfaceAddress,
    string PrivateKey,
    IReadOnlyList<string> DnsServers,
    IReadOnlyList<WireGuardPeer> Peers);

public sealed record WireGuardPeer(
    string PublicKey,
    string Endpoint,
    IReadOnlyList<string> AllowedIps,
    int? PersistentKeepaliveSeconds,
    string? PresharedKey);

