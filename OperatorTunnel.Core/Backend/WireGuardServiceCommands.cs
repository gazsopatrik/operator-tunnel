namespace OperatorTunnel.Core.Backend;

public sealed record ExternalProcessCommand(string FileName, IReadOnlyList<string> Arguments);

public static class WireGuardServiceCommands
{
    public static ExternalProcessCommand InstallTunnel(string wireGuardExecutable, string configPath) =>
        new(wireGuardExecutable, ["/installtunnelservice", configPath]);

    public static ExternalProcessCommand StartTunnel(string tunnelName) =>
        new("sc.exe", ["start", GetServiceName(tunnelName)]);

    public static ExternalProcessCommand StopTunnel(string tunnelName) =>
        new("sc.exe", ["stop", GetServiceName(tunnelName)]);

    public static ExternalProcessCommand QueryTunnel(string tunnelName) =>
        new("sc.exe", ["query", GetServiceName(tunnelName)]);

    public static ExternalProcessCommand UninstallTunnel(string wireGuardExecutable, string tunnelName) =>
        new(wireGuardExecutable, ["/uninstalltunnelservice", ValidateTunnelName(tunnelName)]);

    public static string GetServiceName(string tunnelName) => $"WireGuardTunnel${ValidateTunnelName(tunnelName)}";

    private static string ValidateTunnelName(string tunnelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tunnelName);

        if (tunnelName.Length > 128 || tunnelName.Any(character =>
                !(char.IsLetterOrDigit(character) || character is '.' or '-' or '_')))
        {
            throw new ArgumentException("Tunnel name contains unsupported characters.", nameof(tunnelName));
        }

        return tunnelName;
    }
}

