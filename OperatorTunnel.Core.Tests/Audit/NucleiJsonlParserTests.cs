using OperatorTunnel.Audit;

namespace OperatorTunnel.Core.Tests.Audit;

public sealed class NucleiJsonlParserTests
{
    [Fact]
    public void ParsesFindingRecordWithProvenance()
    {
        const string json = """{"host":"10.0.0.1","template-id":"exposed-panel","matched-at":"http://10.0.0.1","info":{"name":"Exposed panel","severity":"medium"}}""";

        var result = new NucleiJsonlParser().Parse(json, "session-1", "evidence-1", DateTimeOffset.UnixEpoch);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Observations.Count);
        Assert.All(result.Observations, observation =>
        {
            Assert.Equal("session-1", observation.SessionId);
            Assert.Equal("evidence-1", observation.RawEvidenceId);
            Assert.Equal(DateTimeOffset.UnixEpoch, observation.ObservedAt);
        });
    }

    [Fact]
    public void KeepsValidRecordsWhenOneLineIsMalformed()
    {
        const string json = """
            {"host":"10.0.0.1","template-id":"one","info":{"severity":"low"}}
            not-json
            """;

        var result = new NucleiJsonlParser().Parse(json, "session-1", "evidence-1");

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Observations.Count);
        Assert.Single(result.Issues);
    }
}
