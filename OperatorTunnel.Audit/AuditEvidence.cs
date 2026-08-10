using System.Security.Cryptography;
using System.Text;

namespace OperatorTunnel.Audit;

public sealed record AuditEvidence(
    string Id,
    string SessionId,
    string Source,
    string FileName,
    DateTimeOffset CapturedAt,
    string ContentHash,
    string Content)
{
    public static AuditEvidence Create(
        string sessionId,
        string source,
        string fileName,
        string content,
        DateTimeOffset? capturedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        return new(
            $"evidence-{hash[..16]}",
            sessionId,
            source.Trim(),
            Path.GetFileName(fileName),
            capturedAt ?? DateTimeOffset.UtcNow,
            hash,
            content);
    }
}
