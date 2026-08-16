# Keystone Getting Started (English)

> A universal **foundation plugin framework** for .NET — a plugin substrate any C# app can embed.
> 中文版： [getting-started.md](getting-started.md)

This guide gets you through three things with the smallest runnable examples:
**embed Keystone in a host → write and load a plugin → understand the core
plugin/host collaboration API.** For deeper principles, jump to the architecture
docs linked at the end.

---

## 1. What is Keystone?

Keystone is a **configuration-driven .NET plugin framework** with multi-instance
isolation and hot reload. It brings the "everything is a plugin" composition
discipline into C#, but replaces JS dynamism with native .NET capabilities
(DI, middleware shape, configuration system, Proto.Actor) and adds the
**lifecycle management** JS versions lack (supervision trees, hot reload,
dependency gating).

**We don't reinvent**: DI (`IServiceProvider`), configuration (`IOptions`),
logging (`ILogger`), hosted services, and the AI substrate (Microsoft MAF/MCP
composition). **We only implement what's framework-specific**: the ALC plugin
loader, plugin-ID-scoped registration/recycling, the pipeline config schema, and
the plugin SDK.

> Naming note: Keystone is an independently named foundation framework. The
> plugin philosophy is inspired by DeepSeek Harness's vendored Cordis (used as a
> reference baseline) — it is **not** an official re-implementation of Cordis.

## 2. Key Features

- **File-based plugins**: a single `.cs` file with top-level statements, compiled
  in-memory by Roslyn into a private, collectible `AssemblyLoadContext` (ALC).
- **Plugin SDK**: strongly-typed `IPlugin` / `IPluginContext` / `IMiddleware`
  surface — compile-time type safety (no `Dictionary<string, object>` service table).
- **Middleware pipeline**: `await next(ctx)` before/after = before/after semantics;
  not calling `next` = short-circuit (waterfall semantics).
- **Event system**: five dispatch modes — `emit` / `parallel` / `serial` /
  `bail` / `waterfall`.
- **Dependency-gated activation**: a plugin stays `PENDING` until its `inject`
  services are ready, then activates automatically; service changes auto-reload
  dependents.
- **Hot reload**: config-only changes take the "in-place same-ALC hot update"
  path; structural changes take the "cold reload" path; state is externalized so
  nothing is lost.
- **Configuration layer**: YAML entry tree, layered overlays, `!!env` / `!!file`
  static interpolation, schema validation, and fail-fast manifest checks.
- **AOT-ready**: host code is written to AOT-compatible standards (Rule 0), so a
  future switch to NativeAOT is low-cost.

## 3. Architecture at a Glance (three layers)

```
┌─────────────────────────────────────────────┐
│ Config layer   plugin manifests + capability │
│                definitions                   │
├─────────────────────────────────────────────┤
│ Management     KeystoneHost (host composition│
│                root, not an actor): read     │
│                config → create domain →      │
│                compile/load plugins →        │
│                hot reload → supervised restart│
├─────────────────────────────────────────────┤
│ Domain actor   holds context + pipeline +     │
│                event bus; requests flow the   │
│                pipeline (waterfall), observers│
│                use events                      │
└─────────────────────────────────────────────┘
```

`context` is long-lived, plugins are short-lived — hot reload swaps the old
plugin for a new one while the actor and context stay put, so state is preserved
and multiple instances are isolated by construction.

## 4. Prerequisites

