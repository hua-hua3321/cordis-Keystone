# Keystone 快速上手（中文）

> 通用地基插件框架（cordis-csharp）——任何 C# 应用都能嵌入的插件底层。
> 配套英文版：[getting-started.en.md](getting-started.en.md)

本文用最小可运行的例子带你跑通三件事：**在宿主程序中嵌入 Keystone → 编写并加载一个插件 → 理解插件与宿主协作的核心 API**。深入原理请转到文末「下一步」里的架构文档。

---

## 1. Keystone 是什么

Keystone 是一个**配置驱动、支持多实例隔离与热重载**的 .NET 插件框架。它把「一切皆插件」的组合纪律带进 C#，但用 .NET 原生能力（DI、中间件形状、配置系统、Proto.Actor）替代 JS 动态特性，并补上了 JS 版本没有的**生命周期管理**（监督树、热重载、依赖门控）。

**不重造**：DI（`IServiceProvider`）、配置（`IOptions`）、日志（`ILogger`）、后台服务、以及 AI 底层（组合微软官方 MAF/MCP）。**只实现框架独有的部分**：ALC 插件加载层、按插件 ID 分组的注册回收、管道配置 schema、插件 SDK。

> 命名说明：Keystone 是独立命名的地基框架，插件理念受 DeepSeek Harness vendored Cordis 启发（作为参照基线），**非 Cordis 官方再实现**。

## 2. 核心特性

- **文件式插件**：单文件 `.cs` + 顶层语句，经 Roslyn 内存编译进私有可卸载 `AssemblyLoadContext`（ALC）。
- **插件 SDK**：`IPlugin` / `IPluginContext` / `IMiddleware` 强类型接口面，编译期类型安全（无 `Dictionary<string, object>` 服务表）。
- **中间件管道**：`await next(ctx)` 前后即 before/after，不调用 `next` 即短路（waterfall 语义）。
- **事件系统**：`emit` / `parallel` / `serial` / `bail` / `waterfall` 五种分发模式。
- **依赖门控激活**：插件 `inject` 的服务未就绪前保持 `PENDING`，就绪后自动激活；服务变更自动重载依赖方。
- **热重载**：仅配置变化走「同 ALC 原地热更新」，结构变化走「冷重启」，状态外置不丢数据。
- **配置层**：YAML 条目树、分层叠加、`!!env` / `!!file` 静态插值、schema 校验、fail-fast manifest 检查。
- **AOT 就绪**：宿主代码按 AOT 兼容标准编写（规则 0），未来切 NativeAOT 零改动。

## 3. 架构一览（三层）

```
┌─────────────────────────────────────────────┐
│ 配置层  插件清单 + 能力域定义                  │
├─────────────────────────────────────────────┤
│ 管理层  KeystoneHost（宿主组合根，非 actor）   │
│   读配置 → 创建能力域 → 编译/加载插件           │
│   → 热重载 → 监督重启                          │
├─────────────────────────────────────────────┤
│ 能力域 actor  持 context + 管道 + 事件总线      │
│   请求链走管道（waterfall），观察者走事件         │
└─────────────────────────────────────────────┘
```

`context` 长命、插件短命——热重载 = 摘旧插件换新插件，actor 与 context 不动，因此状态不丢、多实例天然隔离。

## 4. 环境要求

