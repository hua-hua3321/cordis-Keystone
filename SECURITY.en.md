# Security Policy

> 中文版： [SECURITY.md](SECURITY.md)

## Supported Versions

We currently support the latest stable minor release line of Keystone 1.x.
Security fixes are backported to the most recent release.

| Version | Supported          |
| ------- | ------------------ |
| 1.x     | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

If you discover a security vulnerability in Keystone, please **do not** open a
public GitHub issue.

Instead, report it privately through one of the following channels:

- **GitHub Private Vulnerability Reporting** — use the "Report a vulnerability"
  button on the **Security** tab of the repository. This is the preferred
  channel.
- **Email** — send details to **security@keystone.dev** (replace with the
  project owner's address). Use encryption if available.

Please include:

- A description of the vulnerability and its impact
- Steps to reproduce (proof of concept if possible)
- Affected version(s) and environment
- Any suggested mitigation, if known

### What to expect

1. **Acknowledgement** within 5 business days.
2. **Triage and confirmation** — we will reproduce and assess severity.
3. **Coordinated disclosure** — we aim to release a fix and publish an advisory
   within 90 days of confirmation, or earlier when a practical workaround exists.
4. **Credit** — with your permission, we will acknowledge reporters in the
   advisory.

## Security Design Notes

Keystone treats plugins as **same-process trusted code** by default (see
`docs/architecture/02-plugin-model.md` and ADR-0001). The trust boundary is the
user who installs the plugin. If your deployment requires stronger isolation
(e.g. untrusted third-party plugins), do not load untrusted sources until the
process-isolation extension point (`IPluginHost`, ADR-0001 decision 1) is
available, and consider running plugins in a separate, sandboxed host process.