- [.NET 10 SDK](https://dot.net/) (`net10.0` + C# 14)
- Packages: `Keystone.Hosting` (host entry point), `Keystone.Runtime` (plugin
  interface surface, referenced by plugin compilation), `Keystone.Sdk`
  (plugin-author surface: timer extensions / manifest validation)

## 5. Quick Start

### 5.1 Embed Keystone in your host

```csharp
using Keystone.Hosting;
using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;

// 1) Configure the host: wire "config entry -> manifest / source"
var options = new KeystoneHostOptions
{
    // entry -> plugin manifest (a record constructed by the embedder; provides/inject declarations)
    ManifestProvider = entry => new PluginManifest(
        id: entry.Id ?? "greeter",
        version: "1.0.0",
        main: $"{entry.Name}.cs",
        dependencies: ["Keystone.Runtime"],
        provides: ["greeter"]),

    // source abstraction: manifest.Main resolved against Roots (local files first; swap in a remote IPluginSource)
    PluginSource = new LocalPluginSource("plugins"),

    // config file path (CRUD changes are debounce-written back)
    ConfigFilePath = "keystone.yaml",
};

// 2) Start the host
var host = new KeystoneHost(options);
await host.StartFromFileAsync();

// 3) Run your application …

// 4) Graceful shutdown (global quiesce, per-plugin convergence)
await host.ShutdownAsync();
```

> `LocalPluginSource` resolves `manifest.Main` against its `Roots` (falling back
> to the `{root}/{id}/{main}` convention), and also enables
> `EnablePluginWatch()` to auto cold-reload on source changes. Production can
> swap in a remote-distribution implementation of `IPluginSource`, or use the
> synchronous delegate `SourceProvider = entry => new PluginSource(entry.Id!, code)`.
> Precise wiring is documented in `docs/architecture/09-management-layer.md`.

### 5.2 Write your first plugin

A plugin is a single-file class implementing `IPlugin`. This one registers a
service named `greeter` during initialization:

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Keystone.Runtime.Context;
using Keystone.Runtime.Events;
using Keystone.Runtime.Plugins.Lifecycle;

namespace MyApp.Plugins;

public sealed class GreeterPlugin : IPlugin
{
    public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
    {
        // Read from validated config (defaults already filled in)
        var greeting = config.TryGetValue("greeting", out var g) ? g?.ToString() : "Hello";

        // Provide a service: key = service name, type from the interface whitelist
        context.Provide<IGreeter>("greeter", new Greeter(greeting!));

        // Subscribe to events (subscriptions are recycled with the plugin lifecycle)
        context.Subscribe<TaskCompletedFact>(e => context.Logger.LogInformation("done: {TaskId}", e.TaskId));

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // Only detach what you registered; recycling is converged by the host quiesce
        return Task.CompletedTask;
    }
}

public interface IGreeter { string Greet(string name); }

public sealed class Greeter : IGreeter
{
    private readonly string _greeting;
    public Greeter(string greeting) => _greeting = greeting;
    public string Greet(string name) => $"{_greeting}, {name}!";
}
```

### 5.3 Write the plugin manifest

Each plugin has a manifest declaring its identity, assembly whitelist, and
service-level dependencies:

```json
{
  "id": "plugin-greeter",
  "version": "1.0.0",
  "main": "GreeterPlugin.cs",
  "dependencies": ["Keystone.Runtime"],
  "provides": ["greeter"],
  "inject": ["telemetry"]
}
```

In code the manifest is a `PluginManifest` record (constructed by the
`ManifestProvider` in 5.1); the JSON above is the idiomatic shape of its declared
content — the repository ships no built-in JSON loader, so embedders construct
the record from JSON / a database / etc. themselves (field validation via
`ManifestSchemaValidator`).

| Field | Dimension | Meaning |
|-------|-----------|---------|
| `dependencies` | assembly compile whitelist | which assemblies the plugin code may reference (Roslyn reference set) |
| `provides` / `inject` | service-level runtime deps | services the plugin provides/consumes (`inject` not ready → `PENDING` wait) |

The names in `provides` / `inject` are **service names** (semantic identities);
types are declared in the host interface whitelist. The manifest is fail-fast
validated at startup: unique id, reachable & acyclic dependency graph, `provides`
type within the whitelist.

### 5.4 Configure entries (YAML)

The host learns what to load from a YAML "entry tree":

```yaml
- id: greeter
  name: GreeterPlugin          # resolves via the ManifestProvider/PluginSource in 5.1
  inject: [telemetry]          # entry-level dependency (union with manifest inject)
  config:
    greeting: "Hello"

- id: tools
  group:                       # a group = transaction unit, cascading suspend/load
    - id: rate-limit
      name: RateLimitPlugin
      config:
        limit: 100
```

The top level is a list of entries with fields: `id` (stable identity), `name`
(plugin locator), `config`, `disabled`, `inject`, `isolate`, `group`.

### 5.5 Start & shutdown

See 5.1. `StartFromFileAsync()` will: parse layered YAML → schema validation →
manifest validation → build root context → load in parallel (dependency gating
naturally yields topological order) → ready. `ShutdownAsync()` runs a global
quiesce (per-plugin convergence + shutdown-timeout audit).

## 6. Plugin API Cheat Sheet (IPluginContext)

A plugin may only access capabilities through `IPluginContext` (`ctx`) — no
`new`-ing host internals, no touching the DI root container.

### Services

```csharp
ctx.Provide<T>("service-name", instance);   // provide a service (owner = this plugin)
T svc = ctx.Get<T>("service-name");          // PENDING-waits then injects if not ready
T? opt = ctx.TryGet<T>("service-name");      // optional service
ctx.Set<T>("service-name", instance);        // in-place update (owner-checked)
Lazy<Task<T>> lazy = ctx.GetLazy<T>("x");    // method-level lazy injection
```

### Events (five modes)

```csharp
ctx.Subscribe<TEvent>(e => { /* emit: non-blocking listener */ });
ctx.SubscribeParallel<TEvent>(async e => { /* parallel: concurrent */ });
ctx.SubscribeSerial<TEvent>(async e => { /* serial: first bail short-circuits */ return null; });
ctx.SubscribeBail<TEvent>(e => { /* bail: first non-null short-circuits */ return null; });
ctx.SubscribeWaterfall<TEvent>(async (e, next, ct) => { /* wrap the next chain */ await next(); });
ctx.EmitFireAndForget<TEvent>(e);            // fire-and-forget publish
```

### Timers (auto-recycled with the plugin lifecycle)

Timers are extension methods from the `Keystone.Sdk` package (namespace
`Keystone.Sdk.Timers`):

```csharp
using Keystone.Sdk.Timers;

ctx.SetTimeout (async () => { }, TimeSpan.FromSeconds(1));
ctx.SetInterval(async () => { }, TimeSpan.FromMinutes(1));
ctx.Throttle (async () => { }, TimeSpan.FromSeconds(2));
ctx.Debounce (async () => { }, TimeSpan.FromSeconds(2));
```

### Logging

```csharp
ctx.Logger.LogInformation("plugin started");
```

Logging goes through `ctx.Logger` (category = `{domain}/{plugin id}`); **do not
use `Console` directly**.

## 7. Middleware Pipeline (IMiddleware)

Write a plugin as a "middleware" mounted on the domain actor's pipeline:

```csharp
using Keystone.Runtime.Context;
using Keystone.Runtime.Pipeline;

public sealed class TimingMiddleware : IMiddleware
{
    public string Id => "timing";
    public int Order => 0;                       // ascending execution order
    public async Task InvokeAsync(IPluginContext ctx, RequestDelegate next)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await next(ctx);                          // RequestDelegate takes ctx; not calling next = short-circuit (reject)
        sw.Stop();
        ctx.Logger.LogInformation("took {Ms}ms", sw.ElapsedMilliseconds);
    }
}
```

Before `await next(ctx)` is before, after is after; returning without calling
`next` short-circuits (waterfall rejection). The pipeline shares the actor's
lifecycle; nodes (plugins) can be hot-swapped (`SwapPipelineAsync` atomically
replaces the chain).

## 8. Dependency Gating & Multi-Instance

- **Dependency gating**: a plugin stays `PENDING` until all `inject` services
  are ready; when a provider unloads/replaces, dependents auto reload/unload.
- **Multi-instance isolation**: one domain can spawn multiple actors, each with
  its own context (events/Effects/pipeline isolated); services are partitioned by
  a shared store + key (`realm` ∈ default shared / `#entryId` private / `@label`
  named shared).
