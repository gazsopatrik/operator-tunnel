using System.Text.RegularExpressions;

namespace OperatorTunnel.Core.Backend;

public enum WireGuardServiceStatus
{
    Unknown,
    Stopped,
    StartPending,
    Running,
    StopPending
}

public sealed record WireGuardServiceStatusResult(
    bool Succeeded,
    WireGuardServiceStatus Status,
    string? Error = null);

public static partial class WireGuardServiceStatusParser
{
    public static bool TryParse(string output, out WireGuardServiceStatus status)
    {
        ArgumentNullException.ThrowIfNull(output);
        status = WireGuardServiceStatus.Unknown;

        var match = StateLineRegex().Match(output);
        if (!match.Success || !int.TryParse(match.Groups["code"].Value, out var code))
            return false;

        status = code switch
        {
            1 => WireGuardServiceStatus.Stopped,
            2 => WireGuardServiceStatus.StartPending,
            3 => WireGuardServiceStatus.StopPending,
            4 => WireGuardServiceStatus.Running,
            _ => WireGuardServiceStatus.Unknown
        };

        return status != WireGuardServiceStatus.Unknown;
    }

    [GeneratedRegex(@"STATE\s*:\s*(?<code>\d+)\s+(?<name>[A-Z_]+)", RegexOptions.IgnoreCase)]
    private static partial Regex StateLineRegex();
}
