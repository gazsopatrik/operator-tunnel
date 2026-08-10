using OperatorTunnel.Audit;

namespace OperatorTunnel.Core.Tests.Audit;

public sealed class AuditSessionStoreTests
{
    [Fact]
    public async Task SessionLifecycle_PersistsActiveAndCompletedStates()
    {
        var directory = Directory.CreateTempSubdirectory("operator-audit-session-");
        var path = Path.Combine(directory.FullName, "sessions.json");
        try
        {
            var store = new JsonAuditSessionStore(path);
            var started = AuditSession.Start("project-1", "assessment-profile", DateTimeOffset.UnixEpoch);
            await store.SaveAsync(started);
            await store.SaveAsync(started.Complete(DateTimeOffset.UnixEpoch.AddHours(1)));

            var restored = Assert.Single(await store.ListAsync());
            Assert.Equal(AuditSessionStatus.Completed, restored.Status);
            Assert.Equal(DateTimeOffset.UnixEpoch.AddHours(1), restored.EndedAt);
            Assert.Equal("assessment-profile", restored.VpnProfileName);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void CompleteRejectsAnEarlierEndTime()
    {
        var session = AuditSession.Start("project-1", now: DateTimeOffset.UnixEpoch.AddHours(1));

        Assert.Throws<ArgumentOutOfRangeException>(() => session.Complete(DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public async Task InvalidHeader_FailsClosed()
    {
        var directory = Directory.CreateTempSubdirectory("operator-audit-session-");
        var path = Path.Combine(directory.FullName, "sessions.json");
        try
        {
            await File.WriteAllTextAsync(path, "invalid\n[]");

            await Assert.ThrowsAsync<InvalidDataException>(() => new JsonAuditSessionStore(path).ListAsync());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
