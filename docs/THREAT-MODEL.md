# Threat model

## Assets

- WireGuard private keys and preshared keys
- Tunnel configuration and endpoint metadata
- DNS and routing policy
- User traffic while the tunnel is active
- Connection diagnostics and transfer statistics

## Trust zones

1. **Unprivileged UI** — potentially exposed to malformed files, injected input, or local user tampering.
2. **Privileged broker** — small trusted computing base responsible for privileged actions.
3. **WireGuard service/backend** — protocol and cryptographic implementation outside this project.
4. **Operating system/network** — Windows services, adapters, DNS resolver, firewall, and hostile networks.

## Primary threats and mitigations

| Threat | Mitigation | Verification |
|---|---|---|
| Malformed or hostile profile | Strict parser, bounds checks, duplicate-key policy, safe normalization | Parser unit tests and fuzz corpus |
| Private-key disclosure | DPAPI/Windows-protected storage, redacted logs, no secret-rich exceptions | Secret scanning and redaction tests |
| Privilege escalation through UI | Narrow typed IPC, broker-side authorization and validation | IPC integration tests |
| DNS leak during tunnel use | Explicit DNS policy, transition-state handling, leak test matrix | Windows integration tests |
| Traffic leak during disconnect | Kill-switch state machine and fail-closed behavior | Firewall/routing integration tests |
| Misconfigured DNS or kill switch | Typed network policy, fail-closed validation, explicit offline warnings | Policy unit tests and Windows integration tests |
| Stale or misleading UI state | Backend is source of truth; event reconciliation and polling fallback | State-machine tests |
| Malicious local profile path | Canonicalization, constrained file access, no arbitrary execution | Path validation tests |

## Out of scope for the first milestone

- Implementing cryptography or a new VPN protocol
- Hiding activity from the operating system or network provider
- Treating Tor as a WireGuard feature
- Claiming leak-proof behavior before Windows integration testing exists

