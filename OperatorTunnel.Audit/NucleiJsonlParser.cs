using System.Text.Json;

namespace OperatorTunnel.Audit;

public sealed class NucleiJsonlParser : IAuditOutputParser
{
    public string ToolName => "nuclei-jsonl";

    public AuditParseResult Parse(
        string output,
        string sessionId,
        string evidenceId,
        DateTimeOffset? observedAt = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceId);

        var observations = new List<AuditObservation>();
        var issues = new List<string>();
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var host = ReadString(root, "host");
                var templateId = ReadString(root, "template-id");
                var matchedAt = ReadString(root, "matched-at") ?? host;
                var severity = ReadNestedString(root, "info", "severity") ?? "unknown";
                var name = ReadNestedString(root, "info", "name") ?? templateId ?? "unnamed template";

                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(templateId))
                {
                    issues.Add("Nuclei record is missing host or template-id.");
                    continue;
                }

                observations.Add(AuditObservation.Create(sessionId, AuditObservationKind.Host, host, "nuclei", evidenceId, observedAt));
                observations.Add(AuditObservation.Create(
                    sessionId,
                    AuditObservationKind.Note,
                    $"Nuclei {severity} // {templateId} // {name} // {matchedAt}",
                    "nuclei",
                    evidenceId,
                    observedAt));
            }
            catch (JsonException)
            {
                issues.Add("Malformed Nuclei JSONL record.");
            }
        }

        return new(observations, issues);
    }

    private static string? ReadString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadNestedString(JsonElement root, string parentName, string propertyName) =>
        root.TryGetProperty(parentName, out var parent) && parent.ValueKind == JsonValueKind.Object
            ? ReadString(parent, propertyName)
            : null;
}
