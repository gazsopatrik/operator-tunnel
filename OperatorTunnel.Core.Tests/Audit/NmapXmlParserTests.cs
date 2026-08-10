using OperatorTunnel.Audit;

namespace OperatorTunnel.Core.Tests.Audit;

public sealed class NmapXmlParserTests
{
    [Fact]
    public void ParsesOpenPortsAndServicesWithProvenance()
    {
        const string xml = """
            <?xml version="1.0"?>
            <nmaprun>
              <host>
                <address addr="10.10.20.15" addrtype="ipv4"/>
                <ports>
                  <port protocol="tcp" portid="22">
                    <state state="open"/>
                    <service name="ssh" product="OpenSSH" version="9.3p1"/>
                  </port>
                  <port protocol="tcp" portid="80">
                    <state state="closed"/>
                    <service name="http" product="nginx" version="1.24"/>
                  </port>
                </ports>
              </host>
            </nmaprun>
            """;

        var result = new NmapXmlParser().Parse(xml, "session-1", "evidence-1", DateTimeOffset.UnixEpoch);

        Assert.True(result.IsValid);
        Assert.Equal(3, result.Observations.Count);
        Assert.Contains(result.Observations, item => item.Kind == AuditObservationKind.Host && item.Value == "10.10.20.15");
        Assert.Contains(result.Observations, item => item.Kind == AuditObservationKind.Port && item.Value == "tcp/22");
        Assert.Contains(result.Observations, item => item.Kind == AuditObservationKind.Service && item.Value.Contains("OpenSSH 9.3p1"));
        Assert.All(result.Observations, item =>
        {
            Assert.Equal("session-1", item.SessionId);
            Assert.Equal("evidence-1", item.RawEvidenceId);
            Assert.Equal(DateTimeOffset.UnixEpoch, item.ObservedAt);
        });
    }

    [Fact]
    public void RejectsDtdAndExternalEntityInput()
    {
        const string xml = """
            <!DOCTYPE nmaprun [<!ENTITY secret SYSTEM "file:///Windows/win.ini">]>
            <nmaprun><host><address addr="&secret;"/></host></nmaprun>
            """;

        var result = new NmapXmlParser().Parse(xml, "session-1", "evidence-1");

        Assert.False(result.IsValid);
        Assert.Empty(result.Observations);
    }

    [Fact]
    public void ReportsHostWithoutAddress()
    {
        var result = new NmapXmlParser().Parse("<nmaprun><host /></nmaprun>", "session-1", "evidence-1");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Contains("missing an address"));
    }
}
