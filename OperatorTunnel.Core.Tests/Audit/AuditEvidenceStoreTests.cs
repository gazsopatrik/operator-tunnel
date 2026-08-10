using OperatorTunnel.Audit;

namespace OperatorTunnel.Core.Tests.Audit;

public sealed class AuditEvidenceStoreTests
{
    [Fact]
    public async Task EvidenceRoundTrip_PreservesContentAndHash()
    {
        var directory = Directory.CreateTempSubdirectory("operator-audit-evidence-");
        var path = Path.Combine(directory.FullName, "evidence.json");
        try
        {
            var store = new JsonAuditEvidenceStore(path);
            var evidence = AuditEvidence.Create("session-1", "nmap", @"C:\scans\result.xml", "<nmaprun />");
            await store.SaveAsync(evidence);

            var restored = Assert.Single(await store.ListBySessionAsync("session-1"));
            Assert.Equal("result.xml", restored.FileName);
            Assert.Equal("<nmaprun />", restored.Content);
            Assert.Equal(evidence.ContentHash, restored.ContentHash);
            Assert.StartsWith("evidence-", restored.Id);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void EvidenceIdIsStableForSameContent()
    {
        var first = AuditEvidence.Create("session-1", "nmap", "one.xml", "same");
        var second = AuditEvidence.Create("session-1", "nmap", "two.xml", "same");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.ContentHash, second.ContentHash);
    }
}
