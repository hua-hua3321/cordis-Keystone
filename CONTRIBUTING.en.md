# Contributing

Thank you for considering a contribution to the **Keystone** foundation plugin
framework! This guide explains how to report issues, propose changes, and submit
pull requests (PRs).

> 中文版： [CONTRIBUTING.md](CONTRIBUTING.md)

## Code of Conduct

By participating, you agree to abide by our
[Code of Conduct](CODE_OF_CONDUCT.md). Please be respectful and welcoming to all
community members.

## How Can I Contribute?

- Report bugs (use the **Bug report** issue template)
- Propose features / design improvements (use the **Feature request** template,
  or start a Discussion first)
- Fix documentation, add examples and tutorials
- Submit code fixes and features (via the PR workflow)

## Development Environment

**Prerequisites**

- [.NET 10 SDK](https://dot.net/) (the project targets `net10.0` + C# 14)
- Any .NET-capable editor (Visual Studio / VS Code + C# extension recommended)
- Optional: Python 3 (for the internal doc frontmatter validation tooling)

**Get the code**

```bash
git clone https://github.com/hua-hua3321/cordis-Keystone.git
cd cordis-Keystone
dotnet restore cordis-csharp.slnx
```

## Build & Test

```bash
dotnet build cordis-csharp.slnx            # warnings are errors
dotnet test  cordis-csharp.slnx            # 500+ unit tests
```

Style check (also run by CI):

```bash
dotnet format cordis-csharp.slnx --verify-no-changes
```

### AOT-Ready Discipline (highest priority)

This project does **not** currently use NativeAOT (ADR-0002), but **all host
code must be written to AOT-compatible standards** (see `AGENTS.md` Rule 0).
Before submitting, please ensure:

- No runtime `Reflection.Emit` / `Expression.Compile` / dynamic assembly
  generation
- No runtime reflection in business code; prefer Source Generators or
  compile-time-known types
- Explicit serialization contracts (`[MessagePackObject]` / `[JsonSerializable]`)
- No `CSharpScript` / `CodeDom` / `Assembly.Load(byte[])`
- Run an AOT smoke test once if your configuration permits:
  ```bash
  dotnet publish src/Keystone.Core -c Release -r <rid> --self-contained /p:PublishAot=true
  ```
  The only exception is the plugin-loading layer (Roslyn + ALC, ADR-0001/0002).

### Coding Style

- The repo ships `.editorconfig` and `Directory.Build.props`; follow the existing
  style.
- Reuse existing patterns (naming, structured logging, `KeystoneException`
  error codes).
- Do not re-invent capabilities .NET already provides (DI, configuration,
  logging, middleware shape).

## Commit Convention

Use `<type>: <desc>` commit messages:

| type      | meaning                      |
|-----------|------------------------------|
| `feat`    | new feature                  |
| `fix`     | bug fix                      |
| `docs`    | documentation                |
| `refactor`| refactoring (no behavior change) |
| `test`    | tests                        |
| `chore`   | build / tooling / misc       |

Example: `fix: resolve service-rebind conflict on cold reload`

Keep commits atomic and revertible — one logical change per commit.

## Design Decisions (ADRs)

Architectural or interface-level changes require an **ADR** in
`docs/decisions/` (ADR-0001 ~ ADR-0018 are converged). Sync the `AGENTS.md`
index and related architecture docs. Implementation-time decisions go through
the `docs/architecture/14-implementation-log.md` §4 channel.

## PR Workflow

1. Fork and create a feature branch off `main` (`feat/xxx`, `fix/xxx`).
2. Ensure `dotnet build` / `dotnet test` / `dotnet format` all pass.
3. Fill out the PR template: motivation, scope, and how you verified.
4. At least one maintainer review is required before merge (see
   [CODEOWNERS](CODEOWNERS)).
5. If the change is user-visible, update `CHANGELOG.md` and the relevant docs.

## Documentation

- Architecture docs: `docs/architecture/`; decisions: `docs/decisions/`;
  tutorials: `docs/tutorials/`.
- Keep the frontmatter header on architecture docs (validated by internal
  tooling; does not affect normal contributions).

---

Thanks again for contributing! 🫘
