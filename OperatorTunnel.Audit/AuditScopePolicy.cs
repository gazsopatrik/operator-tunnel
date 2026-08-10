using System.Net;

namespace OperatorTunnel.Audit;

public sealed record AuditScopeValidationResult(bool IsAllowed, IReadOnlyList<string> Issues);

public static class AuditScopePolicy
{
    public static AuditScopeValidationResult ValidateTargets(string scope, IReadOnlyList<string> targets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(targets);

        var scopeEntries = scope
            .Split([',', ';', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (scopeEntries.Length == 0)
            return new(false, ["Audit scope is empty."]);

        var issues = new List<string>();
        foreach (var target in targets)
        {
            if (!scopeEntries.Any(entry => IsTargetWithinEntry(entry, target)))
                issues.Add($"Target '{target}' is outside the audit project scope.");
        }

        return new(issues.Count == 0, issues);
    }

    private static bool IsTargetWithinEntry(string scopeEntry, string target)
    {
        if (string.Equals(scopeEntry, target, StringComparison.OrdinalIgnoreCase))
            return true;

        if (IPAddress.TryParse(target, out var targetIp))
        {
            if (TryParseCidr(scopeEntry, out var network, out var prefixLength))
                return IsInNetwork(targetIp, network, prefixLength);
            return false;
        }

        return target.EndsWith($".{scopeEntry}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseCidr(string value, out IPAddress network, out int prefixLength)
    {
        network = IPAddress.None;
        prefixLength = 0;
        var parts = value.Split('/', 2);
        if (parts.Length != 2 ||
            !IPAddress.TryParse(parts[0], out var parsedNetwork) ||
            !int.TryParse(parts[1], out prefixLength))
            return false;

        network = parsedNetwork;
        var maxPrefix = network.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        return prefixLength >= 0 && prefixLength <= maxPrefix;
    }

    private static bool IsInNetwork(IPAddress address, IPAddress network, int prefixLength)
    {
        var addressBytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        if (addressBytes.Length != networkBytes.Length)
            return false;

        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;
        for (var index = 0; index < fullBytes; index++)
        {
            if (addressBytes[index] != networkBytes[index])
                return false;
        }

        return remainingBits == 0 ||
            (addressBytes[fullBytes] & (0xFF << (8 - remainingBits))) ==
            (networkBytes[fullBytes] & (0xFF << (8 - remainingBits)));
    }
}
