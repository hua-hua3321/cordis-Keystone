# Changelog

> 中文版： [CHANGELOG.md](CHANGELOG.md)

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] — 2026-08-15

First stable release of the **Keystone** universal foundation plugin framework
for .NET.

### Added

- **Three-layer architecture**: configuration layer, management layer
  (`KeystoneHost` / `CompositionRoot` actor), and capability-domain actors.
- **Plugin model**: file-based (.NET 10 file-scoped apps) and precompiled DLL
  plugins, loaded via Roslyn in-memory compilation into a collectible
  `AssemblyLoadContext` (ALC).
- **Lifecycle & hot reload**: `IPlugin` contract, dependency-gated activation
  (PENDING until dependencies are ready), and true hot update (config-only
  in-place swap, ADR-0017) plus cold reload.
- **Middleware pipeline**: `IMiddleware` with `await next()` waterfall semantics
  and short-circuit support.
- **Event system**: five dispatch modes — `emit`, `parallel`, `serial`, `bail`,
  `waterfall` (ADR-0006).
- **Service model**: keyed service store with realm-based isolation
  (`Provide` / `Get` / `Set`), typed via a compile-time interface whitelist
  (no `Dictionary<string, object>`).
- **Configuration layer**: YAML entry tree with layered overlays, static
  interpolation (`!!env` / `!!file`), schema validation, and fail-fast manifest
  checks (ADR-0013 / ADR-0014).
- **AI capability composition**: Microsoft MAF / MCP composition (one-directional
  dependency; framework core stays free of AI packages), SEP-2640 skill support
  (ADR-0008).
- **Observability**: OpenTelemetry tracing and metrics hooks (ADR-0018).
- **AOT-ready codebase**: all host code written to AOT-compatible standards
  (Rule 0), with the plugin-loading layer as the only deliberate exception.
- **Plugin SDK template**: `dotnet new keystone-plugin` scaffolding.
- **500+ unit tests** covering all milestones (M0–M13) and parity/audit passes.

### Notes

- This release is **JIT** (not NativeAOT) per ADR-0002; the codebase remains
  AOT-compatible so a future switch is low-cost.
- Plugins execute as same-process trusted code by default (ADR-0001).
