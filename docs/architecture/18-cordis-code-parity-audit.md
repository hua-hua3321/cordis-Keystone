---
type: architecture-doc
tags: [cordis-csharp, architecture, gap-tracking, code-parity]
created: 2026-08-16
---

# 18 — Cordis 代码级对照审计与实现提案（实现后第二轮，研判版）

> **审计方法**：不采信任何文档状态表（11/16/17 的 ✅ 标注一律不作为依据），直接比对两侧源码——
> Cordis 侧 = vendored 4.0.1 全部 8 个核心文件 + `cordis-plugin-loader/src`（5 文件）+ `cordis-plugin-include/src`；
> Keystone 侧 = 本仓 `src/` 六工程逐项 grep/读码验证。
> **审计基线**：`~/Projects/deepseek-harness/vendor/cordis/`（2026-08-16，17 审计 30 项闭合后）。
>
> **v2 研判版（P52）**：对初版全部 18 项做二次复核（完整读码替代抽样 grep）。**两处修正**：
> ①CA-9 初判误报——计时器 effect 挂接已存在（初版 grep `_ctx.Effect` 漏检 `ctx.Context.Effect` 写法），降级为加固项；
> ②CA-1 运行时隔离机制已存在（ContextFacade 独立链 = 天然隔离），缺口收窄为"配置接线 + 门控域感知"。
> **本文性质**：差距登记 + 解决方案集（**待人工决策，未实施任何生产代码**）。审核后按 §5 分流。

## 1. 审计口径

- 可移植行为点 ≈ 95 个；三档判定：**A 未实现 / B 部分·语义差异 / C 等价**（§4 抽样凭证）
- 结论（研判后）：A 类 12 项中 **1 项误报降级（CA-9）**、1 项缺口收窄（CA-1）；真正的 P0 正确性仅 **CA-10** 一项
- 核心运行时（事件五模式/生命周期/管道/门控/上下文/事实持久化/热更新主链）代码级等价

## 2. A 类：未实现（12 项）

### CA-1 isolate 服务隔离：配置接线 + 门控域感知（P1，M）——✅ 已实施（P57，2026-08-16；见 14 §7.57：T1 schema / T2 KeyedServiceStore / T3 facade 接线 / T4 发现投影+门控统一 / T5 宿主端到端+F10）

**研判（含两轮修正）**：
- Cordis 事实（isolate.ts 全文）：`isolate` 是 **`Dict<name → true|"label">` 两档域**，非列表——
  - `true` = `LocalRealm`（条目**私有**域，suffix `#entryId`）：每个条目独占该服务 scope
  - `"label"` = `GlobalRealm`（**共享**命名域，suffix `@label`）：同 label 的条目共享该服务 scope
  - per-entry 应用（entry.options.isolate），经 `loader/patch-context` 钩子生成 isolate map（原型链继承父），`reflect.store` 按 symbol 键存取，notify 按域符号相等过滤
- Keystone 代码事实：
  - `EntryOptions.Isolate` 是 `IReadOnlySet<string>`（EntryOptions.cs:25），EntryParser.cs:92 按**列表** `StringList(map, "isolate")` 解析；08 §3 定位"**组级**服务隔离"——**配置 schema 与 Cordis 的 map 两档模型从解析层就分叉**（不是单纯"缺接线"）
  - 运行时机制确实已有：`ContextFacade` 每 context 独立 `_services` + `Resolve` 沿父链 + `Provide` 写公共 root（组合语义，ContextFacade.cs:88-117）；类注释"独立链=天然隔离"
  - 门控 `IServiceRegistry` 宿主级单例无域概念（IServiceRegistry.cs）
- **修正后定性**：三层缺口——①schema 分叉（列表 vs map 两档）；②配置未接线（三处 context 工厂一律挂 root）；③门控无域感知。其中 ① 是**设计决策**（08 §3 故意简化），需先定 schema 去留

**解决方案（决策全部裁定：P54 默认域=共享 · P55 schema=对齐 Cordis map · 抽象化接缝）**：

