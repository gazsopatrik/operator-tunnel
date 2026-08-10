namespace OperatorTunnel.Audit;

public sealed record AuditProject(
    string Id,
    string Name,
    string Scope,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<string> TargetIds)
{
    public static AuditProject Create(string name, string scope, DateTimeOffset? now = null)
    {
        ValidateText(name, nameof(name));
        ValidateText(scope, nameof(scope));
        var timestamp = now ?? DateTimeOffset.UtcNow;
        return new(Guid.NewGuid().ToString("N"), name.Trim(), scope.Trim(), timestamp, timestamp, []);
    }

    private static void ValidateText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 200)
            throw new ArgumentException("Value exceeds the maximum length.", parameterName);
    }
}
