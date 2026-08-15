---
type: architecture-doc
tags: [cordis-csharp, architecture, cordis-gap, review]
created: 2026-08-15
---

# 16 — Cordis 功能差距复核（实现后）

> 对照 vendored Cordis 源码（`~/Projects/deepseek-harness/vendor/cordis/src/`，9 模块 2693 行）与 Keystone **当前实现**（M0-M13 + P14-P22，209 测试）的差距复核。
> 07-cordis-migration-gap 是**设计期**差距分析（0 覆盖/7 部分/0 未覆盖）；本文是**实现后**复核——验证设计期映射是否真正落地，找出仍缺的实现缺口。
> 方法：4 个独立子代理并行审计（Events/Registry+Service/Reflect+Context/Logger+Utils）+ 主代理独立验证关键点。
> 执行状态：**🔴 3 高危已全部闭合（P24 G-C1 / P25 G-C2 / P26 G-C3，见 14-implementation-log）**；中危待排期。

## 1. 综合差距清单（按严重度）

### 🔴 高危（真实功能缺口，设计已承诺但实现未落地）

| # | 差距 | Cordis 依据 | Keystone 现状 | 影响 |
|---|------|-----------|--------------|------|
| G-C1 | **插件配置注入缺失** | Fiber 启动前 `resolveConfig`（fiber.ts:641-645）；Config schema 校验 + 默认值补齐 | ~~`PluginRuntime` 调 `InitializeAsync(ctx, new Dictionary<string,object?>())`——空字典；`EntryOptions.Config` 从未传递~~ **✅ 已闭合（P24）**：Host 经 ConfigSchemaProvider + ConfigResolver 校验/补齐后传入；无 schema 直传；校验失败 = 插件 FAILED（隔离语义） | 配置驱动架构核心承诺兑现 |
| G-C2 | **依赖恢复 re-arm 缺失** | 依赖消失 → 依赖方卸载；依赖重现 → 自动重启（fiber.ts:625-639 epoch 驱动） | ~~依赖消失 → `StopCoreAsync()` → DISPOSED；无重现订阅；`RestartAsync` 只接受 Active/Failed~~ **✅ 已闭合（P25）**：依赖重现（Available=true）→ Disposed 自动 StartAsync；订阅生命周期区分（自动卸载保留/显式停止销毁，防 ALC 泄漏） | 热更新链闭合（ADR-0007 决策 3 对称） |
| G-C3 | **服务值卸载注销缺失** | provide 返回 disposer，fiber 卸载自动注销 + 唤醒依赖（reflect.ts） | ~~`ContextFacade.Provide` 写 root store 无 Remove；插件卸载只摘 manifest 声明名~~ **✅ 已闭合（P26）**：`IServiceStore.Remove`（属主校验）+ ContextFacade 属主追踪 + PluginRuntime 卸载钩子 RemoveOwnedServices——运行期 Provide 值卸载后注销，依赖方不再拿陈旧值 | 卸载语义完整（双轨补齐） |

### 🟡 中危（语义偏差或文档声称未落地）

| # | 差距 | Cordis 依据 | Keystone 现状 | 影响 |
|---|------|-----------|--------------|------|
| G-C4 | **serial/bail 的 false 短路语义偏差** | `isBailed` 排除 false/null/undefined（events.ts:13-15） | ~~`result is not null`（EventBus.cs:96/116）——`false` 被误判为短路~~ **✅ 已闭合（P27）**：`IsBailed` 对齐（null/false 不短路，0/空串短路） | 返回 false 的监听器不再提前截断链 |
| G-C5 | **M4 方法级延迟注入未落地** | `@Inject` 方法调用等到服务可用（registry.ts:45-59） | ~~12 文档声称 `Lazy<Task<T>>`，实现 grep 零命中；`IPluginContext.Get` 同步抛 GatingServiceNotFound~~ **✅ 已闭合（P29）**：`GetLazy<T>` 返回 `Lazy<Task<T>>`——首次访问解析（Lazy 缓存，只解析一次） | 文档声称已映射但实现缺失 → 已补齐 |
| G-C6 | **waterfall 发布者注入 terminal 缺失** | 发布者注入最内层 next，返回最外层值（events.ts:234-243） | ~~`PublishWaterfallAsync` terminal 硬编码 `Task.CompletedTask`（EventBus.cs:144）~~ **✅ 已闭合（P28）**：terminal 可注入 + 返回值；监听器不调 next → 否决（null） | "内置行为可被否决"核心用法可用 |
| G-C7 | **日志导出器抽象缺失** | Exporter 可插拔 sink（多导出器/per-exporter formatters/levels/maxLength，logger.ts:41-131） | 仅内建 RingBuffer + `GetSnapshot()`；无第二输出挂载点；无终端 sink（日志实际不可见） | 观测性缺口：日志只能内存快照，无法输出 |
| G-C8 | **热更新触发缺失** | 文件监听 → 原子替换/重载（09 承诺；fiber.ts update/restart） | `PluginLoader.ReloadAsync` 存在但**无 FileSystemWatcher 接线**；`KeystoneHost` 无 `ReloadPlugin`/`UpdatePlugin` API（09 §5 表格承诺未实现） | 热更新原语有，触发机制无 |