*第 0 步：schema 对齐（P55 已裁定 = 方案 A）*
- `isolate` 改 `Dict<name → true|"label">`：`EntryOptions.Isolate` 由 `IReadOnlySet<string>` 改
  `IReadOnlyDictionary<string, IsolateSpec>`；`IsolateSpec = Private（true）| Shared（label）`
- EntryParser 改映射解析 + **列表 shim**（旧 `isolate: [fs]` ≡ `{fs: true}` 全私有，迁移零成本）
- EntrySerializer / EntryTree 分层合并 / ConfigDiffer 结构键 三触点跟改；08 §3 示例随实现同步改 map 形态

*第 1 步：统一键控存储（值层，进程内，不可分布式）*
- `KeyedServiceStore`（自写 ~200 行纯内存簿记）：
  `ConcurrentDictionary<(name, realm), (value, ownerId)>`；热路径无锁读；冷路径 `Lock` 复合写（属主校验+写）；
  **出锁后** fire `OnChanged(变更集)`（对齐 Cordis `notify(names[])` 批量语义：init 期 N 个 provide 合并唤醒，避免 N 次门控重评，P56）；`可用 = ContainsKey`（单一事实源）
- `ContextFacade`：`_services` 从自建 `ServiceStore` → 引用共享 store；`Resolve` 改"算 realm + 查共享 store"；
  `Provide/RemoveOwnedServices` 带 realm；新增 realm 计算（沿链继承）
- 清理：Provide 注册 effect disposer（删键 + Removed 通知），复用 EffectRegistry **推式**清理（无扫描器）
- realm ∈ {"" 默认共享, "#entryId" 私有, "@label" 命名共享}

*第 2 步：发现层抽象（元数据，可分布式——用户新增要求，接缝必须画此处）*
- **值层不可分布式**：`KeyedServiceStore` 持**活 .NET 对象实例**（`ctx.Provide<T>` 的 `T`），Redis/Consul 存不了
- **可分布式的只有发现元数据**（谁提供/可用性/端点）。抽象接缝 = 发现层，非值层：
  - `IServiceDiscovery` **只读+通知，不含写**（P56 收窄：注册/注销生命周期已由 effect-disposer 覆盖，写路径走 store，
    发现层再暴露 Register*/Unregister* = 浅接口）：`IsAvailable(name, realm)` + `Subscribe(change)`（+ 诊断用 `AvailableServices`）
  - **同步契约**：`IsAvailable` 永远同步本地读——未来 Redis/Consul adapter 也是"本地缓存 + 后台同步"
    （OnChanged→publish，远端 watch→更新缓存），网络永不上门控热路径；**今天零 async 感染**（P56）
  - 内存实现 = **投影** `KeyedServiceStore`（可用 = 键存在，零冗余状态）
  - 未来实现 = Redis pub/sub / Consul / etcd（同构 Steeltoe `IDiscoveryClient`、Aspire `IServiceEndpointProvider`）
- 插件门控只消费 `IServiceDiscovery` → 未来换分布式实现，PluginRuntime/值 store **零改动**
- **单 adapter 警示**（P56）：seam 今天只有一个 adapter，形状未经第二实现验证——对策 = 接口保持 2~3 成员
  （未来 reshape 代价趋零）+ 契约（仅元数据/本地同步读/后台写同步）文档化于本节
- 这修正了 P52 的"删 ServiceRegistry"表述：**不是删，是升格**——`IServiceRegistry` → `IServiceDiscovery`（可交换抽象），
  其内存实现从"独立冗余状态"改为"投影值 store"，单一事实源仍在

*第 3 步：门控域感知（完整档，域维度落在发现层）*
- `IServiceDiscovery` 的 name 带 realm；PluginRuntime 门控传本插件 realm；
  notify 域过滤 = `WatchAsync` 按 `(name, realm)` 精确订阅（对齐 Cordis notify filter）

**实施序（P56 细化）**：第 0 步 schema+shim 纯配置层、零运行时涟漪，**可独立先行提交**；随后第 1+2 步一并落地
（store + facade 改接 + 发现投影本就是一体）；第 3 步按需。每步 TDD + 全量回归 + AOT 冒烟 + 文档回写 + 独立提交

