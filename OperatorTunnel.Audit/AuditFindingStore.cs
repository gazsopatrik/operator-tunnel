using System.Text.Json;

namespace OperatorTunnel.Audit;

public interface IAuditFindingStore
{
    Task<IReadOnlyList<AuditFinding>> ListBySessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task SaveAsync(AuditFinding finding, CancellationToken cancellationToken = default);
}

public sealed class JsonAuditFindingStore : IAuditFindingStore
{
    private const string FileHeader = "OPERATOR-AUDIT-FINDINGS-V1\0";
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public JsonAuditFindingStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<IReadOnlyList<AuditFinding>> ListBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadAsync(cancellationToken)).Where(item => item.SessionId == sessionId).ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(AuditFinding finding, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(finding);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var findings = (await ReadAsync(cancellationToken)).ToList();
            var index = findings.FindIndex(item => item.Id == finding.Id);
            if (index >= 0)
                findings[index] = finding;
            else
                findings.Add(finding);
            await WriteAsync(findings, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<AuditFinding>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
            return [];
        await using var stream = File.OpenRead(_filePath);
        using var reader = new StreamReader(stream);
        if (!string.Equals(await reader.ReadLineAsync(cancellationToken), FileHeader, StringComparison.Ordinal))
            throw new InvalidDataException("Audit finding store header is invalid.");
        return JsonSerializer.Deserialize<List<AuditFinding>>(
            await reader.ReadToEndAsync(cancellationToken), _jsonOptions) ?? [];
    }

    private async Task WriteAsync(IReadOnlyList<AuditFinding> findings, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("Audit finding store directory is unavailable.");
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            await using (var writer = new StreamWriter(stream))
            {
                await writer.WriteLineAsync(FileHeader);
                await writer.FlushAsync(cancellationToken);
                await JsonSerializer.SerializeAsync(stream, findings, _jsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
