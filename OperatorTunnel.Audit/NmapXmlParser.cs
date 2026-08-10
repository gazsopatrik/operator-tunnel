using System.Xml;
using System.Xml.Linq;

namespace OperatorTunnel.Audit;

public sealed record AuditParseResult(
    IReadOnlyList<AuditObservation> Observations,
    IReadOnlyList<string> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

/// <summary>
/// Converts structured Nmap XML into normalized observations. DTDs and external
/// entities are disabled because scan output is untrusted input.
/// </summary>
public sealed class NmapXmlParser : IAuditOutputParser
{
    public string ToolName => "nmap-xml";

    public AuditParseResult Parse(
        string xml,
        string sessionId,
        string evidenceId,
        DateTimeOffset? observedAt = null)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceId);

        var issues = new List<string>();
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                // Nmap emits a benign <!DOCTYPE nmaprun> declaration.
                // Parsing is allowed, but external resolution remains disabled.
                DtdProcessing = DtdProcessing.Parse,
                XmlResolver = null,
                MaxCharactersFromEntities = 0
            };
            using var stringReader = new StringReader(xml);
            using var reader = XmlReader.Create(stringReader, settings);
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException)
        {
            return new([], ["Nmap XML is malformed or uses a prohibited XML feature."]);
        }

        var observations = new List<AuditObservation>();
        foreach (var host in document.Descendants("host"))
        {
            var address = host.Elements("address").FirstOrDefault()?.Attribute("addr")?.Value;
            if (string.IsNullOrWhiteSpace(address))
            {
                issues.Add("Nmap host is missing an address.");
                continue;
            }

            observations.Add(AuditObservation.Create(
                sessionId,
                AuditObservationKind.Host,
                address,
                "nmap",
                evidenceId,
                observedAt));

            foreach (var port in host.Descendants("port"))
            {
                var portId = port.Attribute("portid")?.Value;
                var protocol = port.Attribute("protocol")?.Value;
                var state = port.Element("state")?.Attribute("state")?.Value;
                if (string.IsNullOrWhiteSpace(portId) || string.IsNullOrWhiteSpace(protocol))
                {
                    issues.Add($"Nmap host {address} contains a port without protocol or port number.");
                    continue;
                }

                if (!string.Equals(state, "open", StringComparison.OrdinalIgnoreCase))
                    continue;

                var portValue = $"{protocol.ToLowerInvariant()}/{portId}";
                observations.Add(AuditObservation.Create(
                    sessionId,
                    AuditObservationKind.Port,
                    portValue,
                    "nmap",
                    evidenceId,
                    observedAt));

                var service = port.Element("service");
                if (service is null)
                    continue;

                var name = service.Attribute("name")?.Value;
                var product = service.Attribute("product")?.Value;
                var version = service.Attribute("version")?.Value;
                var serviceValue = string.Join(" ", new[] { name, product, version }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
                if (!string.IsNullOrWhiteSpace(serviceValue))
                {
                    observations.Add(AuditObservation.Create(
                        sessionId,
                        AuditObservationKind.Service,
                        $"{address}:{portValue} // {serviceValue}",
                        "nmap",
                        evidenceId,
                        observedAt));
                }
            }
        }

        return new(observations, issues);
    }
}
