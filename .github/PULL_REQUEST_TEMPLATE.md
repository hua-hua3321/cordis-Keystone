<!-- Thanks for contributing to Keystone! -->

## Summary

<!-- What does this PR do, and why? Link the issue it closes (e.g. "Closes #123"). -->

## Type of change

- [ ] `feat` — new feature
- [ ] `fix` — bug fix
- [ ] `docs` — documentation only
- [ ] `refactor` — no behavior change
- [ ] `test` — tests only
- [ ] `chore` — build/tooling/misc

## Affected areas

<!-- e.g. Plugin model, Configuration layer, Hosting API, SDK, Observability… -->

## Verification

<!-- How did you verify? Paste build/test/format command output. -->

- [ ] `dotnet build cordis-csharp.slnx` passes (warnings = errors)
- [ ] `dotnet test cordis-csharp.slnx` passes
- [ ] `dotnet format cordis-csharp.slnx --verify-no-changes` passes
- [ ] AOT smoke test ran (if applicable to host code)

## AOT-ready check (Rule 0)

- [ ] No runtime reflection / `Reflection.Emit` / dynamic assembly in host code
- [ ] Serialization uses explicit contracts; Source Generators where applicable
- [ ] Plugin-loading layer is the only deliberate exception (Roslyn + ALC)

## Docs / Changelog

- [ ] Updated `CHANGELOG.md` (if user-visible)
- [ ] Updated relevant `docs/architecture/` or `docs/decisions/` (if applicable)
- [ ] ADR added/updated for design-level changes (if applicable)
