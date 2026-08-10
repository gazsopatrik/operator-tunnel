using OperatorTunnel.Audit;

namespace OperatorTunnel.Core.Tests.Audit;

public sealed class AuditInventoryTests
{
    [Fact]
    public void SnapshotCountsDistinctInventoryValues()
    {
        var observations = new[]
        {
            AuditObservation.Create("session-1", AuditObservationKind.Host, "10.0.0.2", "nmap", "e1"),
            AuditObservation.Create("session-1", AuditObservationKind.Host, "10.0.0.2", "nmap", "e2"),
            AuditObservation.Create("session-1", AuditObservationKind.Port, "tcp/22", "nmap", "e1"),
            AuditObservation.Create("session-1", AuditObservationKind.Port, "tcp/22", "nmap", "e2"),
            AuditObservation.Create("session-1", AuditObservationKind.Service, "OpenSSH 9.3p1", "nmap", "e1")
        };

        var snapshot = AuditInventorySnapshot.FromObservations(observations);

        Assert.Equal(1, snapshot.HostCount);
        Assert.Equal(1, snapshot.PortCount);
        Assert.Equal(1, snapshot.ServiceCount);
        Assert.Single(snapshot.Hosts);
        Assert.Single(snapshot.Ports);
    }

    [Fact]
    public async Task BuilderReadsOnlyTheRequestedSession()
    {
        var directory = Directory.CreateTempSubdirectory("operator-audit-inventory-");
        var path = Path.Combine(directory.FullName, "observations.json");
        try
        {
            var store = new JsonAuditObservationStore(path);
            await store.AddAsync([
                AuditObservation.Create("session-1", AuditObservationKind.Host, "10.0.0.1", "nmap", "e1"),
                AuditObservation.Create("session-2", AuditObservationKind.Host, "10.0.0.2", "nmap", "e2")
            ]);

            var snapshot = await new AuditInventoryBuilder(store).BuildAsync("session-1");

            Assert.Equal(1, snapshot.HostCount);
            Assert.Equal(["10.0.0.1"], snapshot.Hosts);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