### 🟢 低危（DX 或已接受丢弃）

| # | 差距 | 说明 |
|---|------|------|
| G-C9 | 事件 internal/* 拦截事件缺失 | Cordis 的 internal/listener、dispatch、update、config 总线事件无对应物（以 .NET 事件/拦截器替代）——设计取向差异，记录 |
| G-C10 | 监听器拿不到发布 context | Cordis this=ctx + 自定义 filter；Keystone handler 仅收 TEvent（scope 作过滤基准）——C# 无 this 概念，可接受 |
| G-C11 | 日志级别过滤无全局默认 | Cordis 默认 INFO 三级阈值；Keystone `IsEnabled` 无 override 恒 true——小幅语义差 |
| G-C12 | ANSI 彩色输出缺失 | 文档判"无需对应"（L8），但连默认 Console sink 都无——与 G-C7 合并处理 |
| G-C13 | accessor/mixin 缺失 | G16 已接受丢弃（10-plugin-sdk §8）——无差距 |
| G-C14 | composeError 堆栈增强 | L5 显式弃用（.NET 原生 async 栈更好）——无差距 |

## 2. 已确认等价/更强的面（非差距）

- **EffectRegistry**：逆序 + 幂等 + AsyncLocal 父子树 + 诊断元数据——**比 DisposableList 更强**（审计确认零差距）
- **五种事件分发主骨架**：emit/parallel 错误聚合/顺序控制等价覆盖
- **G9 check 谓词**：显式弃用，ManifestValidator 静态校验承接（✅）
- **G6 intercept**：IOptions 命名选项（✅，.NET 语义承接）
- **H2/H3**：MountAsync/IContextInterceptor 落地有测试（✅）
- **getTraceable**：TraceContext（Activity.Current）等价覆盖（✅）
- **事件 once/prepend/Global/Scope**：选项齐全（✅）

## 3. 差距根因分析

1. **配置注入（G-C1）是最大缺口**：M3（ConfigResolver）阶段只测了配置层本身，**未做宿主级接线验收**——与 P21 发现的服务解析缺口同源：**各能力域实现了，但"接起来"的验收缺位**
2. **G-C2/G-C3 同源**：依赖门控的"服务消失→卸载"实现了，"重现→重启"和"值注销"遗漏——ADR-0007 决策 3 的对称性未完成
3. **G-C4/G-C6 同源**：事件语义的**边界细节**（false 判定、terminal 注入）在实现期未逐条对照 Cordis 源码——语义映射停留在文档层

## 4. 建议工作计划

| 优先级 | 差距 | 建议方案 | 工作量 |
|--------|------|---------|--------|
| P0 | G-C1 配置注入 | ✅ 已执行（P24，14 §7.24/ID-20）：`KeystoneHostOptions.ConfigSchemaProvider` + `ConfigResolver` 校验/默认值 → `InitializeAsync`；无 schema 直传；校验失败 = 插件 FAILED（隔离） | 中 |
| P0 | G-C2 依赖 re-arm | ✅ 已执行（P25，14 §7.25/ID-21）：依赖重现自动重启；订阅生命周期区分（自动卸载保留/显式停止销毁）；StartCoreAsync 接受 Disposed 恢复路径 | 中 |
| P0 | G-C3 服务值注销 | ✅ 已执行（P26，14 §7.26/ID-22）：`IServiceStore.Remove` + ContextFacade 属主追踪 + 卸载钩子；运行期值注销，依赖方不再拿陈旧值 | 小 |
| P1 | G-C5 M4 延迟注入 | `IPluginContext.GetLazy<T>` / `Task<T>` 延迟解析（Lazy 语义） | 中 |
| P1 | G-C4 事件 false 语义 | `EventBus` serial/bail 判定改 `value is null`（对齐 isBailed：false 不短路） | 小 |
| P1 | G-C6 waterfall terminal | `PublishWaterfallAsync` 支持注入最内层 next + 返回最外层值 | 小 |
| P1 | G-C7/G-C12 日志输出 | `IExporter` 抽象 + Console sink（含可选 ANSI 配色）挂到 LoggerProvider 外 | 中 |
| P2 | G-C8 热更新触发 | `FileSystemWatcher` 接线 + `KeystoneHost.ReloadPlugin/UpdatePlugin` | 中 |

## 5. 结论

- **核心骨架已等价覆盖**：生命周期状态机、quiesce、五分发事件、服务门控、Effect、Trace、配置层、加载层——设计期"7 项部分覆盖"的**主语义**均已落地
- **真实差距 8 项**（3 高 + 5 中）：**🔴 3 高危已全部闭合（P24-P26）**——配置注入（G-C1）、依赖 re-arm（G-C2）、值注销（G-C3）；🟡 5 中危待排期（事件 false 语义/延迟注入/waterfall terminal/日志导出器/热更新触发）
- **根因**：M3/P3 阶段的"功能独立测试通过"未做宿主级接线验收；事件语义映射停留在文档层未逐条对照源码

## 关联

- 07-cordis-migration-gap（设计期差距）、11-gap-register（差距状态矩阵）、12-cordis-semantics-mapping（语义映射）
- 14-implementation-log（执行记录：本复核记 P23）
