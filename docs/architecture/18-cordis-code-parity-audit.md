---
type: architecture-doc
tags: [cordis-csharp, architecture, gap-tracking, code-parity]
created: 2026-08-16
---

# 18 — Cordis 代码级对照审计与实现提案（实现后第二轮）

> **审计方法**：不采信任何文档状态表（11/16/17 的 ✅ 标注一律不作为依据），直接比对两侧源码——
> Cordis 侧 = vendored 4.0.1 全部 8 个核心文件（`src/{context,events,fiber,reflect,registry,service,utils,logger}.ts`）
> + `node_modules/@deepseek-ai/cordis-plugin-loader/src`（EntryTree/Group/isolate/config 全 5 文件）
> + `node_modules/@deepseek-ai/cordis-plugin-include/src`（文件管线）；
> Keystone 侧 = 本仓 `src/` 六工程逐项 grep/读码验证对应物是否存在、语义是否等价。
> **审计基线**：`~/Projects/deepseek-harness/vendor/cordis/`（2026-08-16，17 审计 30 项闭合后）。
>
> **本文性质**：差距登记 + **实现提案集（待人工决策）**。每项给出建议方案但不实施；
> 审核后按 §5 决策矩阵分流（实施 → 进 13/14 计划；弃用 → 补 ADR；延期 → 11-gap-register 挂账）。

## 1. 审计口径

- 可移植行为点 ≈ 95 个（核心类成员 + loader CRUD 事务 + include 文件管线）
- 三档判定：**A 未实现**（Cordis 有代码，Keystone 全仓无对应）/ **B 部分·语义差异**（有对应物但行为不同）/ **C 等价**（代码级验证一致，§4 抽样凭证）
- 结论：A 类 12 项 + B 类 6 项 ≈ 全部行为点的 18%；核心运行时（事件五模式/生命周期/管道/门控/上下文/事实持久化/热更新主链）代码级等价

## 2. A 类：未实现（12 项，含实现提案）

### CA-1 isolate 服务隔离接线（P1，M）

| 列 | 内容 |
|----|------|
| Cordis | `ctx.isolate(name, label)`：子作用域 shadow 原型链（context.ts:121-135）+ `reflect.notify` 按 isolate 相等过滤投递（reflect.ts:314-320）+ loader `config/isolate.ts`（条目级声明入口） |
| Keystone 现状 | `EntryOptions.Isolate` 仅被 EntryParser.cs:92 解析、EntrySerializer.cs:41 序列化、ConfigDiffer.cs:70 当结构键——**Runtime/Hosting 零消费，配置写了不生效** |
| 风险 | 已承诺未兑现（3 §2.2）；多实例同服务名场景无法隔离 |

**实现提案**：
- `ServiceStore` 键扩展：`Dictionary<string, Entry>` → `Dictionary<(string Name, string Scope), Entry>`；默认 Scope = `""`（共享根域）。`Set/TryGet/Get/Remove` 增加 `string? scope` 参数（缺省 null = 调用方 context 的生效域）
- `ContextFacade` 增 `Isolate(string serviceName, string? label = null)`：返回子 context，携带 `{serviceName → label ?? 新 Guid}` 的隔离映射（沿 Parent 链继承合并——对齐 Cordis shadow 原型链）
- 解析规则：查 (name, 本 context 生效 label)；未命中且 label 非默认 → 回落 (name, "") 共享域（对齐"隔离域优先、共享兜底"）；同域 rebind 语义不变（G14）
- 宿主接线：`LoadEntryAsync/ReloadPluginAsync/MountAsync` 的 context 工厂（KeystoneHost.cs:342/374/561 三处 `id => new ContextFacade(id, _rootContext)`）改为按 `entry.Isolate` 预 isolate 的子 context
- **TDD**：红用例——两个插件 provide 同名服务（各自 isolate 组）互不可见；无 isolate 时第二个 provide 报 ServiceAlreadyRegistered（现状保持）；isolate 域内 TryGet 未命中回落共享域
- 开放问题：notify 过滤是否需要（Cordis 事件投递也按 isolate 过滤）→ 建议首期只做服务解析隔离，事件过滤保持现状（G15 scope 链已覆盖主场景），审核时定

### CA-2 插件源文件 watcher（P2，S）