**开放问题**：①事件投递是否也按域过滤 → 建议否（G15 scope 链已覆盖）；②完整档（registry 域感知）是否随第 1 步一起做，还是第 1+2 步先行（值域隔离 + 发现抽象），第 3 步按需——建议 1+2 先行

### CA-2 插件源文件 watcher（P2，S）

**研判**：08 §6 第一触发源；`ReloadPluginAsync` 冷重启管线完备（重编译+换 ALC+quiesce 旧实例），仅缺触发器。`IPluginSource` 抽象（P48）使 roots 可枚举。

**解决方案**：
1. `PluginFileWatcher`（Hosting）：复用 ConfigFileWatcher 防抖模式（100ms 合并）；监听 `LocalPluginSource` 的 roots 目录；变更文件 → 按 `manifest.Main` 文件名匹配 active 条目（`EnumerateActiveLeaves` + ManifestProvider）→ 逐条 `ReloadPluginAsync(id)`；编译失败 → 插件 FAILED（隔离语义，不崩宿主）
2. `EnablePluginWatch()` 宿主 API（与 EnableConfigWatch 对称，opt-in；随 Dispose 停）
3. TDD：临时目录插件文件改写 → PluginReloading 事件 + loader 程序集实例变化（热替换凭证）；无匹配条目的文件变更 = 无操作

### CA-3 组级事务：并行应用 + 聚合 + 回滚（P1，M）——✅ 已实施（P59-T1，2026-08-16；见 14 §7.59：逐条目失败收集聚合 + 逆序回滚 + Disposal owns termination；组内依赖拓扑分层并行未做——门控 PENDING 等待已天然拓扑序，并行留待性能需求）

**研判**：group.ts:59-118 全事务语义（allSettled 并行/单错抛因/多错 AggregateError/逆序回滚新建+重建旧/回滚失败聚合/"Disposal owns termination"）。Keystone `ApplyConfigAsync` 顺序、首错中断、无回滚。**但 diff 增量模式下回滚面更小**（旧条目未动无需重建，只需撤销本次新增/变更）。

**解决方案**（含 P53 复核修正——并行有门控超时隐患）：
1. `ApplyConfigAsync` 分组化：diff 按**所属组**分桶。**并行策略修正**：Keystone 门控有超时（DC-5 GatingDependencyTimeout——不无限 PENDING，PluginRuntime.cs:285），Cordis PENDING 无限等——组内全并行会让"依赖兄弟条目的新条目"在等待窗口内伪超时失败。故：**组内按 inject 依赖拓扑分层**（无依赖的叶并行，依赖者等被依赖者 Active 后再起）；或组事务期间对本次新建条目放宽/豁免门控超时。桶间顺序（根桶最后）
2. 失败聚合：收集 Exception 列表——1 个抛原因、多个抛 `AggregateException`；**回滚**：逆序撤销本次已成功的变更（Added→RemoveEntryAsync；ConfigChanged→UpdatePluginAsync(旧 config)；StructurallyChanged→ReloadPluginAsync 前先 ReplaceEntry 回旧值再 Reload）；回滚本身失败 → 聚合进同一 AggregateException 上抛
3. 树卸载短路：`ThrowIfShuttingDown` 检查点进每步（卸载中的组更新不回滚——对齐 Disposal owns termination）
4. TDD：组内 2 新增 1 失败 → 2 个均回滚（DumpConfig 复原 + 插件不在 _plugins）；双失败 → AggregateException 含 2 内因；回滚失败 → 聚合上抛

### CA-4 组合 update（config+移动+position 一步）（P1，S）——✅ 已实施（P59-T2，2026-08-16；见 14 §7.59：UpdateEntryAsync 热/冷路径 + (源父,原下标) 精确回插）

**研判**：tree.ts:114-143 一次调用改选项+跨组移动+position，双段回滚。Keystone 只有分离的 MoveEntryAsync（回滚仅回根）与 UpdatePluginAsync。

