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
    public void DuplicateToolNamesAreRejected()
    {
        var parser = new NmapXmlParser();

        Assert.Throws<ArgumentException>(() => new AuditParserRegistry([parser, parser]));
    }
}
