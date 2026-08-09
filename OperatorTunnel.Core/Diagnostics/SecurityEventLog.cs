using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace OperatorTunnel.Core.Diagnostics;

public enum EventSeverity
{
    Info,
    Warning,
    Error
}

public sealed record SecurityEvent(
    DateTimeOffset Timestamp,
    EventSeverity Severity,
    string Code,
    string Message);

public sealed class SecurityEventLog
{
    private static readonly Regex SecretAssignment = new(
        @"(?<key>PrivateKey|PresharedKey)\s*=\s*[^\s,]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly int _capacity;
    private readonly List<SecurityEvent> _events = [];
    private readonly object _gate = new();

    public SecurityEventLog(int capacity = 500)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public void Add(EventSeverity severity, string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(message);

        var securityEvent = new SecurityEvent(
            DateTimeOffset.UtcNow,
            severity,
            code,
            Redact(message));

        lock (_gate)
        {
            _events.Add(securityEvent);
            if (_events.Count > _capacity)
                _events.RemoveRange(0, _events.Count - _capacity);
        }
    }

    public IReadOnlyList<SecurityEvent> Snapshot()
    {
        lock (_gate)
            return new ReadOnlyCollection<SecurityEvent>(_events.ToList());
    }

    private static string Redact(string message) =>
        SecretAssignment.Replace(message, "$key=[REDACTED]");
}