**解决方案**：
1. 新 API `UpdateEntryAsync(string id, EntryOptions options, string? parent = null, int? position = null)`：
   - 判定：结构键（name/inject/isolate）不变且 parent 未变 → 仅 config 路径（PatchContext 瀑布 + UpdatePlugin 语义）；结构变或跨组 → 冷重启
   - 移动记账：记录 (源组, 原下标) → RemoveFromTree + InsertEntry(new)；**任一步失败回插原位置**（修复现 MoveEntryAsync 回滚只回根的偏差）
2. `MoveEntryAsync` 保留为纯移动便捷面（内部委托新 API 的移动段）
3. TDD：移动+config 同改一次成功；config 失败 → 条目回原组原下标（断言下标精确）

### CA-5 运行期 patch 注入（P2，S）

**研判**：include/index.ts:58-130 `Config.patches` 读后插入（组/根）+ 按 id 覆盖 + name 不匹配跳过。Keystone 无对应；`PatchContextAsync` 是上下文补丁瀑布（另一语义，XML doc 需注明防混淆）。

**解决方案**：
1. Config 层纯函数 `EntryPatcher.Apply(IReadOnlyList<EntryOptions> tree, IReadOnlyList<EntryPatch> patches)`：`EntryPatch(string? GroupId, IReadOnlyList<EntryOptions>? Insert, IReadOnlyDictionary<string, EntryOptions>? Overrides)`；插入组（GroupId 非空）或根；覆盖按 id 合并非 null 字段（name 不匹配 → 跳过 + 可选警告回调 `Action<string>? onWarn`）
2. `KeystoneHostOptions.ConfigPatches`；`StartAsync` 解析后、校验前应用（patch 后的树才进 manifest 校验——对齐 Cordis patch 在 schema 前生效）
3. TDD：插入组/插入根/覆盖 config/name 不匹配跳过且 warn 回调触发/空 patches 恒等

### CA-6 initial 引导接线（P1，S）

**研判**：include Service.init ENOENT+initial → 先写再读。Keystone `EnsureInitialAsync` 是死代码（宿主零调用）且 `KeystoneHostOptions` 无 initial 选项。**注意**：现 `StartAsync(string)` 收 yaml **文本**而非路径（文件读取在嵌入方）——接线需要一个文件入口。

**解决方案**：
1. `KeystoneHostOptions.InitialEntries: IReadOnlyList<EntryOptions>?`
2. 新宿主 API `StartFromFileAsync()`（无参）：要求 ConfigFilePath 已配置 → 文件不存在且 InitialEntries 非空 → `_configWriter.EnsureInitialAsync(InitialEntries)` → `File.ReadAllTextAsync` → 走既有 `StartAsync(text)`；文件已存在 → initial 忽略（对齐 Cordis）；文件不存在且无 initial → 抛 ConfigValidationFailed（对齐 include 报错）
3. TDD：无文件+initial → 启动后文件存在且插件 Active；文件已存在 → initial 不覆盖；无文件无 initial → 明确报错

### CA-7 配置写 readonly 优雅降级（P2，S）

**研判**：include `checkAccess(W_OK)` 预检 → readonly 标记 → 写静默跳过。Keystone 只有占用重试（10×50ms）→ 超限抛 `ConfigProviderFailed`。08 §6.3 已承诺"readonly 检测：无写权限 → 只读模式，写操作报错不崩溃"。

**解决方案**：
1. `ConfigFileWriter` 增 `bool IsReadOnly` 状态 + `event Action? ReadOnlyDetected`（或构造注入回调，一次性触发）：
   - 判定：`WriteCoreAsync` 重试循环中错误 HResult = 0x80070005（拒绝访问）且非首次 → 置 readonly（区别于 0x80070020 共享占用——占用该重试、拒绝该降级；Unix 下 EACCES 映射到同判定尽力而为）
   - readonly 后：`ScheduleWrite/WriteAsync/FlushAsync` 直接返回（不抛、不再尝试）；写回失败不阻断 Shutdown 的既有 catch 语义兼容
