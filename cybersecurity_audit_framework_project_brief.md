# Cybersecurity Audit Framework --- Project Brief

## Project Goal

Build a professional cybersecurity audit application that helps security
professionals collect, organize, visualize, and analyze audit data.

The application is **not an automated exploitation tool**. Its purpose
is to reduce repetitive audit work by automatically processing the
output of security tools, organizing discovered assets, correlating
detected software with known vulnerabilities, and helping the user
create verified findings.

The main workflow should be:

**Terminal → Scan/Command Output → Parsed Assets → Visual Nodes →
CVE/Intel Correlation → Manual Verification → Findings → Report**

The UI should use a consistent **black and green terminal-inspired
design**, similar to a modern cybersecurity operations console rather
than an exaggerated "hacker" interface.

------------------------------------------------------------------------

## 1. Audit Projects

Users should be able to create separate audit projects.

Each project should contain:

-   Project name
-   Scope
-   Targets
-   Assets
-   Services
-   Findings
-   Evidence
-   Notes
-   Audit history
-   Network/topology map
-   Security intelligence related to discovered technologies

All information collected during an audit belongs to the currently
active project.

------------------------------------------------------------------------

## 2. Integrated Audit Terminal

The application should contain a real terminal that security
professionals can use during an authorized assessment.

Example:

``` bash
nmap -sV 10.10.20.15
```

The important feature is that the application should not only display
terminal output.

When supported security tools are executed, their output should
automatically be parsed and added to the audit database.

For example, if Nmap discovers:

``` text
10.10.20.15

22/tcp   OpenSSH 9.3p1
80/tcp   nginx 1.24
443/tcp  nginx 1.24
```

the framework should automatically create or update:

``` text
Host
└── 10.10.20.15
    ├── Port 22
    │   └── OpenSSH 9.3p1
    ├── Port 80
    │   └── nginx 1.24
    └── Port 443
        └── nginx 1.24
```

The user should not need to manually copy results from the terminal into
the audit.

------------------------------------------------------------------------

## 3. Parser System

The terminal should support modular parsers.

Initial support should focus on a small number of tools, starting with
**Nmap**.

Architecture example:

``` text
Terminal
   ↓
Command Execution
   ↓
Output Detection
   ↓
Tool Parser
   ↓
Normalized Audit Data
   ↓
Audit Database
```

Whenever possible, prefer structured output such as XML or JSON instead
of parsing human-readable terminal text.

The parser architecture should be extendable so additional security
tools can be supported later.

Possible future parsers include:

-   Nuclei
-   Nikto
-   DNS tools
-   HTTP enumeration tools
-   vulnerability scanners
-   custom hardware/software tools

Unknown commands should still work normally in the terminal even when
the framework cannot parse their output.

------------------------------------------------------------------------

## 4. Asset and Service Database

The framework should maintain a structured inventory.

Example entities:

``` text
Project
Asset
Host
Domain
IP Address
Port
Service
Technology
Finding
Evidence
CVE Candidate
```

Detected technologies should be normalized where possible.

Example:

``` text
Product: OpenSSH
Version: 9.3p1
Port: 22
Host: 10.10.20.15
Source: Nmap
```

Every discovered piece of information should retain information about
where it came from.

For example:

``` text
Discovered by:
nmap -sV 10.10.20.15

Time:
2026-08-10 08:14

Audit Session:
SESSION-0042
```

This provenance information is important because the user must be able
to trace audit data back to its original evidence.

------------------------------------------------------------------------

## 5. Automatic CVE Correlation

When software and versions are discovered, the framework should
automatically search vulnerability intelligence sources for potentially
relevant CVEs.

Example:

``` text
Detected:
OpenSSH 9.3p1

Framework:
→ normalize product/version
→ search CVE sources
→ compare affected versions
→ attach possible matches to the service
```

The application must NOT automatically claim that a system is vulnerable
simply because a CVE appears to match.

Instead, use statuses such as:

``` text
Potential Exposure
Verification Required
Verified
Not Affected
False Positive
```

A CVE candidate only becomes a real audit finding after verification by
the user.

Where possible, display a confidence level:

``` text
HIGH
Exact product and version match

MEDIUM
Product matches but version information is incomplete

LOW
Technology detected but affected version cannot be confirmed
```

------------------------------------------------------------------------

## 6. Security Intelligence Feed

The application should include an optional live security intelligence
panel.

Instead of displaying completely random cybersecurity news, the feed
should prioritize information related to technologies discovered in the
current audit.

For example, if the audit contains:

``` text
OpenSSH
nginx
WordPress
PHP
```

the feed should prioritize:

-   relevant new CVEs
-   vendor security advisories
-   vulnerability research
-   important security news involving these technologies

Example:

``` text
[CRITICAL]
New CVE potentially affects OpenSSH

MATCHING ASSETS: 3

[ADVISORY]
nginx security update published

MATCHING ASSETS: 2
```