- Plugins are stateless; state lives in the context, so hot reload loses no state
  and instances are isolated by construction.

## 9. Hot Reload

- **Config-only change**: `UpdatePluginAsync` / `ApplyConfigAsync` → in-place
  same-ALC hot update (no recompile, no ALC swap).
- **Structural change** (name/inject/isolate/cross-group): `ReloadPluginAsync` →
  cold reload (recompile + new ALC + quiesce old instance).
- File watching: `host.EnableConfigWatch()` (config change) and
  `host.EnablePluginWatch()` (source change, requires `LocalPluginSource`)
  trigger the above automatically, keeping the "last good tree" on failure.

## 10. Advanced Configuration

- **Layered overlays**: multiple YAML layers stack in order (base → profile →
  user patch → runtime overlay), merged by entry `id`.
- **Static interpolation**: `!!env NAME`, `!!file path` (recursive content
  interpolation + cycle detection).
- **Schema validation**: an entry may declare `configSchema`, validated by a
  source generator (required/unknown-field fail-fast) + default fill before
  injection into `InitializeAsync`.
- **Disable, don't delete**: `disabled: true` suspends an entry (kept in the
  tree, not loaded); a suspended parent group suspends the whole subtree.

## 11. Testing

```bash
dotnet test cordis-csharp.slnx     # 500+ unit tests (M0–M13 + audit passes)
```

Plugin authors should unit-test their plugins: start a real `KeystoneHost` with
an in-memory config, or construct a `ContextFacade` directly to verify
`Provide` / `Subscribe` / `DisposeAsync` behavior. The AOT discipline (Rule 0)
applies to plugin code too.

## 12. Next Steps

- Architecture overview: [docs/architecture/01-overview.md](../architecture/01-overview.md)
- Plugin model: [docs/architecture/02-plugin-model.md](../architecture/02-plugin-model.md)
- Context & scope chain: [docs/architecture/03-context.md](../architecture/03-context.md)
- Pipeline: [docs/architecture/04-pipeline.md](../architecture/04-pipeline.md)
- Configuration layer: [docs/architecture/08-configuration-layer.md](../architecture/08-configuration-layer.md)
- Full plugin SDK: [docs/architecture/10-plugin-sdk.md](../architecture/10-plugin-sdk.md)
- Design decisions (ADR-0001~0018): [docs/decisions/](../decisions/)
- Contributing: [CONTRIBUTING.en.md](../../CONTRIBUTING.en.md)

Have a question? See [SUPPORT.md](../../SUPPORT.md). 🫘
