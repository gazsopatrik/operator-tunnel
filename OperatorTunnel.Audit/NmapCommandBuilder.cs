using System.Text.RegularExpressions;

namespace OperatorTunnel.Audit;

public sealed record AuditExternalCommand(string FileName, IReadOnlyList<string> Arguments);

public static partial class NmapCommandBuilder
{
    public static AuditExternalCommand Build(
        IReadOnlyList<string> targets,
        string nmapExecutable = "nmap.exe")
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentException.ThrowIfNullOrWhiteSpace(nmapExecutable);
        if (targets.Count == 0)
            throw new ArgumentException("At least one target is required.", nameof(targets));
        if (targets.Count > 64)
            throw new ArgumentException("Too many targets were supplied.", nameof(targets));

        foreach (var target in targets)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(target);
            if (target.Length > 255 || target.StartsWith('-') || !TargetRegex().IsMatch(target))
                throw new ArgumentException("Target contains unsupported characters or options.", nameof(targets));
        }

        return new(nmapExecutable, ["-oX", "-", "--", .. targets]);
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9.:\-_/]*$")]
    private static partial Regex TargetRegex();
}
