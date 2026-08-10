using OperatorTunnel.Audit;

namespace OperatorTunnel.Core.Tests.Audit;

public sealed class AuditCoreTests
{
    [Fact]
    public void ProjectCreation_TrimsInputAndCreatesStableIdentity()
    {
        var project = AuditProject.Create("  Internal Review  ", "  10.10.20.0/24  ", DateTimeOffset.UnixEpoch);

        Assert.False(string.IsNullOrWhiteSpace(project.Id));
        Assert.Equal("Internal Review", project.Name);
        Assert.Equal("10.10.20.0/24", project.Scope);
        Assert.Equal(DateTimeOffset.UnixEpoch, project.CreatedAt);
        Assert.Equal([], project.TargetIds);
    }

    [Fact]
    public void SessionCanRecordTheVpnContextWithoutOwningVpnSecrets()
    {
        var session = AuditSession.Start("project-1", "assessment-profile", DateTimeOffset.UnixEpoch);

        Assert.Equal("project-1", session.ProjectId);
        Assert.Equal("assessment-profile", session.VpnProfileName);
        Assert.Equal(AuditSessionStatus.Active, session.Status);
        Assert.Null(session.EndedAt);
    }

    [Fact]
    public void ObservationRequiresProvenance()
    {
        var observation = AuditObservation.Create(
            "session-1",
            AuditObservationKind.Service,
            "OpenSSH 9.3p1",
            "nmap",
            "evidence-42",
            DateTimeOffset.UnixEpoch);

        Assert.Equal("session-1", observation.SessionId);
        Assert.Equal("nmap", observation.Source);
        Assert.Equal("evidence-42", observation.RawEvidenceId);
        Assert.Equal(DateTimeOffset.UnixEpoch, observation.ObservedAt);
    }

    [Fact]
    public void ProjectCreationRejectsMissingScope()
    {
        Assert.Throws<ArgumentException>(() => AuditProject.Create("Review", " "));
    }
}