| 列 | 内容 |
|----|------|
| Cordis | 08 §6 第一触发源：源文件变更 → 重编译（Cordis 由 plugin-loader/HMR 承担） |
| Keystone 现状 | 全仓只有 P50 的**配置文件** watcher（ConfigFileWatcher）；插件目录监听无。`ReloadPluginAsync`（冷重启管线）已具备，只缺触发器 |

**实现提案**：`PluginFileWatcher`（Hosting 新文件，复用 ConfigFileWatcher 的防抖模式）——监听 `LocalPluginSource` 的 roots；文件变更防抖后按 `manifest.Main` 匹配条目 → `ReloadPluginAsync(id)`；`EnablePluginWatch()` 宿主 API（与 EnableConfigWatch 对称，opt-in）。**TDD**：临时目录写插件文件 → PluginReloading 事件 + loader 换新（Assembly 不同）。

### CA-3 组级事务：并行应用 + 失败聚合 + 逆序回滚（P1，M）

| 列 | 内容 |
|----|------|
| Cordis | group.ts:59-118：`Promise.allSettled` 并行应用；单失败抛原因、多失败 AggregateError；回滚 = 逆序 remove 新建 + 按旧配置重建；回滚失败再聚合 |
| Keystone 现状 | `ApplyConfigAsync`（KeystoneHost.cs）顺序应用、首错直接抛出中断、**无回滚**；"Disposal owns termination"边界（树卸载中不回滚）未建模 |

**实现提案**：`ApplyConfigAsync` 组感知化——同组条目收集后 `Task.WhenAll` 并行执行；失败聚合（1 个抛原因，多个抛 `AggregateException`，对齐 Cordis）；回滚路径：逆序 `RemoveEntryAsync(本次新建)` + 按旧树重建失败的组；加树卸载中短路（宿主 `_shutdown` 已有，补充组级判据）。**TDD**：组内两新增一失败 → 两者的 CreateEntry 均回滚；双失败 → AggregateException 含两内因。

### CA-4 EntryTree.update 组合语义（P1，S）

| 列 | 内容 |
|----|------|
| Cordis | tree.ts:114-143：一次调用同时改条目选项 + 跨组移动 + position；失败双段回滚（移动回滚 + 选项回滚），回滚失败 AggregateError |
| Keystone 现状 | 只有分离的 `MoveEntryAsync`（仅移动；回滚只回根不回原位置）和 `UpdatePluginAsync`（仅 config） |

**实现提案**：新宿主 API `UpdateEntryAsync(string id, EntryOptions options, string? parent = null, int? position = null)`——语义：结构未变且仅 config 变 → 走热更新瀑布；结构变 → 冷重启；跨组 → 记录 (源组, 原下标) 后移动，任一步失败按记录回插原位置。`MoveEntryAsync` 保留为纯移动便捷面。**TDD**：移动 + config 同改一次成功；config 失败 → 条目回到原组原下标。

### CA-5 运行期 patch 注入（P2，S）

| 列 | 内容 |
|----|------|
| Cordis | include/index.ts:58-130：`Config.patches` 读文件后插入组/追加根 + 按 id 覆盖字段（name 不匹配跳过 + warn） |
| Keystone 现状 | 无对应（`PatchContextAsync` 是上下文补丁瀑布，另一语义——命名易混淆需在 XML doc 注明） |

**实现提案**：`KeystoneHostOptions.ConfigPatches: IReadOnlyList<EntryPatch>?`；`EntryPatch(string? GroupId, IReadOnlyList<EntryOptions>? Insert, IReadOnlyDictionary<string, EntryOptions>? Overrides)`；`StartAsync` 解析后应用（Config 层纯函数 `EntryPatcher.Apply(tree, patches)`，复用 FindEntry/ReplaceEntry）。用途：部署期环境差异化注入而不改文件。**TDD**：插入组/插入根/覆盖 config/name 不匹配告警跳过。

### CA-6 initial 引导接线（P1，S）

| 列 | 内容 |
|----|------|
| Cordis | include Service.init：ENOENT 且有 `initial` → 先写初始文件再读（include/index.ts init 段） |
| Keystone 现状 | `ConfigFileWriter.EnsureInitialAsync` 是**死代码**（宿主零调用）；`KeystoneHostOptions` 无 initial 选项 |

**实现提案**：`KeystoneHostOptions.InitialEntries: IReadOnlyList<EntryOptions>?`；`StartAsync(string)` 入口：ConfigFilePath 已配置且文件不存在且 InitialEntries 非空 → `_configWriter.EnsureInitialAsync(InitialEntries)` 后继续正常加载。**TDD**：无文件 + initial → 启动后文件存在且插件加载；文件已存在 → initial 被忽略。

