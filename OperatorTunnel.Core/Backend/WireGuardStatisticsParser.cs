namespace OperatorTunnel.Core.Backend;

public sealed record WireGuardPeerStatistics(
    string PublicKey,
    DateTimeOffset? LatestHandshake,
    ulong ReceiveBytes,
    ulong TransmitBytes);

public sealed record WireGuardStatisticsParseResult(
    IReadOnlyList<WireGuardPeerStatistics> Peers,
    IReadOnlyList<string> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public sealed class WireGuardStatisticsParser
{
    public WireGuardStatisticsParseResult Parse(string latestHandshakes, string transfer)
    {
        ArgumentNullException.ThrowIfNull(latestHandshakes);
        ArgumentNullException.ThrowIfNull(transfer);

        var issues = new List<string>();
        var handshakes = ParseHandshakeLines(latestHandshakes, issues);
        var transfers = ParseTransferLines(transfer, issues);
        var keys = handshakes.Keys.Union(transfers.Keys, StringComparer.Ordinal).ToArray();
        var peers = keys.Select(key => new WireGuardPeerStatistics(
            key,
            handshakes.TryGetValue(key, out var handshake) && handshake > 0
                ? DateTimeOffset.FromUnixTimeSeconds(handshake)
                : null,
            transfers.TryGetValue(key, out var bytes) ? bytes.Receive : 0,
            transfers.TryGetValue(key, out bytes) ? bytes.Transmit : 0)).ToArray();

        return new WireGuardStatisticsParseResult(peers, issues);
    }

    private static Dictionary<string, long> ParseHandshakeLines(string text, List<string> issues)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var line in Lines(text))
        {
            var fields = SplitFields(line);
            if (fields.Length != 2 || !long.TryParse(fields[1], out var timestamp) || timestamp < 0)
            {
                issues.Add("Malformed latest-handshakes output.");
                continue;
            }

            if (!result.TryAdd(fields[0], timestamp))
                issues.Add("Duplicate peer in latest-handshakes output.");
        }

        return result;
    }

    private static Dictionary<string, (ulong Receive, ulong Transmit)> ParseTransferLines(string text, List<string> issues)
    {
        var result = new Dictionary<string, (ulong Receive, ulong Transmit)>(StringComparer.Ordinal);
        foreach (var line in Lines(text))
        {
            var fields = SplitFields(line);
            if (fields.Length != 3 || !ulong.TryParse(fields[1], out var receive) || !ulong.TryParse(fields[2], out var transmit))
            {
                issues.Add("Malformed transfer output.");
                continue;
            }

            if (!result.TryAdd(fields[0], (receive, transmit)))
                issues.Add("Duplicate peer in transfer output.");
        }

        return result;
    }

    private static IEnumerable<string> Lines(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string[] SplitFields(string line) =>
        line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

