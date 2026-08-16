# Keystone Foundation Plugin Framework (cordis-csharp)

> A **universal foundation plugin framework** for .NET: any C# application can
> embed it (configuration-driven, multi-instance isolation, hot reload, and a
> middleware-pipeline execution model).
> **Naming & positioning note**: Keystone is an independently named foundation
> framework. The plugin philosophy is inspired by DeepSeek Harness's vendored
> Cordis (used as a reference baseline) — it is **not** an official
> re-implementation of Cordis and does not claim the Cordis name; "Cordis" here
> refers only to the upstream reference.

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dot.net/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![CI](https://github.com/hua-hua3321/cordis-Keystone/actions/workflows/ci.yml/badge.svg)](https://github.com/hua-hua3321/cordis-Keystone/actions/workflows/ci.yml)
[![Docs](https://img.shields.io/badge/docs-architecture-blue)](docs/architecture/)

**Keystone** is a universal, configuration-driven plugin framework for .NET — a
plugin substrate any C# application can embed, with multi-instance isolation,
hot reload, and a middleware-pipeline execution model.

中文版（Chinese）： [README.cn.md](README.cn.md)

## 📚 Documentation

| Resource | Link |
|----------|------|
| Getting Started (English) | [docs/tutorials/getting-started.en.md](docs/tutorials/getting-started.en.md) |
| 快速上手（中文） | [docs/tutorials/getting-started.md](docs/tutorials/getting-started.md) |
| Architecture (20 docs) | [docs/architecture/](docs/architecture/) |
| Design decisions ADR-0001~0018 | [docs/decisions/](docs/decisions/) |
| Contributing (EN / 中) | [CONTRIBUTING.en.md](CONTRIBUTING.en.md) · [CONTRIBUTING.md](CONTRIBUTING.md) |
| Code of Conduct | [CODE_OF_CONDUCT.en.md](CODE_OF_CONDUCT.en.md) |
| Security Policy | [SECURITY.en.md](SECURITY.en.md) |
| Support & Q&A | [SUPPORT.en.md](SUPPORT.en.md) |
| Changelog | [CHANGELOG.en.md](CHANGELOG.en.md) |

## Current Status

**Implementation complete (M0–M13 all passed, 500 tests green)**: the Keystone
1.0 framework is runnable — all thirteen phases are delivered (contracts /
context / events / lifecycle / pipeline / loading / configuration / management /
capability domains / observability / persistence / SDK / AI composition), with
zero AOT IL warnings across the solution (Proto.Actor / MAF exceptions per
ADR-0015). This repository is the Single Source of Truth for the design and
implementation progress.

## Project Positioning

- **Universal foundation**: any C# app (web / service / desktop / embedded host)
  can embed Keystone as its plugin substrate; not tied to any business domain.
- **We don't reinvent**: DI (`IServiceProvider`), middleware pipeline (ASP.NET
  Core shape), configuration (`IOptions` + M.E.C provider abstraction), logging
  (`ILogger`), hosted services (`IHostedService`), and the **AI substrate (LLM
  adapters / skill packs / MCP / agent orchestration — composed from Microsoft's
  official MAF/MCP, ADR-0008)**.
- **We only implement what's framework-specific**: the ALC plugin loader,
  plugin-ID-scoped registration/recycling, the pipeline config schema, and the
  plugin SDK.
- **Configuration unbundled**: configuration sources are not hard-locked — a
  provider abstraction (ADR-0013), default local YAML (development, ADR-0014),
  with AgileConfig as a reserved optional source; users may implement any source.
  Hardcoding is forbidden (all tunable framework values go through configuration).

## Core Mechanisms

| Mechanism | Description | Docs |
|-----------|-------------|------|
| Plugin hot reload | Roslyn in-memory compilation + private Collectible ALC + quiesce convergence gate | [02-plugin-model.md](docs/architecture/02-plugin-model.md), ADR-0005 |
| Dependency-gated activation | manifest `inject` service-level deps; PENDING waits until deps are ready | ADR-0007 |
| Event dispatch | five modes — emit / parallel / serial / bail / waterfall | ADR-0006 |
| Multi-instance isolation | independent context + child container + service-level isolate | [03-context.md](docs/architecture/03-context.md) |
| Config provider abstraction | M.E.C `IConfigurationSource` contract + default local YAML (AOT-safe YamlStream parsing); AgileConfig reserved optional source | ADR-0013/0014, [08-configuration-layer.md](docs/architecture/08-configuration-layer.md) |
| AI capability composition | compose Microsoft's official MAF/MCP (SEP-2640 skill packs, MCP both ends, Workflows); one-directional dependency, core stays free | ADR-0008 |

## Documentation Index

- Architecture: `docs/architecture/` (00 tech stack ~ 19 second parity audit; see
  the [AGENTS.md](AGENTS.md) index)
- Decisions: `docs/decisions/README.md` (ADR-0001 ~ 0018; converged at design
  time; implementation-time decisions go through the 14 §4 channel)
- Gap analysis: [07-cordis-migration-gap.md](docs/architecture/07-cordis-migration-gap.md)
  (7 must-check items vs vendored Cordis source + gap list)
- Implementation: [13-implementation-plan.md](docs/architecture/13-implementation-plan.md)
  (phased plan + milestones) + [14-implementation-log.md](docs/architecture/14-implementation-log.md)
  (process log + back-trace index)

## Build & Test

```bash
dotnet build cordis-csharp.slnx          # warnings as errors
dotnet test  cordis-csharp.slnx           # 500 unit tests (M0–M13 + P14–P72 audit batches)
```

Rule 0 AOT smoke test (per-phase acceptance, 13 §4):

```bash
dotnet publish src/Keystone.Core -c Release -r osx-arm64 --self-contained /p:PublishAot=true
dotnet publish src/Keystone.Config -c Release -r osx-arm64 --self-contained /p:PublishAot=true
```

Doc validation (R10):

```bash
cd ~/Projects/central-governance && python3 scripts/validate_frontmatter.py
```

## Governance

- Central governance library: `~/Projects/central-governance` (rules D01–D08 +
  R00–R20)
- Rule 0: AOT-ready coding standard (highest priority, see [AGENTS.md](AGENTS.md))
  — currently JIT (ADR-0002), but all code must be written to AOT-compatible
  standards.
- Write an ADR before landing a new decision; doc changes follow the documentation
  governance rule (R10).

## Reference Projects

- cognitive-tree-csharp (sibling kanban, slug cognitivetree-c / cognitive-tree-csharp)
- DeepSeek Harness (vendored Cordis source baseline:
  `~/Projects/deepseek-harness/vendor/cordis/src/` — reference only)

---

## 🤝 Community & Contributing

Contributions welcome! Please read the [Contributing guide](CONTRIBUTING.en.md)
and [Code of Conduct](CODE_OF_CONDUCT.en.md) first. Use the GitHub issue
templates for bugs and feature requests; for security issues see the
[Security Policy](SECURITY.en.md) — **do not** file a public issue. For help,
see [Support](SUPPORT.en.md).

## 📄 License

This project is open-sourced under the [MIT License](LICENSE). © 2026 Keystone
contributors.
