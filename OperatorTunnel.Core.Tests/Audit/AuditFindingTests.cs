using OperatorTunnel.Audit;

namespace OperatorTunnel.Core.Tests.Audit;

public sealed class AuditFindingTests
{
    [Fact]
    public void FindingRequiresExplicitVerificationBeforeItCanBeVerified()
    {
        var finding = AuditFinding.CreatePotentialExposure(
            "session-1",
            "Potential OpenSSH exposure",
            FindingSeverity.High,
            "10.10.20.15:tcp/22",
            "Detected product/version may match a known advisory.",
            ["evidence-1"],
            "CVE-2026-0001",
            "Upgrade OpenSSH",
            DateTimeOffset.UnixEpoch);

        Assert.Equal(FindingStatus.PotentialExposure, finding.Status);
        Assert.Throws<InvalidOperationException>(() => finding.Verify("verified", DateTimeOffset.UnixEpoch));

        var verified = finding
            .RequireVerification(DateTimeOffset.UnixEpoch.AddMinutes(1))
            .Verify("Confirmed affected version on host.", DateTimeOffset.UnixEpoch.AddMinutes(2));

        Assert.Equal(FindingStatus.Verified, verified.Status);
        Assert.Equal("Confirmed affected version on host.", verified.VerificationNotes);
        Assert.Equal("CVE-2026-0001", verified.RelatedCve);
    }

    [Theory]
    [InlineData(FindingStatus.NotAffected)]
    [InlineData(FindingStatus.FalsePositive)]
    public void VerificationCanResolveWithoutClaimingVulnerability(FindingStatus expectedStatus)
    {
        var finding = AuditFinding.CreatePotentialExposure(
            "session-1",
            "Potential exposure",
            FindingSeverity.Medium,
            "host-1",
            "Needs review.",
            ["evidence-1"])
            .RequireVerification();

        var resolved = expectedStatus == FindingStatus.NotAffected
            ? finding.MarkNotAffected("Vendor backport is installed.")
            : finding.MarkFalsePositive("Product identification was incorrect.");

        Assert.Equal(expectedStatus, resolved.Status);
        Assert.NotNull(resolved.VerificationNotes);
    }

    [Fact]
    public void EvidenceIdsAreRequired()
    {
        Assert.Throws<ArgumentException>(() => AuditFinding.CreatePotentialExposure(
            "session-1",
            "Potential exposure",
            FindingSeverity.Low,
            "host-1",
            "Needs review.",
            [" "]));
    }
}
