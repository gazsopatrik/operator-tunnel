using System.Text.Json;

namespace OperatorTunnel.Audit;

public interface IAuditSessionStore
{
    Task<IReadOnlyList<AuditSession>> ListAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AuditSession session, CancellationToken cancellationToken = default);
}

public sealed class JsonAuditSessionStore : IAuditSessionStore
{
    private const string FileHeader = "OPERATOR-AUDIT-SESSIONS-V1\0";
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JsonAuditSessionStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<IReadOnlyList<AuditSession>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(AuditSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(session.Id);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessions = (await ReadAsync(cancellationToken)).ToList();
            var existingIndex = sessions.FindIndex(item => item.Id == session.Id);
            if (existingIndex >= 0)
                sessions[existingIndex] = session;
            else
                sessions.Add(session);

            await WriteAsync(sessions, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<AuditSession>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
            return [];

        await using var stream = File.OpenRead(_filePath);
        using var reader = new StreamReader(stream);
        if (!string.Equals(await reader.ReadLineAsync(cancellationToken), FileHeader, StringComparison.Ordinal))
            throw new InvalidDataException("Audit session store header is invalid.");

        return JsonSerializer.Deserialize<List<AuditSession>>(
            await reader.ReadToEndAsync(cancellationToken),
            _jsonOptions) ?? [];
    }

    private async Task WriteAsync(IReadOnlyList<AuditSession> sessions, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("Audit session store directory is unavailable.");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            await using (var writer = new StreamWriter(stream))
            {
                await writer.WriteLineAsync(FileHeader);
                await writer.FlushAsync(cancellationToken);
                await JsonSerializer.SerializeAsync(stream, sessions, _jsonOptions, cancellationToken);
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
