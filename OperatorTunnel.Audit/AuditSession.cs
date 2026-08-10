namespace OperatorTunnel.Audit;

public enum AuditSessionStatus
{
    Active,
    Completed,
    Cancelled
}

public sealed record AuditSession(
    string Id,
    string ProjectId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    AuditSessionStatus Status,
    string? VpnProfileName)
{
    public static AuditSession Start(string projectId, string? vpnProfileName = null, DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        if (vpnProfileName?.Length > 128)
            throw new ArgumentException("VPN profile name exceeds the maximum length.", nameof(vpnProfileName));

        return new(
            Guid.NewGuid().ToString("N"),
            projectId,
            now ?? DateTimeOffset.UtcNow,
            null,
            AuditSessionStatus.Active,
            vpnProfileName);
    }
}
