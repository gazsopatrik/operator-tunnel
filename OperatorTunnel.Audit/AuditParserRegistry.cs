namespace OperatorTunnel.Audit;

public interface IAuditOutputParser
{
    string ToolName { get; }
    NmapParseResult Parse(string output, string sessionId, string evidenceId, DateTimeOffset? observedAt = null);
}

public sealed record AuditParserResult(
    bool ParserFound,
    NmapParseResult? Parsed,
    IReadOnlyList<string> Issues)
{
    public bool IsValid => ParserFound && Parsed?.IsValid == true;
}

/// <summary>
/// Routes structured tool output to modular parsers. Unknown tools are not
/// rejected by the framework; they simply remain unparsed evidence.
/// </summary>
public sealed class AuditParserRegistry
{
    private readonly IReadOnlyDictionary<string, IAuditOutputParser> _parsers;

    public AuditParserRegistry(IEnumerable<IAuditOutputParser> parsers)
    {
        ArgumentNullException.ThrowIfNull(parsers);
        var parserList = parsers.ToArray();
        if (parserList.Any(parser => string.IsNullOrWhiteSpace(parser.ToolName)))
            throw new ArgumentException("Every parser must declare a tool name.", nameof(parsers));
        if (parserList.Select(parser => parser.ToolName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != parserList.Length)
            throw new ArgumentException("Parser tool names must be unique.", nameof(parsers));

        _parsers = parserList.ToDictionary(parser => parser.ToolName, StringComparer.OrdinalIgnoreCase);
    }

    public static AuditParserRegistry CreateDefault() => new([new NmapXmlParser()]);

    public AuditParserResult Parse(
        string toolName,
        string output,
        string sessionId,
        string evidenceId,
        DateTimeOffset? observedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(output);

        if (!_parsers.TryGetValue(toolName, out var parser))
            return new(false, null, [$"No parser is registered for tool '{toolName}'."]);

        var parsed = parser.Parse(output, sessionId, evidenceId, observedAt);
        return new(true, parsed, parsed.Issues);
    }
}