The intelligence panel should be collapsible or completely disabled by
the user.

------------------------------------------------------------------------

## 7. Visual Audit Map

The application should contain an interactive node-based visualization
of the audit.

Example:

``` text
Internet
   │
   ▼
Gateway
   │
   ▼
10.10.20.15
   ├── OpenSSH :22
   │      └── Potential CVE
   │
   └── nginx :443
          └── Potential CVE
```

Users should be able to:

-   drag nodes
-   connect nodes
-   inspect nodes
-   create relationships
-   open related evidence
-   view CVEs
-   create findings from nodes
-   add manual nodes
-   add notes

The graph should update as new information is discovered through the
terminal.

The graph is not only decorative. It should represent relationships
stored in the application's data model.

------------------------------------------------------------------------

## 8. Findings

Verified security issues should be stored as findings.

A finding should support fields such as:

``` text
Title
Severity
Affected Asset
Description
Evidence
Related CVE/CWE
Remediation
Status
Created Date
Verification Notes
```

Example workflow:

``` text
Detected Service
      ↓
Potential CVE
      ↓
Manual Verification
      ↓
Verified Finding
      ↓
Audit Report
```

This distinction between **potential vulnerability** and **verified
finding** is important throughout the application.

------------------------------------------------------------------------

## 9. Evidence

Users should be able to attach evidence to assets and findings.

Examples:

-   terminal output
-   screenshots
-   logs
-   scan results
-   files
-   notes
-   imported scanner results

Terminal-generated evidence should preferably be stored automatically.

------------------------------------------------------------------------

## 10. Future Hardware Integration

The architecture should allow external tools to send audit data into the
framework later.

One planned example is a custom security tool running on a **LILYGO
T-Embed CC1101 Plus**.

Future hardware could collect authorized field data and send
observations to the framework, where they become assets, observations,
or nodes.

Therefore, avoid designing the application around Nmap specifically.

Nmap should be the first data source, not the foundation of the entire
data model.

The core should accept normalized observations from many sources.

Conceptually:

``` text
Nmap ──────────┐
Nuclei ────────┤
Manual Input ──┤
LILYGO ────────┤
Future Tools ──┘
       │
       ▼
Normalized Audit Data
       │
       ▼
Audit Core
       │
 ┌─────┼─────────┐
 ▼     ▼         ▼
Graph Findings  Intel
```

------------------------------------------------------------------------

## 11. UI / Design

Use a consistent black and green visual identity.

General direction:

-   black / near-black backgrounds
-   terminal green accent
-   monospace typography where appropriate
-   thin borders
-   compact professional panels
-   terminal-inspired status messages
-   subtle animations
-   clear severity indicators
-   dense information without becoming difficult to navigate

The application should feel like a modern professional security
operations tool.

Avoid excessive "Hollywood hacker" effects.

Possible main navigation:

``` text
> DASHBOARD
> TERMINAL
> ASSETS
> TOPOLOGY
> FINDINGS
> INTELLIGENCE
> EVIDENCE
> REPORTS
```

------------------------------------------------------------------------

## 12. Dashboard

The dashboard should provide a quick overview of the current audit.

Example:

``` text
AUDIT STATUS

24 ASSETS
67 SERVICES
8 POTENTIAL EXPOSURES
3 VERIFIED FINDINGS
2 HIGH PRIORITY INTEL MATCHES

LATEST ACTIVITY

08:14  Nmap scan completed
08:14  4 new services discovered
08:15  CVE correlation completed
08:17  New potential exposure detected
```

------------------------------------------------------------------------

## 13. Development Strategy

Do NOT attempt to implement every planned feature immediately.

The project should be designed for expansion, but development should
happen incrementally.

### Initial MVP

Focus on:

1.  Project creation and management
2.  Asset database
3.  Integrated terminal
4.  Nmap execution/import
5.  Nmap result parsing
6.  Automatic creation of hosts/services
7.  Basic interactive topology graph
8.  CVE correlation for detected technologies
9.  Basic findings management
10. Black/green terminal-style UI

Once these features work reliably, expand the framework with:

-   security news
-   additional tool parsers
-   evidence management
-   reporting
-   advanced topology
-   additional intelligence sources
-   external API
-   hardware integration
-   LILYGO integration
-   collaboration features

------------------------------------------------------------------------

## Core Principle

The primary idea of the application is:

**Work in the terminal. The audit builds itself.**

The security professional remains in control of the assessment.

The framework handles repetitive work:

``` text
collect
→ parse
→ normalize
→ organize
→ correlate
→ visualize
```

The professional handles:

``` text
investigate
→ verify
→ decide
→ document
```

The result should be a semi-automated professional cybersecurity audit
environment that saves time without becoming an automated exploitation
framework.