### CA-7 readonly 优雅降级（P2，S）

| 列 | 内容 |
|----|------|
| Cordis | include `checkAccess`：`access(W_OK)` 预检失败 → readonly 标记 → 后续写跳过（不抛） |
| Keystone 现状 | 只有占用重试（0x80070020/05 × 10 次退避）→ 超限抛 `ConfigProviderFailed`；无只读模式 |

**实现提案**：`ConfigFileWriter` 增 readonly 状态——首次写失败且错误码为拒绝访问类（0x80070005/EACCES 语义）→ 置 readonly + 一次性警告回调（新事件或 Action 注入），后续 `ScheduleWrite/WriteAsync` 直接跳过；`FlushAsync` 在 readonly 下返回完成。注意与 CA-3 交互：写回失败不阻断关闭已有 catch，语义兼容。**TDD**：以子类注入故障模拟拒绝访问 → 第二次写不再抛、回调触发一次。

### CA-8 JSON 配置格式（弃用候选，S）

| 列 | 内容 |
|----|------|
| Cordis | include 按扩展名支持 yaml/json/模块三种 writable 格式 |
| Keystone 现状 | 仅 YAML（**ADR-0014 明确 P0 YAML-only**——这是设计范围而非遗漏） |

**建议**：补一行 ADR-0014 备注或新 ADR 显式记录"JSON 延期/弃用"；若要做：`StartAsync` 按扩展名分流到 `JsonDocument` → 同构 `Dictionary<string,object?>` 树 → 复用 EntryParser 后半管线（插值 tag 仅 YAML 有，JSON 走不了 !!env——需声明限制）。**建议弃用**（与静态插值能力冲突，得不偿失），审核时定。

### CA-9 计时器不随插件卸载回收（**P0 正确性**，S）

| 列 | 内容 |
|----|------|
| Cordis | 计时器是 effect（注册进 fiber `_disposables`）——fiber 卸载自动清除 |
| Keystone 现状 | `TimerExtensions` 四件套（SetTimeout/SetInterval/Throttle/Debounce）存在，但 `TimerHandle` **不注册进任何 effect/disposal 链**——插件卸载后回调继续跑（IPluginContext.cs:8 注释自认"随插件生命周期回收，P3 补齐"未兑现） |

**实现提案**：`TimerHandle` 构造时 `_ctx.Context.Effect(() => DisposeAsync().AsTask(), $"timer:{label}")`；`DisposeAsync` 幂等（已有 `_disposed`）。一行级修复 + TDD：挂计时器 → `host.RemoveEntryAsync` → 回调不再触发。**这是当前唯一会产出"僵尸副作用"的正确性 bug，建议最优先。**

### CA-10 组条目 CRUD 级联（**P0 正确性**，M）

| 列 | 内容 |
|----|------|
| Cordis | `EntryTree.create` 组条目 → 加载整子树并 await 收敛；`remove` → 组内逐子卸载再删（tree.ts:97-112 + group.ts:48-57） |
| Keystone 现状 | `CreateEntryAsync` 对组只发 EntryInit **不加载 children**（KeystoneHost.cs:239-243）；`RemoveEntryAsync` 只 Dispose 精确匹配 id 的**叶子**插件——**直接删除组条目会留下整组孤儿运行插件**（仅 `ApplyConfigAsync` 路径因 ConfigDiffer 扁平化 diff 间接弥补，直接调 RemoveEntryAsync("group") 则泄漏） |

**实现提案**：
- `RemoveEntryAsync`：目标是组 → `EnumerateLeaves(entry.Group)` 逆序逐叶 Dispose（各发 EntryDisposing）→ 再从树移除组
- `CreateEntryAsync`：目标是组 → 按 `EnumerateActiveLeaves(children)` 逐叶 LoadEntryAsync（await 收敛，失败聚合可选——首期沿用首错中断，CA-3 落地后升级）
- **TDD**：红用例——加载含 2 子插件的组后 `RemoveEntryAsync(组id)` → 两插件 loader 均 Dispose（可用 ALC 卸载断言或 GetPluginState 抛错验证）；`CreateEntryAsync(组)` → 子插件均 Active

### CA-11 `cordis:` 内建插件命名空间（弃用候选，S）

