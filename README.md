# Operator Tunnel

Operator Tunnel is an open-source Windows desktop client for managing WireGuard tunnels with a security-focused control plane and a cyberpunk-inspired user experience.

> Early development — not production-ready. Do not use this project to protect sensitive traffic yet.

## Scope

The application will provide:

- WireGuard profile import and management
- Strict, safe configuration validation
- Connect/disconnect and tunnel status
- Handshake and transfer statistics
- DNS policy and kill-switch controls
- Allowed-IPs visualization
- Connection and error logging without secret leakage
- Windows system-tray operation

The application does **not** implement a VPN protocol or cryptography. WireGuard remains responsible for tunnel cryptography and packet protection; Operator Tunnel owns the user-facing control plane, policy checks, state presentation, and Windows integration.

## Security boundary

The UI is treated as unprivileged code. Operations requiring elevation will cross a narrow, authenticated local service boundary. Private keys and sensitive configuration values must never be written to logs, telemetry, screenshots, or crash reports.

See:

- [Architecture](docs/ARCHITECTURE.md)
- [Threat model](docs/THREAT-MODEL.md)
- [Development roadmap](docs/ROADMAP.md)

## Planned stack

- C#/.NET desktop client
- WPF for the initial Windows shell and system-tray integration
- Official WireGuard Windows tunnel service/backend
- Unit and integration tests around parsing, policy, and service boundaries

## License

To be selected before the first public release.

