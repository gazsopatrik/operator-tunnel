using OperatorTunnel.Audit;

namespace OperatorTunnel.Core.Tests.Audit;

public sealed class AuditObservationStoreTests
{
    [Fact]
    public async Task AddAndList_FiltersBySessionAndDeduplicatesIds()
    {
        var directory = Directory.CreateTempSubdirectory("operator-audit-observation-");
        var path = Path.Combine(directory.FullName, "observations.json");
        try
        {
            var store = new JsonAuditObservationStore(path);
            var observation = AuditObservation.Create("session-1", AuditObservationKind.Host, "10.0.0.1", "nmap", "evidence-1");
            await store.AddAsync([observation, observation]);
            await store.AddAsync([AuditObservation.Create("session-2", AuditObservationKind.Host, "10.0.0.2", "nmap", "evidence-2")]);

            var sessionOne = await store.ListBySessionAsync("session-1");
            var sessionTwo = await store.ListBySessionAsync("session-2");
            Assert.Single(sessionOne);
            Assert.Single(sessionTwo);
            Assert.Equal("10.0.0.1", sessionOne[0].Value);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
