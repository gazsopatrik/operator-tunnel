using System.Text.Json;

namespace OperatorTunnel.Audit;

public interface IAuditEvidenceStore
{
    Task<IReadOnlyList<AuditEvidence>> ListBySessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task SaveAsync(AuditEvidence evidence, CancellationToken cancellationToken = default);
}

public sealed class JsonAuditEvidenceStore : IAuditEvidenceStore
{
    private const string FileHeader = "OPERATOR-AUDIT-EVIDENCE-V1\0";
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public JsonAuditEvidenceStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<IReadOnlyList<AuditEvidence>> ListBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
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

    public async Task SaveAsync(AuditEvidence evidence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var evidenceItems = (await ReadAsync(cancellationToken)).ToList();
            var index = evidenceItems.FindIndex(item => item.Id == evidence.Id);
            if (index >= 0)
                evidenceItems[index] = evidence;
            else
                evidenceItems.Add(evidence);
            await WriteAsync(evidenceItems, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<AuditEvidence>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
            return [];

        await using var stream = File.OpenRead(_filePath);
        using var reader = new StreamReader(stream);
        if (!string.Equals(await reader.ReadLineAsync(cancellationToken), FileHeader, StringComparison.Ordinal))
            throw new InvalidDataException("Audit evidence store header is invalid.");
        return JsonSerializer.Deserialize<List<AuditEvidence>>(
            await reader.ReadToEndAsync(cancellationToken), _jsonOptions) ?? [];
    }

    private async Task WriteAsync(IReadOnlyList<AuditEvidence> evidenceItems, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("Audit evidence store directory is unavailable.");
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            await using (var writer = new StreamWriter(stream))
            {
                await writer.WriteLineAsync(FileHeader);
                await writer.FlushAsync(cancellationToken);
                await JsonSerializer.SerializeAsync(stream, evidenceItems, _jsonOptions, cancellationToken);
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