| 列 | 内容 |
|----|------|
| Cordis | loader import 按前缀分发内建插件（不落盘的框架自带插件） |
| Keystone 现状 | 无前缀处理；框架能力（loader/include 等价物）由宿主 C# 直接提供，**不是可加载条目**——形态差异 |

**建议**：显式弃用 + 记录理由（Keystone 的"内建"= 宿主组合根直接构造，无动态分发需求；若未来出现纯配置内建件再启用前缀约定）。审核时定。

### CA-12 服务级配置合并链（intercept 对应物，P1，M）

| 列 | 内容 |
|----|------|
| Cordis | `ctx.intercept(name, config)`：context 原型链逐级收集 + `Service.resolveConfig(base, head)` merge（context.ts:139-147、service.ts:95-110）——服务级选项（如 logger 级别）沿作用域链覆盖 |
| Keystone 现状 | 插件 config 有过滤器链（ConfigResolver，可否决）+ 服务 get/set 拦截（IContextInterceptor）；**服务级选项宿主无入口**（KeystoneHostOptions 无对应字典；RingBufferLoggerProvider 已支持 `overrides/defaultLevel` 构造参数但无人接线——DC-20 剩余的代码实证） |

**实现提案**（最小面，按 12 §M2/IOptions 既定方向）：
- `KeystoneHostOptions.ServiceOptions: IReadOnlyDictionary<string, object?>?`（服务名 → 选项字典；宿主级一层，不做 context 链——嵌入方可经 LoggerFactory 自定义 provider 实现多层）
- 日志首例接线：`ServiceOptions["logger"]` → 反序列化 `{ defaultLevel, levels: {name: level} }` → 构造 RingBufferLoggerProvider（capacity/sinks 同参数化）作为默认 LoggerFactory（未显式注入 LoggerFactory 时）
- 服务侧消费模式定式：服务从 `IServiceStore` 收到选项包后自行绑定（编译期泛型 `Options.Create<T>`，规则 0 第 5 条）
- **TDD**：ServiceOptions 配 `logger.levels.{plugin}=Error` → 该插件 Debug 日志不落 RingBuffer 快照
- 开放问题：是否需要 Cordis 式"逐级 merge"（多层叠加）→ 建议首期仅宿主级一层 + 嵌入方经 LoggerFactory 扩展，审核时定

## 3. B 类：部分实现 / 语义差异（6 项）

| # | 差异 | 处置建议 |
|---|------|---------|
| CA-13 | **依赖换实例重载（epoch）**：Cordis epoch 含 `impl.fiber.uid`——同名服务换提供者 → 依赖方自动 unload/reload（fiber.ts:611-623）；Keystone rebind 同 scope 报错（G14），仅 Available 翻转触发重载 | **P2 增强候选**：`Provide` 增 `RebindPolicy { Error(默认), ReloadIfOwnerChanged }`——Error 保持安全默认，策略开 → 属主变化时宿主发起依赖方 Reload（走既有 ServiceChanged 链）。审核时定是否需要（多实现热替换场景） |
| CA-14 | **await 抛启动错误**：Cordis `fiber.await()` 重抛 startup error；Keystone CreateEntryAsync 收敛但失败进 FAILED 不抛（隔离语义 09 §2） | **接受差异**（FAILED 可经 GetPluginState 查询 + TaskFailedFact 事实已记录）；补 12 §映射注记 |
| CA-15 | **update noSave**：Keystone UpdatePluginAsync 固定写回；Cordis 有 `noSave` 提示位 | P2 小改：`UpdatePluginAsync(id, config, save: bool = true)`——save=false 跳过 ScheduleWriteBack（内存态更新，watcher 场景防回环写：文件已是新值） |
| CA-16 | **internal/listener、internal/dispatch 事件**：无对应（.NET 事件模型下监听器注册/分发不作为总线事件暴露） | **接受差异**（G15/ADR-0006 已有等价过滤与模式语义）；12 §注记 |
| CA-17 | **写队列粒度**：include `applyQueue` 任务级串行化（enqueue 排队）；Keystone `_applyingConfig` 自旋等待（10ms 轮询） | **接受差异**（功能等价；若高并发 CRUD 场景出现饿死再改 Channel 串行泵，挂 11 观察项） |
| CA-18 | **Service 抽象基族**：init/invoke/extend/check/tracker 五符号；Keystone 服务 = 任意 T + Provide（init→InitializeAsync、invoke→仅 GetLogger 形态） | **接受差异**（POCO 服务 + 扩展方法组合是 C# 惯例；check 为 G9 显式弃用）；12 §注记 |

