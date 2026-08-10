# Operator Security Workbench

Operator Security Workbench is an open-source Windows security desktop application. It combines a professional audit workspace with the independently testable Operator Tunnel WireGuard control-plane module.

> Early development — not production-ready. Do not use this project to protect sensitive traffic yet.

## Modules

The application is intentionally modular while remaining a single Windows app:

- **Operator Tunnel** — WireGuard profile management, lifecycle, telemetry, DNS and kill-switch policy boundaries.
- **Audit Core** — audit projects, sessions, normalized observations and provenance for future terminal/parser workflows.

The audit framework will remain focused on authorized assessment workflows. It is not an automated exploitation tool.

## Operator Tunnel scope

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

## Build and run locally

After the initial dependency restore, build and test the app with:

```powershell
.\scripts\build.ps1
```

For a clean restore:

```powershell
.\scripts\build.ps1 -Restore
```

The script prints the generated executable path after a successful build. The current UI is a demo shell; it does not yet control a real WireGuard tunnel.

## License

To be selected before the first public release.

