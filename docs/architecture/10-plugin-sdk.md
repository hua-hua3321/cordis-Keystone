---
type: architecture-doc
tags: [cordis-csharp, architecture, plugin-sdk, dx]
created: 2026-08-15
---

# 10 — 插件 SDK

> 插件作者的接口面全集：插件入口、配置注入、服务/事件/计时器、manifest schema、模板工程。
> 本文收敛 01-overview §7 遗留待定"插件 SDK 体验"（来源：补充排查 N1/N3/N4/N6）。

## 1. SDK 定位

插件 SDK = 宿主显式暴露给插件作者的 API 面（`cordis-runtime` 程序集的一部分）。约束：

- 插件只能实现/使用 SDK 声明的接口（接口白名单，02-plugin-model §2）
- SDK 代码遵守 AGENTS.md 规则 0（AOT 就绪：无运行时反射、显式序列化契约）
- SDK 接口分层提前设计，否则插件写起来难受（02-plugin-model §2）

宿主嵌入形态：宿主程序引用 `cordis-runtime`，通过管理层 hosting API（09-management-layer §5）启动。

## 2. 插件入口（IPlugin）

```csharp
public interface IPlugin : IAsyncDisposable
{
    // 插件名（默认 = manifest id）
    // 配置注入：经 schema 校验后的完整配置（08 §5），apply 永远收到完整配置
    Task InitializeAsync(IPluginContext ctx, IReadOnlyDictionary<string, object?> config);

    // DisposeAsync：走 ADR-0005 quiesce 五步闸门（逆序并发 disposer + 全 settle）
    // 插件自身只需摘除自己注册的东西，回收由宿主收敛
}
```

- `config` 是**校验后**的配置（默认值已补齐），插件不再自行解析配置
- 宿主侧生命周期状态机（PENDING/LOADING/ACTIVE/FAILED/UNLOADING/DISPOSED）叠加在 IPlugin 之上，不进 SDK（ADR-0005）

## 3. 中间件（IMiddleware）

```csharp
public interface IMiddleware
{
    string Id { get; }                        // 插件 ID
    int Order { get; }                        // 管道顺序
    Task InvokeAsync(IPluginContext ctx, RequestDelegate next);
}
```

形状 A 定案（04-pipeline §2）：`await next()` 之前 = before，之后 = after；不调用 next 直接返回 = 短路。

## 4. IPluginContext（插件侧门面）

```csharp
public interface IPluginContext
{
    // 服务：按服务名解析（ADR-0007：key = 服务名）
    T Get<T>(string serviceName);                       // 依赖未就绪 → PENDING 等待后注入；按本条目 isolate 生效 realm 解析（P57）
    T? TryGet<T>(string serviceName);                   // 可选服务（对齐 ctx.get 可选读取）；同名异域不串

    // 服务注册：本插件提供服务（属主 = 本插件，set 属主校验，03-context §2.3）
    void Provide<T>(string serviceName, T instance);    // 写入生效 realm 键；manifest.provides 声明须 init 期兑现（值即注册，P57-T4）

    // 事件：五种分发模式（ADR-0006）
    IDisposable Subscribe<TEvent>(Action<TEvent> handler);              // emit
    IDisposable SubscribeParallel<TEvent>(Func<TEvent, Task> handler);  // parallel
    IDisposable SubscribeSerial<TEvent>(Func<TEvent, Task<object?>> handler); // serial（首个 bail 短路）
    IDisposable SubscribeBail<TEvent>(Func<TEvent, object?> handler);   // bail（首个非空短路）
    IDisposable SubscribeWaterfall<TEvent>(WaterfallHandler<TEvent> handler); // 包裹 next 链

    // 计时器：随插件 fiber 回收（disposal-aware，对齐 @cordisjs/plugin-timer）
    ITimerHandle SetTimeout(Func<Task> callback, TimeSpan delay);
    ITimerHandle SetInterval(Func<Task> callback, TimeSpan period);
    ITimerHandle Throttle(Func<Task> callback, TimeSpan window);
    ITimerHandle Debounce(Func<Task> callback, TimeSpan window);

    // 日志：命名日志（category = 插件 ID），禁止直接 console
    ILogger Logger { get; }
}
```

**计时器随插件生命周期回收**（补充排查 N3）：所有计时器注册进当前 fiber，插件卸载（quiesce）时自动取消/排空——插件作者不需要手动清理，与 Cordis `@cordisjs/plugin-timer` 语义一致。

**服务级选项消费定式**（CA-12，P60，intercept 对应物）：宿主经 `KeystoneHostOptions.ServiceOptions` 配服务选项（服务名 → 选项字典；日志首例 `"logger"`）。服务收到选项包后**自行绑定**：插件侧 `Options.Create<T>(...)` 编译期泛型（规则 0 第 5 条），框架不做反射式绑定。日志无需插件操心——`context.Logger` 已按 `levels.{category}` / `defaultLevel` 三级阈值过滤（RingBufferLoggerProvider 接线）。

