using OperatorTunnel.Core.Backend;

namespace OperatorTunnel.Core.Tests.Backend;

public sealed class WireGuardServiceCommandsTests
{
    [Fact]
    public void StartCommandUsesArgumentListAndOfficialServicePrefix()
    {
        var command = WireGuardServiceCommands.StartTunnel("demo-profile");

        Assert.Equal("sc.exe", command.FileName);
        Assert.Equal(["start", "WireGuardTunnel$demo-profile"], command.Arguments);
    }

    [Fact]
    public void InstallCommandKeepsConfigPathAsOneArgument()
    {
        var command = WireGuardServiceCommands.InstallTunnel(
            "C:\\Program Files\\WireGuard\\wireguard.exe",
            "C:\\Users\\Test User\\profile.conf");

        Assert.Equal("C:\\Program Files\\WireGuard\\wireguard.exe", command.FileName);
        Assert.Equal(["/installtunnelservice", "C:\\Users\\Test User\\profile.conf"], command.Arguments);
    }

    [Fact]
    public void ShellCharactersInTunnelNameAreRejected()
    {
        Assert.Throws<ArgumentException>(() => WireGuardServiceCommands.GetServiceName("demo & whoami"));
    }

    [Fact]
    public void UninstallCommandUsesValidatedTunnelName()
    {
        var command = WireGuardServiceCommands.UninstallTunnel("wireguard.exe", "demo");

        Assert.Equal(["/uninstalltunnelservice", "demo"], command.Arguments);
    }
}

