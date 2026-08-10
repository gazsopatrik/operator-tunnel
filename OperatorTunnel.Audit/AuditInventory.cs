namespace OperatorTunnel.Audit;

public sealed record AuditInventorySnapshot(
    int HostCount,
    int PortCount,
    int ServiceCount,
    IReadOnlyList<string> Hosts,
    IReadOnlyList<string> Ports,
    IReadOnlyList<string> Services)
{
    public static AuditInventorySnapshot FromObservations(IEnumerable<AuditObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var items = observations.ToArray();
        var hosts = DistinctValues(items, AuditObservationKind.Host);
        var ports = DistinctValues(items, AuditObservationKind.Port);
        var services = DistinctValues(items, AuditObservationKind.Service);
        return new(
            hosts.Count,
            ports.Count,
            services.Count,
            hosts,
            ports,
            services);
    }

    private static IReadOnlyList<string> DistinctValues(
        IEnumerable<AuditObservation> observations,
        AuditObservationKind kind) =>
        observations
            .Where(item => item.Kind == kind)
            .Select(item => item.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public sealed class AuditInventoryBuilder
{
    private readonly IAuditObservationStore _observationStore;

    public AuditInventoryBuilder(IAuditObservationStore observationStore)
    {
        _observationStore = observationStore ?? throw new ArgumentNullException(nameof(observationStore));
    }

    public async Task<AuditInventorySnapshot> BuildAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var observations = await _observationStore.ListBySessionAsync(sessionId, cancellationToken);
        return AuditInventorySnapshot.FromObservations(observations);
    }
}
