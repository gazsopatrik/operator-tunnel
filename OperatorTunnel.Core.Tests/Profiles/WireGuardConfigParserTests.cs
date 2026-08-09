using OperatorTunnel.Core.Profiles;

namespace OperatorTunnel.Core.Tests.Profiles;

public sealed class WireGuardConfigParserTests
{
    private readonly WireGuardConfigParser _parser = new();

    [Fact]
    public void ValidClientConfig_IsParsedWithoutLoggingSecrets()
    {
        const string config = """
            # test fixture only
            [Interface]
            Address = 10.0.0.2/32
            PrivateKey = test-private-key
            DNS = 10.0.0.1, 1.1.1.1

            [Peer]
            PublicKey = test-public-key
            AllowedIPs = 0.0.0.0/0, ::/0
            Endpoint = vpn.example.test:51820
            PersistentKeepalive = 25
            """;

        var result = _parser.Parse(config, "Demo");

        Assert.True(result.IsValid);
        Assert.NotNull(result.Profile);
        Assert.Equal("Demo", result.Profile!.Name);
        Assert.Equal(["10.0.0.1", "1.1.1.1"], result.Profile.DnsServers);
        Assert.Equal("test-private-key", result.Profile.PrivateKey);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void DuplicateKey_IsRejected()
    {
        var result = _parser.Parse("""
            [Interface]
            Address = 10.0.0.2/32
            Address = 10.0.0.3/32
            PrivateKey = private
            [Peer]
            PublicKey = public
            AllowedIPs = 10.0.0.0/8
            """);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "key.duplicate");
    }

    [Fact]
    public void UnsupportedDirective_IsRejected()
    {
        var result = _parser.Parse("""
            [Interface]
            Address = 10.0.0.2/32
            PrivateKey = private
            SaveConfig = true
            [Peer]
            PublicKey = public
            AllowedIPs = 10.0.0.0/8
            """);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "key.unsupported");
    }

    [Fact]
    public void MissingPeerRouting_IsRejected()
    {
        var result = _parser.Parse("""
            [Interface]
            Address = 10.0.0.2/32
            PrivateKey = private
            [Peer]
            PublicKey = public
            """);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "peer.allowed_ips.required");
    }
}