2. TDD：子类注入 0x80070005 故障 → 第二次写不抛 + 回调恰一次；0x80070020 占用 → 仍走重试不降级

### CA-8 JSON 配置格式（建议弃用，S）

**研判**：include 支持 yaml/json/模块三种。Keystone 仅 YAML = **ADR-0014 明确的设计范围**（P0 YAML-only）。硬伤：`!!env`/`!!file` 静态插值是 YAML tag 机制（DC-8/P38），JSON 无 tag 概念——支持 JSON 即放弃插值或双轨解析。

**解决方案**：补 ADR-0016《配置格式收敛 YAML-only》记录弃用理由（与静态插值互斥 + 单格式降低矩阵）；若未来确需 JSON：走 `IConfigProvider` 抽象（ADR-0013 已预留）自实现，声明不支持 !!env。**建议弃用，审核时定。**

### CA-9 ~~计时器不随卸载回收~~ → 竞态加固（**初判误报**，降级 P2，S）

**研判（修正）**：初版断言"TimerHandle 不注册任何 effect"为 **grep 误报**（检索 `_ctx.Effect` 漏了实际写法 `ctx.Context.Effect`）。代码事实：
- 构造尾部已注册：`ctx.Context.Effect(() => { _cts.Cancel(); ... }, label: $"timer:{label}")`（TimerExtensions.cs 构造段）
- 插件 quiesce 确实收敛：PluginRuntime.cs:354 `await WithTimeoutAsync(_context.Context.DisposeEffectsAsync(), "effect quiesce")`
- **卸载即取消，无僵尸副作用——核心结论推翻**

**残留两个小加固点（真实但轻微）**：
1. **CTS dispose 竞态**：`DisposeAsync` 在 Cancel 后立即 `_cts.Dispose()`；RunLoop 的 `while(!ct.IsCancellationRequested) → Task.Delay(_delay, ct)` 在检查与注册之间若发生 dispose → `ObjectDisposedException`，而 RunLoop 仅 catch `OperationCanceledException` → 漏成未观察任务异常
2. **收敛不等在途回调**：effect disposer 只 Cancel 不等 `FireSafeAsync` 在途完成——quiesce 返回时最后一次回调可能仍在飞（Cordis effect 收敛 await disposables）

**解决方案**：disposer 改为 `async`：Cancel 后 `await _runTask`（构造时保存 RunLoop 任务引用；`try/catch` 全吞）；`DisposeAsync` 移除 `_cts.Dispose()`（Cancel 已足够释放等待者，CTS finalizable 无压力——消除竞态源头）。TDD：卸载后 RunLoop 无未观察异常（TaskScheduler.UnobservedTaskException 探针或直接代码审查凭证）；卸载 await 完成后无在途回调（计数器断言）。

### CA-10 组条目 CRUD 级联（**唯一 P0 正确性**，M）——✅ 已实施（P58，2026-08-16；见 14 §7.58：组删逆序级联卸载 + 建组逐叶加载 + Move 纯树差异注明）

**研判**：tree.ts:97-112 组 create 加载整子树并 await；group.ts:48-57 remove 逐子卸载。Keystone：
- `RemoveEntryAsync` 只 Dispose 精确匹配 EntryId 的**叶子**插件——`RemoveEntryAsync(组id)` 从树删除组但**整组插件继续运行**（孤儿）。仅 `ApplyConfigAsync` 路径因 ConfigDiffer.Flatten 扁平化间接弥补；直接调 API（嵌入方管理面）必泄漏
- `CreateEntryAsync` 组条目只发 EntryInit 不加载 children（KeystoneHost.cs:239-243）——运行期建组 = 空壳