## 5. 事件订阅与插件生命周期绑定

- `Subscribe*` 返回的 disposer 也可手动调用；不手动调用则随插件卸载自动摘除（03-context §7）
- 事件监听带 context filter（03-context §5 实现形状），防跨实例泄漏

## 6. manifest 完整 schema

```json
{
  "id": "plugin-fs-local",          // 必填，全局唯一（子容器分组键）
  "version": "1.0.0",               // 必填，语义化版本
  "main": "FsLocalPlugin.cs",       // 必填，插件源文件（文件式应用脚本或 DLL 轨）
  "dependencies": ["cordis-runtime", "cordis-contracts"],  // 程序集编译白名单（脚本形态 ↔ #:package）
  "provides": ["fs"],               // 服务级：提供的服务名
  "inject": ["llm", "telemetry"],   // 服务级：依赖的服务名（PENDING 等待，ADR-0007）
  "skills": ["skill://git-workflow/SKILL.md"],  // 技能包：SEP-2640 skill:// URI（ADR-0008 决策 3）
  "configSchema": "fs-plugin-config" // 配置 schema 声明（08 §5，源生成器校验）
}
```

| 字段 | 说明 |
|------|------|
| `skills` | **插件技能包**（ADR-0008 决策 3）：SEP-2640 跨厂商技能格式（`skill://index.json` + `SKILL.md`），经 MAF `AgentMcpSkillsSource` 消费；`skill://` URI 或 MCP resource template，非 cordis-csharp 私有格式 |
| `main` | 插件源文件：默认 .NET 10 文件式应用脚本（单文件 .cs + 顶层语句），复杂插件走预编译 DLL 轨（T11，00-tech-stack） |

manifest 校验器（启动期 fail-fast）：id 唯一、version 合法、依赖可达且无环、provides 类型在白名单内、main 可编译。

## 7. 模板工程与示例

- **`dotnet new cordis-plugin`**：脚手架生成插件骨架（manifest + 单文件 .cs + 测试工程），对齐 Cordis `create-cordis`（补充排查 N4）
- 示例插件库：fs-local、llm-proxy、telemetry（观察者）、auth（决策型 serial 事件）、rate-limit（管道）——每个示例对应一种插件形态，作为 SDK 用法参考
- 插件调试：Roslyn 内存编译带 embedded PDB + source link（02-plugin-model §9），调试器可进插件代码

## 8. SDK 约束清单（插件作者义务）

| 约束 | 说明 |
|------|------|
| 只经 ctx 访问能力 | 不直接 new 宿主内部对象、不碰宿主 DI 根容器 |
| 状态放 context | 插件无状态（D6），热重载不丢状态 |
| 日志走 ctx.Logger | 不直接 console（05-reliability §5） |
| 副作用可逆 | 每个注册必有 disposer（对齐 Cordis 实践规则） |
| 遵守 AOT 规则 | 插件代码同样不写运行时反射/动态生成（规则 0，插件加载层除外） |
| 配置声明 schema | 否则无法通过校验（08 §5） |

### 8.1 已接受丢弃引用表（G16 防回归）

> Cordis 动态能力中经显式决策**不做**的项（见 12-cordis-semantics-mapping §2/§7/§16 与 11-gap-register）。SDK 面**不提供**以下 API，防止实现期"顺手补上"造成语义漂移：

| 已接受丢弃 | 决策依据 | 替代物 |
|-----------|---------|--------|
| `ctx.accessor`（计算属性） | G16 / 12 §16 | 普通属性 + IFeatureCollection |
| `ctx.mixin`（成员混入） | G16 / 12 §16 | 接口默认实现 / 门面方法 |
| `ctx.trace`/`bind`（运行期 proxy 重绑定） | G16 / 12 §7.1 H1 | Activity.Current + CallerMemberName |
| `intercept` 通用语义（internal/get/set 瀑布） | G6 / ADR-0010 | IContextInterceptor（H3，P2 已实现） |
| `check` 谓词（服务可用门控回调） | G9 / ADR-0010 | 依赖门控（ADR-0007 PENDING） |

## 9. 与相邻文档的关系

| 文档 | 关系 |
|------|------|
| 02-plugin-model.md | 插件定义/加载/回收的接口化落点 |
| 04-pipeline.md | IMiddleware 形状 A 定案 |
| 03-context.md | IPluginContext 背后的作用域链/事件分层 |
| 06-contracts.md | 事件 payload 强类型 + 分发模式 |
| 08-configuration-layer.md | config 注入的 schema 校验来源 |
| 09-management-layer.md | 宿主嵌入形态（hosting API） |
| ADR-0005/0006/0007 | 生命周期状态机/分发模式/依赖门控的 SDK 呈现 |
| ADR-0008 | AI 能力域组合：`skills` 字段（SEP-2640 技能包）、llm/mcp/workflow 能力域适配器组合 MAF |
