namespace OperatorTunnel.Audit;

public enum FindingSeverity
{
    Informational,
    Low,
    Medium,
    High,
    Critical
}

public enum FindingStatus
{
    PotentialExposure,
    VerificationRequired,
    Verified,
    NotAffected,
    FalsePositive
}

public sealed record AuditFinding(
    string Id,
    string SessionId,
    string Title,
    FindingSeverity Severity,
    FindingStatus Status,
    string AffectedAsset,
    string Description,
    IReadOnlyList<string> EvidenceIds,
    string? RelatedCve,
    string? Remediation,
    string? VerificationNotes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static AuditFinding CreatePotentialExposure(
        string sessionId,
        string title,
        FindingSeverity severity,
        string affectedAsset,
        string description,
        IReadOnlyList<string> evidenceIds,
        string? relatedCve = null,
        string? remediation = null,
        DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(affectedAsset);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(evidenceIds);
        if (evidenceIds.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Evidence IDs must not be blank.", nameof(evidenceIds));

        var timestamp = now ?? DateTimeOffset.UtcNow;
        return new(
            Guid.NewGuid().ToString("N"),
            sessionId,
            title.Trim(),
            severity,
            FindingStatus.PotentialExposure,
            affectedAsset.Trim(),
            description.Trim(),
            evidenceIds.ToArray(),
            relatedCve?.Trim(),
            remediation?.Trim(),
            null,
            timestamp,
            timestamp);
    }

    public AuditFinding RequireVerification(DateTimeOffset? now = null)
    {
        if (Status != FindingStatus.PotentialExposure)
            throw new InvalidOperationException("Only a potential exposure can enter verification.");
        return this with { Status = FindingStatus.VerificationRequired, UpdatedAt = now ?? DateTimeOffset.UtcNow };
    }

    public AuditFinding Verify(string notes, DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notes);
        if (Status != FindingStatus.VerificationRequired)
            throw new InvalidOperationException("Only a finding awaiting verification can be verified.");
        return this with
        {
            Status = FindingStatus.Verified,
            VerificationNotes = notes.Trim(),
            UpdatedAt = now ?? DateTimeOffset.UtcNow
        };
    }

    public AuditFinding MarkNotAffected(string notes, DateTimeOffset? now = null) =>
        CompleteVerification(FindingStatus.NotAffected, notes, now);

    public AuditFinding MarkFalsePositive(string notes, DateTimeOffset? now = null) =>
        CompleteVerification(FindingStatus.FalsePositive, notes, now);

    private AuditFinding CompleteVerification(FindingStatus status, string notes, DateTimeOffset? now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notes);
        if (Status != FindingStatus.VerificationRequired)
            throw new InvalidOperationException("Only a finding awaiting verification can be resolved.");
        return this with
        {
            Status = status,
            VerificationNotes = notes.Trim(),
            UpdatedAt = now ?? DateTimeOffset.UtcNow
        };
    }
}
