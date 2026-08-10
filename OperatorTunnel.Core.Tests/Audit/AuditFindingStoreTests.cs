using OperatorTunnel.Audit;

namespace OperatorTunnel.Core.Tests.Audit;

public sealed class AuditFindingStoreTests
{
    [Fact]
    public async Task SaveAndList_RoundTripsFindingBySession()
    {
        var directory = Directory.CreateTempSubdirectory("operator-audit-finding-");
        var path = Path.Combine(directory.FullName, "findings.json");
        try
        {
            var store = new JsonAuditFindingStore(path);
            var finding = AuditFinding.CreatePotentialExposure(
                "session-1", "Potential SSH exposure", FindingSeverity.High,
                "10.0.0.1:tcp/22", "Needs verification.", ["evidence-1"]);
            await store.SaveAsync(finding);

            var restored = Assert.Single(await store.ListBySessionAsync("session-1"));
            Assert.Equal(finding.Id, restored.Id);
            Assert.Equal(FindingStatus.PotentialExposure, restored.Status);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
