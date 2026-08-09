using OperatorTunnel.Core.Backend;

namespace OperatorTunnel.Core.Tests.Backend;

public sealed class WireGuardStatisticsParserTests
{
    [Fact]
    public void ValidOutput_IsMergedByPeerKey()
    {
        const string handshakes = "peer-a\t1700000000\npeer-b\t0\n";
        const string transfer = "peer-a\t1024\t2048\npeer-b\t10\t20\n";

        var result = new WireGuardStatisticsParser().Parse(handshakes, transfer);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Peers.Count);
        Assert.Equal(1024UL, result.Peers[0].ReceiveBytes);
        Assert.Equal(2048UL, result.Peers[0].TransmitBytes);
        Assert.Null(result.Peers[1].LatestHandshake);
    }

    [Fact]
    public void MalformedTransfer_IsRejected()
    {
        var result = new WireGuardStatisticsParser().Parse("peer-a\t1700000000", "peer-a\tnot-a-number\t20");

        Assert.False(result.IsValid);
        Assert.Contains("Malformed transfer output.", result.Issues);
    }

    [Fact]
    public void QueryCommandsUseWgShowFormat()
    {
        var handshakes = WireGuardServiceCommands.QueryLatestHandshakes("wg.exe", "demo");
        var transfer = WireGuardServiceCommands.QueryTransfer("wg.exe", "demo");

        Assert.Equal(["show", "demo", "latest-handshakes"], handshakes.Arguments);
        Assert.Equal(["show", "demo", "transfer"], transfer.Arguments);
    }
}

