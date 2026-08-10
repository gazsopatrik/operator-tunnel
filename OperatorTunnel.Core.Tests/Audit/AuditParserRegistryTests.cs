using OperatorTunnel.Audit;

namespace OperatorTunnel.Core.Tests.Audit;

public sealed class AuditParserRegistryTests
{
    [Fact]
    public void RoutesNmapOutputToTheRegisteredParser()
    {
        var xml = """
            <!DOCTYPE nmaprun>
            <nmaprun><host><address addr="10.0.0.1" /></host></nmaprun>
            """;

        var result = AuditParserRegistry.CreateDefault().Parse("nmap-xml", xml, "session-1", "evidence-1");

        Assert.True(result.IsValid);
        Assert.Equal(AuditObservationKind.Host, Assert.Single(result.Parsed!.Observations).Kind);
    }

    [Fact]
    public void UnknownToolsRemainUnparsedInsteadOfBeingRejected()
    {
        var result = AuditParserRegistry.CreateDefault().Parse("future-tool", "raw output", "session-1", "evidence-1");

        Assert.False(result.ParserFound);
        Assert.False(result.IsValid);
        Assert.Contains("future-tool", Assert.Single(result.Issues));
    }

    [Fact]
    public void DefaultRegistryIncludesNucleiJsonl()
    {
        const string json = """{"host":"https://10.0.0.1","template-id":"ssl-expired","matched-at":"https://10.0.0.1","info":{"name":"SSL expired","severity":"high"}}""";

        var result = AuditParserRegistry.CreateDefault().Parse("nuclei-jsonl", json, "session-1", "evidence-1");

        Assert.True(result.IsValid);
        Assert.Contains(result.Parsed!.Observations, item => item.Kind == AuditObservationKind.Note && item.Value.Contains("ssl-expired"));
    }

    [Fact]
    public void DuplicateToolNamesAreRejected()
    {
        var parser = new NmapXmlParser();

        Assert.Throws<ArgumentException>(() => new AuditParserRegistry([parser, parser]));
    }
}
