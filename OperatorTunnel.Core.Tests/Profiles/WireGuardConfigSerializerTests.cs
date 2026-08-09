using OperatorTunnel.Core.Profiles;

namespace OperatorTunnel.Core.Tests.Profiles;

public sealed class WireGuardConfigSerializerTests
{
    [Fact]
    public void ValidProfile_IsSerializedDeterministically()
    {
        var profile = new WireGuardProfile(
            "demo",
            "10.0.0.2/32",
            "private",
            ["10.0.0.1", "1.1.1.1"],
            [new WireGuardPeer("public", "vpn.example.test:51820", ["0.0.0.0/0", "::/0"], 25, "psk")]);

        var text = new WireGuardConfigSerializer().Serialize(profile);

        Assert.Contains("[Interface]", text);
        Assert.Contains("PrivateKey = private", text);
        Assert.Contains("AllowedIPs = 0.0.0.0/0, ::/0", text);
        Assert.Contains("PersistentKeepalive = 25", text);
        Assert.Equal(text, new WireGuardConfigSerializer().Serialize(profile));
    }

    [Fact]
    public void InvalidProfile_IsNotSerialized()
    {
        var profile = new WireGuardProfile("", "", "", [], []);

        Assert.Throws<ArgumentException>(() => new WireGuardConfigSerializer().Serialize(profile));
    }
}