- [.NET 10 SDK](https://dot.net/)（`net10.0` + C# 14）
- 引用包：`Keystone.Hosting`（宿主入口）、`Keystone.Runtime`（插件接口面，供插件编译引用）、`Keystone.Sdk`（插件作者友好面：计时器扩展 / manifest 校验）

## 5. 快速开始

### 5.1 在宿主程序中引用 Keystone

```csharp
using Keystone.Hosting;
using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;

// 1) 配置宿主：把「配置条目 → manifest / 源码」接起来
var options = new KeystoneHostOptions
{
    // 条目 → manifest（记录形态，由嵌入方构造——provides/inject 服务声明）
    ManifestProvider = entry => new PluginManifest(
        id: entry.Id ?? "greeter",
        version: "1.0.0",
        main: $"{entry.Name}.cs",
        dependencies: ["Keystone.Runtime"],
        provides: ["greeter"]),

    // 获取端抽象：manifest.Main 相对根目录解析（本地文件起步，可换远程分发实现 IPluginSource）
    PluginSource = new LocalPluginSource("plugins"),

    // 配置文件路径（CRUD 变更会防抖写回）
    ConfigFilePath = "keystone.yaml",
};

// 2) 启动宿主
var host = new KeystoneHost(options);
await host.StartFromFileAsync();

// 3) 运行你的应用……

// 4) 优雅关闭（全局 quiesce，逐插件收敛）
await host.ShutdownAsync();
```

> `LocalPluginSource` 按 `manifest.Main` 相对 `Roots` 解析（缺省回退 `{root}/{id}/{main}` 约定布局），它同时支持 `EnablePluginWatch()` 监听源文件变更自动冷重启。生产环境可替换为远程分发实现 `IPluginSource`；也可改用同步委托 `SourceProvider = entry => new PluginSource(entry.Id!, code)` 直接给源码。更精确的接线见 `docs/architecture/09-management-layer.md`。

### 5.2 编写第一个插件

插件 = 一个实现 `IPlugin` 的单文件类。下面这个插件在初始化时注册一个名为 `greeter` 的服务：

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
        // 从校验后的配置读取（默认值已补齐）
        var greeting = config.TryGetValue("greeting", out var g) ? g?.ToString() : "Hello";

        // 提供服务：key = 服务名，类型由接口白名单决定
        context.Provide<IGreeter>("greeter", new Greeter(greeting!));

        // 订阅事件（订阅随插件生命周期回收，无需手动退订）
        context.Subscribe<TaskCompletedFact>(e => context.Logger.LogInformation("done: {TaskId}", e.TaskId));

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // 只摘除自己注册的东西；回收由宿主 quiesce 收敛
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

### 5.3 编写插件 manifest

每个插件有一个清单，声明身份、依赖白名单与服务级依赖：

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

代码侧的 manifest 是 `PluginManifest` 记录（由 5.1 的 `ManifestProvider` 构造）；上面的 JSON 是其声明内容的惯用书写形态——仓库未内置 JSON 文件加载器，嵌入方可自行从 JSON / 数据库等来源构造记录（字段校验用 `ManifestSchemaValidator`）。

| 字段 | 维度 | 说明 |
|------|------|------|
| `dependencies` | 程序集编译白名单 | 插件代码能引用哪些程序集（Roslyn 引用集） |
| `provides` / `inject` | 服务级运行时依赖 | 插件提供/消费哪些服务（`inject` 未就绪 → `PENDING` 等待） |

`provides` / `inject` 里的名字是**服务名**（语义标识），类型在宿主接口白名单中声明。manifest 在启动期做 fail-fast 校验：id 唯一、依赖可达且无环、`provides` 类型在白名单内。

### 5.4 配置条目（YAML）

宿主通过一份 YAML「条目树」知道要加载哪些插件、注入什么配置、声明哪些依赖：

```yaml
- id: greeter
  name: GreeterPlugin          # 对应 5.1 中 ManifestProvider/PluginSource 的定位
  inject: [telemetry]          # 条目级依赖声明（与 manifest inject 并集合并）
  config:
    greeting: "你好"

- id: tools
  group:                       # 组 = 事务单元，可级联挂起/加载
    - id: rate-limit
      name: RateLimitPlugin
      config:
        limit: 100
```

顶层是条目列表，每个条目字段：`id`（稳定标识）、`name`（插件定位）、`config`、`disabled`、`inject`、`isolate`、`group`。

### 5.5 启动与关闭

见 5.1。`StartFromFileAsync()` 会：解析分层 YAML → schema 校验 → manifest 校验 → 建根 context → 并行加载（依赖门控天然实现拓扑序）→ 就绪。`ShutdownAsync()` 执行全局 quiesce（逐插件收敛 + 关闭超时审计）。

## 6. 插件 API 速查（IPluginContext）

插件只能通过 `IPluginContext`（简称 `ctx`）访问能力，不直接 new 宿主内部对象、不碰 DI 根容器。

### 服务

```csharp
ctx.Provide<T>("service-name", instance);   // 提供服务（属主 = 本插件）
T svc = ctx.Get<T>("service-name");          // 依赖未就绪 → PENDING 等待后注入
T? opt = ctx.TryGet<T>("service-name");      // 可选服务
ctx.Set<T>("service-name", instance);        // 原位更新（属主校验）
Lazy<Task<T>> lazy = ctx.GetLazy<T>("x");    // 方法级延迟注入
```

### 事件（五种模式）

```csharp
ctx.Subscribe<TEvent>(e => { /* emit：监听不阻塞 */ });
ctx.SubscribeParallel<TEvent>(async e => { /* parallel：并发 */ });
ctx.SubscribeSerial<TEvent>(async e => { /* serial：首个 bail 短路 */ return null; });
ctx.SubscribeBail<TEvent>(e => { /* bail：首个非空短路 */ return null; });
ctx.SubscribeWaterfall<TEvent>(async (e, next, ct) => { /* 包裹 next 链 */ await next(); });
ctx.EmitFireAndForget<TEvent>(e);            // fire-and-forget 发布
```

### 计时器（随插件生命周期自动回收）

计时器是 `Keystone.Sdk` 包的扩展方法（命名空间 `Keystone.Sdk.Timers`）：

```csharp
using Keystone.Sdk.Timers;

ctx.SetTimeout (async () => { }, TimeSpan.FromSeconds(1));
ctx.SetInterval(async () => { }, TimeSpan.FromMinutes(1));
ctx.Throttle (async () => { }, TimeSpan.FromSeconds(2));
ctx.Debounce (async () => { }, TimeSpan.FromSeconds(2));
```

### 日志

```csharp
ctx.Logger.LogInformation("plugin started");
```

日志走 `ctx.Logger`（category = `{域}/{插件 ID}`），**禁止直接 `Console`**。

## 7. 中间件管道（IMiddleware）

把插件写成「中间件」，挂载在能力域 context 的管道上：

```csharp
using Keystone.Runtime.Context;
using Keystone.Runtime.Pipeline;

public sealed class TimingMiddleware : IMiddleware
{
    public string Id => "timing";
    public int Order => 0;                       // 升序执行
    public async Task InvokeAsync(IPluginContext ctx, RequestDelegate next)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await next(ctx);                          // RequestDelegate 携 ctx；不调 next 即短路（否决请求）
        sw.Stop();
        ctx.Logger.LogInformation("took {Ms}ms", sw.ElapsedMilliseconds);
    }
}
```

`await next(ctx)` 之前是 before，之后是 after；不调用 `next` 直接返回 = 短路（waterfall 否决）。管道与 actor 同生命周期，节点（插件）可热重载替换（`SwapPipelineAsync` 原子换链）。

## 8. 依赖门控与多实例

- **依赖门控**：插件在 `inject` 声明的服务全部可用前保持 `PENDING`；服务提供方卸载/替换 → 依赖方自动 reload/unload。这是「等依赖就绪再启动」的核心机制。
- **多实例隔离**：同一能力域可 spawn 多个 actor，各自独立 context（事件/Effect/管道独立）；服务经共享 store + 键隔离（`realm` ∈ 默认共享 / `#entryId` 私有 / `@label` 命名共享）。
- 插件无状态、状态外置到 context，因此热重载不丢状态、多实例天然隔离。

## 9. 热重载

- **配置变化（仅 config 变）**：`UpdatePluginAsync` / `ApplyConfigAsync` → 同 ALC 原地热更新（不重编译、不换 ALC）。
- **结构变化（name/inject/isolate/跨组）**：`ReloadPluginAsync` → 冷重启（重编译 + 换 ALC + quiesce 旧实例）。
- 文件监听：`host.EnableConfigWatch()`（配置变）与 `host.EnablePluginWatch()`（源文件变，需 `LocalPluginSource`）会自动按上述分级触发，失败保留「最后好树」。

## 10. 配置进阶

- **分层叠加**：多 YAML 层按序叠加（base → profile → 用户 patch → 运行期 overlay），以条目 `id` 为主键合并。
- **静态插值**：`!!env NAME`、`!!file path`（内容递归插值 + 环检测）。
- **schema 校验**：条目可声明 `configSchema`，经源生成器校验（必填/未知字段 fail-fast）+ 默认值补齐后注入 `InitializeAsync`。
- **禁用不删**：`disabled: true` 挂起条目（保留在树中，不加载）；父组挂起 → 整棵子树跟随。

## 11. 测试

```bash
dotnet test cordis-csharp.slnx     # 500+ 单元测试（M0–M13 + 多轮审计）
```

插件作者应为自己的插件写单测：用真实 `KeystoneHost` + 内存配置启动，或在单测中直接构造 `ContextFacade` 验证 `Provide` / `Subscribe` / `DisposeAsync` 行为。注意 AOT 纪律（规则 0）对插件代码同样适用。

## 12. 下一步

- 架构总览：[docs/architecture/01-overview.md](../architecture/01-overview.md)
- 插件模型：[docs/architecture/02-plugin-model.md](../architecture/02-plugin-model.md)
- 上下文与作用域链：[docs/architecture/03-context.md](../architecture/03-context.md)
- 管道：[docs/architecture/04-pipeline.md](../architecture/04-pipeline.md)
- 配置层：[docs/architecture/08-configuration-layer.md](../architecture/08-configuration-layer.md)
- 插件 SDK 全集：[docs/architecture/10-plugin-sdk.md](../architecture/10-plugin-sdk.md)
- 设计决策（ADR-0001~0018）：[docs/decisions/](../decisions/)
- 贡献指南：[CONTRIBUTING.md](../../CONTRIBUTING.md)

有问题？见 [SUPPORT.md](../../SUPPORT.md)。🫘
