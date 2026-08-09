using OperatorTunnel.Core.Diagnostics;

namespace OperatorTunnel.Core.Tests.Diagnostics;

public sealed class SecurityEventLogTests
{
    [Fact]
    public void SecretAssignments_AreRedacted()
    {
        var log = new SecurityEventLog();

        log.Add(EventSeverity.Info, "profile.imported", "PrivateKey = secret-value PresharedKey=another-secret");

        var entry = Assert.Single(log.Snapshot());
        Assert.DoesNotContain("secret-value", entry.Message);
        Assert.DoesNotContain("another-secret", entry.Message);
        Assert.Contains("[REDACTED]", entry.Message);
    }

    [Fact]
    public void CapacityKeepsOnlyNewestEvents()
    {
        var log = new SecurityEventLog(capacity: 2);

        log.Add(EventSeverity.Info, "one", "first");
        log.Add(EventSeverity.Info, "two", "second");
        log.Add(EventSeverity.Warning, "three", "third");

        var entries = log.Snapshot();
        Assert.Equal(2, entries.Count);
        Assert.Equal("two", entries[0].Code);
        Assert.Equal("three", entries[1].Code);
    }
}

