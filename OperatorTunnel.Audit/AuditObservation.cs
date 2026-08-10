namespace OperatorTunnel.Audit;

public enum AuditObservationKind
{
    Host,
    Port,
    Service,
    Technology,
    Note
}

public sealed record AuditObservation(
    string Id,
    string SessionId,
    AuditObservationKind Kind,
    string Value,
    string Source,
    DateTimeOffset ObservedAt,
    string RawEvidenceId)
{
    public static AuditObservation Create(
        string sessionId,
        AuditObservationKind kind,
        string value,
        string source,
        string rawEvidenceId,
        DateTimeOffset? observedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawEvidenceId);

        return new(
            Guid.NewGuid().ToString("N"),
            sessionId,
            kind,
            value.Trim(),
            source.Trim(),
            observedAt ?? DateTimeOffset.UtcNow,
            rawEvidenceId);
    }
}
