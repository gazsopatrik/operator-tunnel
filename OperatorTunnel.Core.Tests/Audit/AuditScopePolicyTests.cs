using OperatorTunnel.Audit;

namespace OperatorTunnel.Core.Tests.Audit;

public sealed class AuditScopePolicyTests
{
    [Fact]
    public void AllowsIpInsideCidrAndRejectsOutside()
    {
        var allowed = AuditScopePolicy.ValidateTargets("10.10.20.0/24", ["10.10.20.15"]);
        var rejected = AuditScopePolicy.ValidateTargets("10.10.20.0/24", ["10.10.21.15"]);

        Assert.True(allowed.IsAllowed);
        Assert.False(rejected.IsAllowed);
    }

    [Fact]
    public void AllowsExactAndSubdomainHostnames()
    {
        var result = AuditScopePolicy.ValidateTargets("example.test", ["example.test", "api.example.test"]);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void ScopeValidationFailsClosedForUnsupportedTarget()
    {
        var result = AuditScopePolicy.ValidateTargets("10.0.0.0/8", ["example.test"]);

        Assert.False(result.IsAllowed);
        Assert.Contains("outside", Assert.Single(result.Issues), StringComparison.OrdinalIgnoreCase);
    }
}