**解决方案**：
1. 抽 `DisposeHostedAsync(string id)`（现 RemoveEntryAsync 内联逻辑：EntryDisposing → loader.DisposeAsync → _plugins 移除）
2. `RemoveEntryAsync(id)`：`FindEntry` → 若 `entry.Group is { } children` → `EnumerateLeaves(children)` **逆序**逐叶 `DisposeHostedAsync`（逆序对齐 Cordis 卸载序）→ 再 `RemoveFromTree(id)` + NotifyConfigUpdate + ScheduleWriteBack
3. `CreateEntryAsync` 组路径：`EnumerateActiveLeaves(entry.Group)`（挂起继承语义复用 DC-16）逐叶 `LoadEntryAsync`（await 收敛）；组 EntryInit 仍发；失败策略首期沿用现状隔离语义（叶失败进 FAILED 不阻断兄弟——CA-3 落地后可升级为聚合）
4. `MoveEntryAsync` 组移动 = 纯树操作（插件不迁——组移动不改变成员的 context 链；与 Cordis 的差异注明）
5. TDD：加载含 2 子插件的组 → `RemoveEntryAsync(组)` → 两叶 loader 均 Dispose（GetPluginState 抛 GatingServiceNotFound）+ 树无残留；`CreateEntryAsync(带子组)` → 子叶均 Active；挂起组的 Create → 子叶不加载

### CA-11 `cordis:` 内建插件命名空间（建议弃用，S）

**研判**：loader import 按前缀分发内建。Keystone 框架能力（配置加载/CRUD/能力域）由宿主组合根 C# 直接构造——**不是可加载条目**，无运行时按名分发需求。

**解决方案**：补 ADR（并入 ADR-0016 或独立）记录弃用理由；若未来出现纯配置内建件（如内建 telemetry 条目），启用 `keystone:` 前缀约定 + LocalPluginSource 内置 root。**建议弃用，审核时定。**

### CA-12 服务级配置合并链（intercept 对应物）（P1，M）

**研判**：Cordis `ctx.intercept(name,config)` 原型链逐级 merge + `Service.resolveConfig(base,head)`（context.ts:139-147、service.ts:95-110）。Keystone 代码事实：
- 插件 config 有过滤器链（ConfigResolver 可否决）+ get/set 拦截（IContextInterceptor）——**服务级选项无宿主入口**
- `RingBufferLoggerProvider` 构造已支持 `overrides/defaultLevel/capacity/sinks`（RingBufferLoggerProvider.cs:20-34）但**无人接线**；宿主未配 LoggerFactory 时 root 走 `NullLoggerFactory.Instance`（ContextFacade.cs:40）——RingBuffer 默认根本不在链上（DC-20 剩余的代码实证）

**解决方案（最小面 + 日志首例）**：
1. `KeystoneHostOptions.ServiceOptions: IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>?`（服务名 → 选项字典；宿主级一层）
2. 日志首例接线：`StartAsync` 构建 root context 时——嵌入方未注入 LoggerFactory 且 `ServiceOptions["logger"]` 存在 → 解析 `{ defaultLevel, capacity, levels: {name→level} }` → 构造 `RingBufferLoggerProvider(capacity, levels, sinks, defaultLevel)` 作 LoggerFactory（替代 NullLogger 兜底）；显式 LoggerFactory 优先（不覆盖嵌入方）
3. 服务消费定式（写进 10 §SDK 文档）：服务经 InitializeAsync 收选项包后自行 `Options.Create<T>` 绑定（规则 0 第 5 条编译期泛型）
4. TDD：ServiceOptions 配 `logger.levels.{plugin}=Error` → 该插件 Debug 日志不出现在 `GetSnapshot()`；显式 LoggerFactory 存在时 ServiceOptions["logger"] 被忽略
5. **开放问题**：是否需要 Cordis 式多层 merge（context 链逐级覆盖）→ 建议否（宿主级一层 + 嵌入方经 LoggerFactory 自定义 provider 已可组合多层；多层 merge 引入解析顺序复杂度不值）

## 3. B 类：部分实现 / 语义差异（6 项）

