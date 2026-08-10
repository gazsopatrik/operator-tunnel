using System.Text.Json;

namespace OperatorTunnel.Audit;

public interface IAuditObservationStore
{
    Task<IReadOnlyList<AuditObservation>> ListBySessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task AddAsync(IReadOnlyList<AuditObservation> observations, CancellationToken cancellationToken = default);
}

public sealed class JsonAuditObservationStore : IAuditObservationStore
{
    private const string FileHeader = "OPERATOR-AUDIT-OBSERVATIONS-V1\0";
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public JsonAuditObservationStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<IReadOnlyList<AuditObservation>> ListBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadAsync(cancellationToken))
                .Where(item => item.SessionId == sessionId)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddAsync(
        IReadOnlyList<AuditObservation> observations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (observations.Count == 0)
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var all = (await ReadAsync(cancellationToken)).ToList();
            var knownIds = all.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            all.AddRange(observations.Where(item => knownIds.Add(item.Id)));
            await WriteAsync(all, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<AuditObservation>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
            return [];

        await using var stream = File.OpenRead(_filePath);
        using var reader = new StreamReader(stream);
        if (!string.Equals(await reader.ReadLineAsync(cancellationToken), FileHeader, StringComparison.Ordinal))
            throw new InvalidDataException("Audit observation store header is invalid.");

        return JsonSerializer.Deserialize<List<AuditObservation>>(
            await reader.ReadToEndAsync(cancellationToken),
            _jsonOptions) ?? [];
    }

    private async Task WriteAsync(IReadOnlyList<AuditObservation> observations, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("Audit observation store directory is unavailable.");
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            await using (var writer = new StreamWriter(stream))
            {
                await writer.WriteLineAsync(FileHeader);
                await writer.FlushAsync(cancellationToken);
                await JsonSerializer.SerializeAsync(stream, observations, _jsonOptions, cancellationToken);
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
