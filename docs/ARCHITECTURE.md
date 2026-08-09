# Architecture

## Responsibilities

### Operator Tunnel UI

- Displays profiles, state, health, statistics, and diagnostics.
- Imports configuration through the validation pipeline.
- Never directly owns privileged operations.
- Redacts secrets before rendering or logging.

### Operator Tunnel broker/service

- Runs with the minimum required Windows privilege.
- Validates requested operations against an explicit command model.
- Invokes the supported WireGuard Windows integration.
- Reads tunnel state and returns a typed, redacted status model.
- Applies DNS and kill-switch policy where supported by the platform design.

### WireGuard backend

- Owns the VPN protocol, key exchange, encryption, authentication, and packet transport.
- Is treated as a security-critical external dependency.

The Windows integration targets the official per-tunnel service model. The service name is constrained to `WireGuardTunnel$<validated-name>`, and process arguments are passed as an argument list rather than through a shell.

## Command flow

```text
UI -> typed local IPC command -> broker validation -> WireGuard backend
UI <- redacted status/event model <- broker
```

The UI must not be granted arbitrary command-line execution, arbitrary file paths, or raw service handles. The broker should expose a small allowlisted API such as `ImportProfile`, `Connect`, `Disconnect`, `GetStatus`, and `GetStatistics`.

## Data handling rules

- Private keys are write-only from the UI perspective after import where practical.
- Imported profiles are normalized into an internal model before use.
- Logs use structured events and automatic redaction.
- User-controlled names and paths are treated as untrusted input.