| # | 研判 | 解决方案 |
|---|------|---------|
| CA-13 依赖换实例重载（epoch） | Cordis epoch 含 `impl.fiber.uid`——同名服务换提供者 → 依赖方自动重载（fiber.ts:611-623）；Keystone `Provide→root.Set(ownerId)` 属主不同即抛 ServiceAlreadyRegistered（ServiceStore.cs:19-26，G14 rebind 报错）；仅 Available 翻转触发重载 | P2 增强候选：`Provide` 增重载 `Provide(name, instance, RebindPolicy policy)`——`Error`（默认，现行为）/`ReplaceAndNotify`（属主变更时：旧值 Remove + 新值 Set + registry 发 ServiceChanged → 依赖方走既有重载链）；不做 epoch 字符串（用属主比对等价实现）。**先问场景：多实现热替换（蓝绿）出现再做** |
| CA-14 await 抛启动错误 | Cordis `fiber.await()` 重抛 startup error；Keystone CreateEntryAsync 收敛但失败进 FAILED 不抛（隔离语义 09 §2，GetPluginState 可查 + TaskFailedFact 已记录） | **接受差异**：12 §补注记（隔离语义是刻意设计——单插件失败不阻断管理面调用方） |
| CA-15 update noSave | Cordis `update(config, noSave)` 提示位；Keystone UpdatePluginAsync 固定 ScheduleWriteBack | P2 小改：`UpdatePluginAsync(id, config, save: bool = true)`——save=false 跳过写回（内存态）；**配套**：ConfigFileWatcher 触发的 ApplyConfigAsync 内部走 save=false（文件已是新值，防回环写） |
| CA-16 internal/listener·dispatch | 无对应（.NET 事件模型下监听器注册/分发不作为总线事件暴露）；其余 7 个 internal/* 已有对应物 | **接受差异**：12 §注记（等价面 = EventSubscriptionOptions + 五模式分发本身） |
| CA-17 写队列粒度 | include `applyQueue` 任务级串行（enqueue 排队）；Keystone `_applyingConfig` 自旋等待（10ms 轮询，功能等价粒度粗） | **接受差异 + 挂观察**：若高并发 CRUD 出现饿死/延迟再改 `Channel<Func<Task>>` 单消费泵；11 挂观察项 |
| CA-18 Service 抽象基族 | init/invoke/extend/check/tracker 五符号；Keystone 服务 = 任意 T + Provide（init→InitializeAsync、invoke→仅 GetLogger 形态、extend/check 无） | **接受差异**：POCO 服务 + 扩展方法是 C# 惯例（check 为 G9 显式弃用）；12 §注记 |

## 4. C 类：代码级等价抽样凭证

五模式事件 + once/prepend + isBailed（false 不短路）+ scope 过滤（EventBus.cs）；quiesce 五步闸门 + ALC Unload + 总超时未收敛审计；rebind 同 scope 报错；provide disposer + 属主注销（G-C3）；Effect + EffectMeta 诊断树（IContext.Effect ↔ fiber effect/getEffects）；**计时器 effect 挂接（本轮修正后归此类——CA-9 误报的正面凭证）**；六态状态机含 Loading/Unloading 真实转移（PluginRuntime.cs:222/350）；CRUD + 写回（防抖/原子写/占用重试/关停排空）；diff 分级热/冷/挂起；配置 watcher；TaskId 幂等 + Trace + 取消贯穿；日志三级级别过滤逻辑（RingBufferLoggerProvider 内，接线归 CA-12）；append-only 事实存储 + 归档 + 定时 Prune；semver/白名单 manifest 校验；IPluginSource 获取端抽象。

## 5. 决策矩阵（v2 研判版，待人工审核）

| 编号 | 项 | 研判后优先级 | 工作量 | 建议处置 |
|------|----|-----------|--------|---------|
| CA-10 | 组 CRUD 级联（孤儿插件） | **P0（唯一正确性）** | M | **实施** |
| CA-1 | isolate 接线 | P1 | M（1+2 步）→ +M（第 3 步） | **全部裁定**：默认域=共享（P54）/ schema=对齐 Cordis map（P55）/ 抽象接缝=发现层（P55）；实施序 1+2 先行 |
| CA-3 | 组级事务 + 回滚 | P1 | M | 实施（08 §6.2 已设计）；**并行改拓扑分层**（规避 DC-5 门控超时伪失败） |
| CA-4 | 组合 update | P1 | S | 实施 |
| CA-6 | initial 引导（激活死代码） | P1 | S | 实施 |
| CA-12 | 服务级选项 + 日志首例 | P1 | M | 实施（DC-20 剩余收口） |
| CA-2 | 插件源文件 watcher | P2 | S | 实施 |
| CA-5 | 运行期 patch | P2 | S | 实施 |
| CA-7 | readonly 降级 | P2 | S | 实施 |
| CA-15 | noSave 参数 | P2 | S | 实施（配套 watcher 防回环） |
| CA-9 | ~~计时器回收~~ 竞态加固 | P2（**误报降级**） | S | 实施加固（CTS dispose 竞态 + 在途回调收敛） |
| CA-13 | epoch 换实例重载 | P2 待定 | M | 场景驱动（蓝绿热替换出现再做） |
| CA-8 | JSON 格式 | — | S | **建议弃用**（与 !!env 互斥；补 ADR-0016） |
| CA-11 | `cordis:` 内建前缀 | — | S | **建议弃用**（补 ADR） |
| CA-14/16/17/18 | 四项语义差异 | — | — | **接受差异**（12 §注记；CA-17 挂 11 观察项） |

> 实施顺序建议（若批准）：CA-10（P0）→ CA-1（先选档）/CA-3/CA-4/CA-6/CA-12（P1 批）→ CA-2/CA-5/CA-7/CA-9/CA-15（P2 批）。
> 每批沿用 13 §6 纪律：TDD + 全量回归 + AOT 冒烟 + 14 日志 + 11 状态回写。

## 5.1 复核注记（P53，对 P52 决策的二次批判）

| 项 | 复核结论 | 更优方案 |
|----|---------|---------|
| CA-1 | P52 判"机制已有缺接线"**不完整**——漏了 schema 分叉（列表 vs Cordis map 两档域） | 先做 §2 第 0 步 schema 决策；未定 schema 前不实施接线 |
| CA-3 | P52"组内全并行"**有缺陷**——门控超时（DC-5）会让依赖兄弟的新条目伪超时 | 拓扑分层并行 / 事务期豁免超时（§2 已改） |
| CA-13 | RebindPolicy **基本冗余**：provider 重启已被 unload→re-register→依赖重载链（P25/P26）覆盖；且"owner 比对"复现不了 Cordis epoch（fiber-uid 变化即触发）语义 | 维持"场景驱动延后"；若做用变更事件（ServiceChanged）而非 owner 比对；只对蓝绿**存活替换**有意义 |
| CA-12 | 首步次序可更优：默认 provider 缺失（走 NullLoggerFactory）比"缺 ServiceOptions"更基础 | 拆两阶段：①默认 RingBuffer+Console provider 开箱即用 + levels 选项；②再做通用 ServiceOptions 机制 |
| CA-4 | 组合 update 命令式 API 可能 YAGNI——diff 路径（ApplyConfigAsync）已覆盖组合场景 | 可整体延后；仅编程式调用方需要时再补 |
| CA-6/7 | initial/readonly 都假设文件后端，与 ADR-0013 配置源抽象有张力 | 写回管线显式圈定 LocalYaml provider 边界；或写回改走 IConfigProvider（大重构，另立专项） |
| CA-9 | 修复方案正确（移除 Cts.Dispose + await runTask）；补充：DisposeAsync 是公共 API 与 effect 双路径，幂等靠 `_disposed`（已有） | 确认无需更多改动 |

## 6. 与相邻文档的关系

| 文档 | 关系 |
|------|------|
| 11-gap-register | §3.3 CA 系列跟踪载体（本轮同步 CA-9 降级/CA-1 收窄） |
| 17-doc-compliance-audit | 文档承诺 vs 实现口径（已闭合）；本文是代码等价口径，结论不互通 |
| 16-cordis-gap-review | 实现后首轮功能复核（已闭合）；本文补 loader/include 源码级细粒度 + 二次复核 |
| 08/09/03 | CA-3/4/6/7/12 设计依据（设计已有，缺实现） |
| decisions/ | CA-8/11 弃用若获批 → 补 ADR-0016；CA-14/16/18 → 12 §注记 |
