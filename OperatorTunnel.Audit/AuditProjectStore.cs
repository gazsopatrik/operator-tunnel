using System.Text.Json;

namespace OperatorTunnel.Audit;

public interface IAuditProjectStore
{
    Task<IReadOnlyList<AuditProject>> ListAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AuditProject project, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores non-secret audit metadata as versioned JSON. Evidence and credentials
/// must use dedicated stores; this class intentionally stores project metadata only.
/// </summary>
public sealed class JsonAuditProjectStore : IAuditProjectStore
{
    private const string FileHeader = "OPERATOR-AUDIT-PROJECTS-V1\0";
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JsonAuditProjectStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<IReadOnlyList<AuditProject>> ListAsync(CancellationToken cancellationToken = default)
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

    public async Task SaveAsync(AuditProject project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.Id);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var projects = (await ReadAsync(cancellationToken)).ToList();
            var existingIndex = projects.FindIndex(item => string.Equals(item.Id, project.Id, StringComparison.Ordinal));
            if (existingIndex >= 0)
                projects[existingIndex] = project with { UpdatedAt = DateTimeOffset.UtcNow };
            else
                projects.Add(project);

            await WriteAsync(projects, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var projects = (await ReadAsync(cancellationToken))
                .Where(item => !string.Equals(item.Id, projectId, StringComparison.Ordinal))
                .ToList();
            await WriteAsync(projects, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<AuditProject>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
            return [];

        await using var stream = File.OpenRead(_filePath);
        using var reader = new StreamReader(stream);
        var header = await reader.ReadLineAsync(cancellationToken);
        if (!string.Equals(header, FileHeader, StringComparison.Ordinal))
            throw new InvalidDataException("Audit project store header is invalid.");

        var json = await reader.ReadToEndAsync(cancellationToken);
        return JsonSerializer.Deserialize<List<AuditProject>>(json, _jsonOptions) ?? [];
    }

    private async Task WriteAsync(IReadOnlyList<AuditProject> projects, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("Audit project store directory is unavailable.");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            await using (var writer = new StreamWriter(stream))
            {
                await writer.WriteLineAsync(FileHeader);
                await writer.FlushAsync(cancellationToken);
                await JsonSerializer.SerializeAsync(stream, projects, _jsonOptions, cancellationToken);
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
