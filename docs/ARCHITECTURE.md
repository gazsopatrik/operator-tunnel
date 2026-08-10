# Architecture

## Responsibilities

## Modular application boundary

The application is a single Windows desktop product composed of separately
owned modules:

```text
Operator Security Workbench
├── OperatorTunnel.Core       shared security and infrastructure primitives
├── OperatorTunnel.Audit      audit projects, sessions, and normalized observations
├── OperatorTunnel.App        WPF shell and module navigation
└── Operator Tunnel backend   WireGuard-specific control-plane integration
```

`OperatorTunnel.Audit` deliberately does not reference the WireGuard backend.
It stores an optional VPN profile name as session context, never VPN private
keys or raw tunnel configuration. The VPN module remains independently
testable and reusable, while the audit module can later accept observations
from Nmap, other tools, manual input, or external hardware.

The first shared audit entities are `AuditProject`, `AuditSession`, and
`AuditObservation`. Every observation carries a session ID, source, timestamp,
and raw evidence ID so later parsers cannot silently detach data from its
provenance.

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

The process runner redirects output and supports cancellation. Raw process output is not exposed to the UI by the backend adapter because service output can contain machine-specific or sensitive diagnostics.

Configuration serialization is an in-memory boundary only. The application must not write the serialized private key to a normal user file until encrypted storage and restrictive ACLs are implemented.

The first secrets boundary uses Windows DPAPI scoped to the current user, with application-specific entropy. DPAPI failures are fatal to the storage operation; the app must not fall back to plaintext storage.

The encrypted profile store uses a versioned file header, sanitized profile names, a root-directory containment check, and atomic replacement through a temporary file. Stored bytes contain the DPAPI ciphertext, not the serialized configuration.

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

