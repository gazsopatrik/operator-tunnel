using OperatorTunnel.Core.Profiles;

namespace OperatorTunnel.Core.Tests.Profiles;

public sealed class WireGuardProfileValidatorTests
{
    private readonly WireGuardProfileValidator _validator = new();

    [Fact]
    public void EmptyProfile_IsRejected()
    {
        var result = _validator.Validate(new WireGuardProfile(
            Name: "",
            InterfaceAddress: "",
            PrivateKey: "",
            DnsServers: [],
            Peers: []));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.name.required");
        Assert.Contains(result.Issues, issue => issue.Code == "peer.required");
    }

    [Fact]
    public void ValidProfile_IsAccepted()
    {
        var profile = new WireGuardProfile(
            Name: "demo",
            InterfaceAddress: "10.0.0.2/32",
            PrivateKey: "test-private-key",
            DnsServers: ["1.1.1.1"],
            Peers: [new WireGuardPeer(
                PublicKey: "test-public-key",
                Endpoint: "vpn.example.test:51820",
                AllowedIps: ["0.0.0.0/0", "::/0"],
                PersistentKeepaliveSeconds: 25,
                PresharedKey: null)]);

        Assert.True(_validator.Validate(profile).IsValid);
    }

    [Fact]
    public void KeepaliveOutsideRange_IsRejected()
    {
        var profile = new WireGuardProfile(
            "demo", "10.0.0.2/32", "private", [],
            [new WireGuardPeer("public", "vpn.example.test:51820", ["10.0.0.0/8"], 65536, null)]);

        var result = _validator.Validate(profile);

        Assert.Contains(result.Issues, issue => issue.Code == "peer.keepalive.range");
    }
}

