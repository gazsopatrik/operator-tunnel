using System.Text;

namespace OperatorTunnel.Core.Profiles;

/// <summary>
/// Serializes an already validated profile to the supported WireGuard config subset.
/// This returns secret-bearing text in memory only; callers must choose secure storage.
/// </summary>
public sealed class WireGuardConfigSerializer
{
    public string Serialize(WireGuardProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var validation = new WireGuardProfileValidator().Validate(profile);
        if (!validation.IsValid)
            throw new ArgumentException("Only a validated profile can be serialized.", nameof(profile));

        var builder = new StringBuilder();
        builder.AppendLine("[Interface]");
        builder.Append("Address = ").AppendLine(profile.InterfaceAddress);
        builder.Append("PrivateKey = ").AppendLine(profile.PrivateKey);

        if (profile.DnsServers.Count > 0)
            builder.Append("DNS = ").AppendLine(string.Join(", ", profile.DnsServers));

        foreach (var peer in profile.Peers)
        {
            builder.AppendLine();
            builder.AppendLine("[Peer]");
            builder.Append("PublicKey = ").AppendLine(peer.PublicKey);

            if (!string.IsNullOrWhiteSpace(peer.PresharedKey))
                builder.Append("PresharedKey = ").AppendLine(peer.PresharedKey);

            builder.Append("AllowedIPs = ").AppendLine(string.Join(", ", peer.AllowedIps));

            if (!string.IsNullOrWhiteSpace(peer.Endpoint))
                builder.Append("Endpoint = ").AppendLine(peer.Endpoint);

            if (peer.PersistentKeepaliveSeconds is not null)
                builder.Append("PersistentKeepalive = ").AppendLine(peer.PersistentKeepaliveSeconds.Value.ToString());
        }

        return builder.ToString();
    }
}