## 4. C 类：代码级等价抽样凭证（核对通过项）

五模式事件 + once/prepend + isBailed（含 false 不短路）+ scope 过滤（EventBus.cs）；quiesce 五步闸门 + ALC Unload + 总超时未收敛审计；rebind 同 scope 报错；provide disposer + 属主注销（G-C3）；Effect + EffectMeta 诊断树（IContext.Effect/GetEffects ↔ fiber effect/getEffects，fiber.ts:415-571）；六态状态机含 Loading/Unloading 真实转移（PluginRuntime.cs:222/350 ↔ FiberState）；CRUD + 写回（防抖/原子写/占用重试/关停排空 ↔ include writeQueue/retryableWriteError）；diff 分级热/冷/挂起（ConfigDiffer ↔ 08 §6.1）；配置 watcher（防抖合并 ↔ include watcher）；TaskId 幂等 + Trace + 取消贯穿（超出 Cordis 的 .NET 侧补齐）；日志 per-name 级别三级过滤（RingBufferLoggerProvider ↔ logger.ts:155 levels 解析）；append-only 事实存储 + 归档 + 定时 Prune（ADR-0009 对应物，Cordis 无此能力）；semver/白名单 manifest 校验；IPluginSource 获取端抽象（loader import 演进位的 C# 形态）。

## 5. 决策矩阵（待人工审核）

| 编号 | 项 | 建议优先级 | 工作量 | 建议处置 |
|------|----|-----------|--------|---------|
| CA-9 | 计时器随卸载回收 | **P0（正确性）** | S | **实施**——唯一僵尸副作用 bug |
| CA-10 | 组 CRUD 级联（孤儿插件） | **P0（正确性）** | M | **实施**——组删除泄漏运行插件 |
| CA-1 | isolate 服务隔离接线 | P1 | M | 实施（已承诺 3 §2.2） |
| CA-3 | 组级事务 + 回滚 | P1 | M | 实施（08 §6.2 已设计） |
| CA-4 | 组合 update（移动+config+position） | P1 | S | 实施 |
| CA-6 | initial 引导接线 | P1 | S | 实施（激活既有死代码） |
| CA-12 | 服务级选项（日志首例） | P1 | M | 实施（DC-20 剩余收口） |
| CA-2 | 插件源文件 watcher | P2 | S | 实施（ReloadPluginAsync 已具备） |
| CA-5 | 运行期 patch 注入 | P2 | S | 实施（部署期差异化） |
| CA-7 | readonly 优雅降级 | P2 | S | 实施（08 §6.3 承诺） |
| CA-15 | update noSave 参数 | P2 | S | 实施（防 watcher 回环写） |
| CA-13 | epoch 换实例重载 | P2 | M | 待定（多实现热替换场景出现再做） |
| CA-8 | JSON 配置格式 | — | S | **建议弃用**（与 !!env 插值冲突，ADR-0014 范围） |
| CA-11 | `cordis:` 内建前缀 | — | S | **建议弃用**（内建 = 宿主组合根直接构造） |
| CA-14/16/17/18 | await 抛错/listener·dispatch/队列粒度/Service 基族 | — | — | **接受差异**（12 §补注记） |

> 实施顺序建议（若批准）：CA-9 → CA-10（P0 批）→ CA-1/CA-3/CA-4/CA-6/CA-12（P1 批）→ CA-2/CA-5/CA-7/CA-15（P2 批）。
> 每批沿用 13 §6 纪律：TDD + 全量回归 + AOT 冒烟 + 14 日志 + 11 状态回写。

## 6. 与相邻文档的关系

| 文档 | 关系 |
|------|------|
| 11-gap-register | 本文的跟踪载体（§3.3 CA 系列行）；审核决策后状态在此更新 |
| 17-doc-compliance-audit | 文档承诺 vs 实现的审计（已闭合）；本文是**实现后代码级**第二轮——口径不同（文档说没说 vs 代码有没有） |
| 16-cordis-gap-review | 实现后首轮功能复核（G-C1~C14，已闭合）；本文补其未覆盖的 loader/include 源码级细粒度 |
| 08/09/03 | CA-3/4/6/7/12 的设计依据章节（设计已有，缺实现） |
| decisions/ | CA-8/11 弃用与 CA-14/16/17/18 接受差异若获批 → 补 ADR |
