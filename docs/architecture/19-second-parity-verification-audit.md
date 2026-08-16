---
type: architecture
tags: [cordis-csharp, architecture, audit, parity]
created: 2026-08-16
status: standard
---

# 19 · 第二轮实现后等价性复核审计（P57-P63 实施后）

> **本文性质**：18 号审计 18 项全部收敛（P57-P63：11 项实施 + ADR-0016 + 注记）后的**全量再对照**——验证既判项实际完成度 + 对 Cordis 全源（核心 9 文件 + plugin-include + plugin-loader + cosmokit/bin.js）重新扫描漏网。
> **方法**：6 路并行深审（服务值层 SV / 上下文生命周期 CF / 事件 EV / 日志 LG / 加载器配置树 LD / 文件管线 IN）+ 第 7 路表面自查（utils 导出穷举/cosmokit/bin.js/README 生态包）。每项含双侧 `文件:行号`。
> **结论**：P57 核心成果（两档域 schema/发现层投影/disposer 幂等/移动精确回插/防回环）经对抗验证**正确**；但发现 **7 项 P0 正确性缺陷**（多落在近期 CA 实施的未测路径）、6 项 P1 竞态、9 项 P1 语义偏差待决策、~30 项 P2 对齐/文档项。

## 1. P0 正确性缺陷（必然失败路径 / 资源泄漏，建议立即修）

| # | 缺陷 | 双侧位置 | 描述 |
|---|------|---------|------|
| P0-1 (LD-1) | diff Added 丢失组归属 | ConfigDiffer.cs:17-20 + KeystoneHost.cs:754-760 vs group.ts | 向**既有组**新增子叶 → CreateEntryAsync(entry) 无 parent → 插到根，组结构破坏；测试只断言 Active 未查树（GroupTransactionTests.cs:90-115） |
| P0-2 (LD-2) | 新增带子组必然失败 | KeystoneHost.cs:328-338→321-324 | Flatten 产出组+全部子叶 → Create(g) 已逐叶加载子 → 子叶再 Create 撞 duplicate id → 整批回滚。任何"全新组+子"热更必失败 |
| P0-3 (LD-3) | 结构步中途失败留半应用态 | KeystoneHost.cs:714-729 vs 849-861、group.ts:95-101 | ApplyStructuralChangesAsync 两阶段（先全量 ReplaceEntry 再逐叶重载），undo 在整步成功后才登记 → 第 2 叶失败时树已全换、无 undo 可回滚 |
| P0-4 (LD-11/IN-2) | watcher 热重载不重跑静态插值 | KeystoneHost.cs:881 vs 147-151 | EnableConfigWatch 回调 `EntryParser.Parse(yaml)` 无 interpolator → `!!env/!!file` 原始标记串被当配置值注入（DC-8 回退） |
| P0-5 (IN-3) | 写管线无串行化 | ConfigFileWriter.cs（无队列）vs include writeQueue | Timer 防抖触发与显式 Flush 可并发写同一 `.tmp` → Move 竞态（Cordis 有 writeQueue 单消费） |
| P0-6 (CF-1/EV-5) | 插件事件订阅不随生命周期回收 | ContextFacade.cs:155-168 vs events.ts:254-259 | Subscribe 不注册 effect；插件不手动 Dispose 时 handler 永驻 root 共享总线 → ALC 被钉死无法回收（击穿 ALC 回收承诺） |
| P0-7 (CF-3) | CA-9 加固不覆盖 throttle/debounce | TimerExtensions.cs:53/114/121-135 vs plugin-timer | quiesce 时：① debounce 已武装原生 Timer 不取消 → 卸载后到点仍执行插件代码；② throttle 在途回调无人等；③ effect disposer 不置 `_disposed` → Trigger 仍可开火 |

## 2. P1 竞态与状态机缺陷

| # | 缺陷 | 位置 | 描述 |
|---|------|------|------|
| P1-1 (CF-2) | AwaitAsync 死字段 | PluginRuntime.cs:33/137/200/226/430 | `_settled` 从未赋非 null 值，CompleteSettled 空操作 → Pending/Loading 期 AwaitAsync 立即返回不等（文档承诺"稳定等待"）；等待路径无测试 |
| P1-2 (CF-4) | PENDING 期 Stop 竞态 | PluginRuntime.cs:163-176→374-403 vs fiber.ts:277-296 | 停止 PENDING 插件后依赖超时仍到期 → 已停插件被延迟翻 FAILED + 伪事实 |
| P1-3 (CF-5) | StopCoreAsync 无重入守卫 | PluginRuntime.cs:374-403（:89 放火即忘可与显式 Stop/Shutdown 并发） | 并发卸载下插件 DisposeAsync "恰一次"契约被打破 |
| P1-4 (SV-3①) | Unloading 期依赖重现 → 未观察异常 | PluginRuntime.cs:81-90 vs :213-228 自相矛盾 | re-arm 分支含 Unloading 但 StartCoreAsync 守卫拒绝 → fire-and-forget 抛 LifecycleInvalidState 无人观察 |
| P1-5 (SV-3②) | Loading 期依赖消失不卸载 | PluginRuntime.cs:81-90（else-if 只匹配 Active）vs fiber.ts:665-672 | 插件带缺失依赖进入 ACTIVE |
| P1-6 (LD-13) | 组 disabled 运行期翻转不级联 | KeystoneHost.cs:1056-1082 vs entry.ts:88-98、group.ts:108-112 | boot 期祖先剪枝 ✓；运行期组翻转只 ReplaceEntry 组条目，子叶照跑；且 disabled 组内叶单独 re-enable 绕过祖先检查直载 |
| P1-7 (LD-17) | 叶↔组转换误分级 | ConfigDiffer.cs:72-75（结构键不含 Group 关系） | 叶变组归 configChanged 走热更 → 组子不加载（叠 P0-1） |

## 3. P1 语义偏差（待决策：对齐 or 注记接受）

| # | 偏差 | 双侧位置 | 描述与选项 |
|---|------|---------|-----------|
| D-1 (LD-6) | 无真热更新 | KeystoneHost.cs:595-599/522-530 vs entry.ts:194-212 | "热路径"内部仍 ReloadPluginAsync（重编译+新 ALC+新实例）→ config 变更即重编译、实例状态全丢、源码坏时热更失败。选项：PluginLoader 增 config-only 原地通道（大工）or 注记接受 |
| D-2 (LD-7) | UpdateEntryAsync 整条目替换 | KeystoneHost.cs:550 vs entry.ts:146-154 | 未传字段被清空（config/inject → null）；与宿主内文件路径合并语义不一致。建议逐字段合并（仅非 null 覆盖） |
| D-3 (LD-8) | parent 缺省 = 移根 | KeystoneHost.cs:537/553 vs tree.ts:114-124 | 组内条目不带 parent 调用被挪根；Cordis 缺省 = 不动。建议 sentinel 区分"未提供"与"根" |
| D-4 (LD-9) | 失败只复原树不复原运行时 | KeystoneHost.cs:577-581 vs entry.ts:232-243 | 失败后插件处于已卸载态（Cordis 重启旧插件）。建议失败路径补 ReloadPluginAsync |
| D-5 (LD-4) | Removed 不回滚 | KeystoneHost.cs:703-707 vs group.ts:95-101（Cordis 全量重建含 Removed） | 已注记但语义确实不同——失败后 Removed 保持已删。建议补 undo 或扩大注记 |
| D-6 (SV-1) | Provide/set 语义合并 + 文档冲突 | KeyedServiceStore.cs:116-126 vs reflect.ts:289-291/254-265 | 同属主重绑被允许（Cordis 一律抛错）；`set`（仅更新不通知）缺失；**03-context §2.1 声称"重复注册报错"与实现不符**（三方不一致）。选项：对齐（Provide 二次抛错+增设 Set）or 裁定接受并回写 03 |
| D-7 (SV-2) | 门控缺"提供者 ACTIVE"时机 | KeyedServiceStore.cs:67-71 vs reflect.ts:241/294-296、fiber.ts:590-594 | 可用=值存在（init 中途 Provide 即放行依赖方）；Cordis ACTIVE 转换才补发。建议 ACTIVE 后补一次门控重评 |
| D-8 (EV-1) | 事件 scope 默认过滤语义 | EventBus.cs vs events.ts | Cordis 默认广播（跨 scope 可收）；Keystone 限定祖先链。需决策语义取向 |
| D-9 (CF-7) | Effect 句柄 Dispose = 取消非执行 | EffectRegistry.cs:130-140 vs fiber.ts:427-442 | `using var h = ctx.Effect(cleanup)` 期望释放清理，实际永不执行——契约陷阱。建议对齐（Dispose 即执行）或显式文档化取消语义 |

## 4. P2 对齐 / 加固 / 文档项

| # | 项 | 位置 | 描述 |
|---|-----|------|------|
| P2-1 (LD-10/IN-1) | watcher 不重跑 ConfigPatches | KeystoneHost.cs:878-884 vs include:315-321 | 外部改文件后 patch 丢失（与 P0-4 同根：回调裸 Parse） |
| P2-2 (LD-12) | EntryPatcher 三处对齐差异 | EntryPatcher.cs:18-21/115-125/24-28 vs include:63-64/121-124/80-103 | 无 patch 不 detached；`??` 合并无法清字段；insert+override 不互斥 |
| P2-3 (LD-14) | CA-2 只监听 roots[0] | KeystoneHost.cs:909 | 多 root 其余不触发；防抖"末文件胜出" |
| P2-4 (LD-15) | isolate 变更一律重启 + None 超集 | vs isolate.ts:96-153/98-101 | Cordis 原地换服务实现；None 是 Keystone 扩展（additive）。建议注记 |
| P2-5 (LD-16) | 结构键两处定义不一致 | ConfigDiffer.cs:72-75 vs KeystoneHost.cs:617-618 | 一含生效 isolate 一不含 → 同次变更两路径分级不同。建议统一 |
| P2-6 (LD-18a) | ResolveEntry 仅两级嵌套 | KeystoneHost.cs:422 vs tree.ts:76-87 | `:` 分隔三级以上解析失败 |
| P2-7 (LD-18b) | 无 id 条目 diff 崩 | EntryTree.cs:24-27 + ConfigDiffer.cs:17 | 分层丢弃 vs Cordis ensureId 自动分配；diff 层 ToDictionary(null) 崩 |
| P2-8 (LD-18c) | MoveEntryAsync 回滚到根 | KeystoneHost.cs:402-411 | CA-4 修复未覆盖旧 API；失败后条目位置与报错矛盾 |
| P2-9 (LD-18d) | EntryGroup.cs 死代码 | EntryGroup.cs:9-125 | standalone 组事务仅单测引用；其回滚语义与宿主**相反**（恢复 Removed 不回退 Updated）——双实现漂移 |
| P2-10 (LD-19) | 自销毁钩子缺失 | vs index.ts:117-157 | fiber 自 dispose → 条目自动 disabled + 写回；Keystone 仅 EntryDisposing 事件 |
| P2-11 (CF-6) | 无 PluginStoppedFact | PluginRuntime.cs:374-403 | 事实轨迹只见启/败不见停——重放审计呈现"永不停" |
| P2-12 (CF-8) | 卸载期注册 effect 静默 + static AsyncLocal | EffectRegistry.cs:74-84/:11 | Cordis 抛 INACTIVE_EFFECT；AsyncLocal 跨注册表劫持（A 的 disposer 内注册挂 A 名下） |
| P2-13 (CF-9) | 依赖消失状态命名分歧 | PluginRuntime.cs:81-90 vs fiber.ts:611-623 | Keystone → Disposed（re-arm 可）；Cordis → PENDING；FAILED 不参与 re-arm（Cordis 会重评） |
| P2-14 (CF-11) | root effects 无人收敛 | KeystoneHost.cs:200-261 | ShutdownAsync 不调 `_rootContext.DisposeEffectsAsync()` → 根级 effect 进程泄漏 |
| P2-15 (SV-4/5/6) | Get 抛错 vs undefined / 卸载窗口可见性 / 重绑通知 | ContextFacade.cs:72-79 等 | TryGet 已覆盖 undefined 对应物（注记）；RemoveOwnedServices 在 quiesce ③步（①②窗口可读将死值）；重绑触发通知（并入 D-6） |
| P2-16 (SV-7/8/9) | provides 兑现只查可用不查属主 / internal/service 插件面不可见 / BeginNotifyScope 未接线 | PluginRuntime.cs:279-287 等 | 加固项：属主校验；发现订阅暴露插件面；批量合并落地或修注释 |
| P2-17 (SV-12/13) | Shared label 未校验前缀 / 同组同名声明陷阱 | EntryParser.cs:194-199 | `"#foo"` 被解析为 Shared → realm `"@#foo"`；组覆盖叶子场景文档警示 |
| P2-18 (LG-12) | ConsoleLogSink 未接线 | KeystoneHost.cs:122（sinks: null） | 全仓无 `new ConsoleLogSink`；05 §5 承诺"Console 默认"未兑现（与 Cordis 核心等价，非 parity 缺口——接线或修文档） |
| P2-19 (LG-18) | levels 键前缀文档缺口 | KeystoneHostOptions.cs:101、10-plugin-sdk.md:84 | 键须为完整 category（含域前缀 `keystone/logp`）；嵌入方写 `logp` 静默失效 |
| P2-20 (LG-6/7/8/9/10/11) | 日志 partial 族 | logger.ts 各处 vs RingBufferLoggerProvider | 第四级 per-logger level（由 levels 键承担——注记）；per-exporter 阈值；sink 运行期增删；args 结构化保留；动态调级；maxLength 截断 |
| P2-21 (LG-19/20/21) | 日志 minor | ContextFacade.cs:177-181 等 | GetLogger xmldoc 错位；配置非法值静默；缺 Dispose 断言测试 |
| P2-22 (EV-2/4) | 模式分列注册 / waterfall 返回值 | EventBus.cs vs events.ts | Cordis 单 on+options 分模式；waterfall 返回值语义差异（已接受设计需显式标注） |
| P2-23 (SV-11) | 门控/解析域同源脆弱耦合 | KeystoneHost.cs:1017-1019 | 同一 isolateMap 实例双注入——未来派生即分叉（观察项） |

## 5. verified 凭证（对抗验证通过，择要）

- **P57 CA-1 两档域 schema 正确**：realm 键模型（`""`默认/`#私有`/`@共享`）、IsolateMapResolver 外→内累积+子覆盖+None 解除、`#groupId` 组内共享——与 Cordis 原型链 shadow 语义一致（SV-17/18，含运行时探针实测对照）
- **disposer 幂等 + 属主校验**：Interlocked 双 dispose、跨属主抛 ServiceAlreadyRegistered、16 线程竞写恰一胜者（SV-19/20）
- **六态状态机 + quiesce 五步闸门 + effect 逆序/嵌套后序**（CF verified；串行 vs 并行更严格不构成缺口）
- **五模式事件分发语义代码级等价**（EV verified，有测试背书）
- **CA-12 四项接线**：三级阈值/capacity 环形/显式工厂优先/Shutdown 释放（LG-1..5）
- **CA-15 防回环三路径成对解除 verified**；CA-6 initial 幂等 verified；CA-7 readonly 功能等价（预检 vs 首错降级差异集中瞬态 EACCES 误判）
- **CA-4 移动记账精确回插**；patch 组内建索引等价；isolate 两档对齐；manifest.Main 匹配 + 防抖 100ms
- **utils.ts 12 导出全映射**（12 号 §凭证表）；cosmokit 通用工具库（BCL/LINQ 等价）；bin.js 20 行引导（宿主嵌入即替代）；README 生态包覆盖登记纪律在册（12 §凭证 3）

## 6. 决策矩阵建议

| 批次 | 内容 | 优先级建议 |
|------|------|-----------|
| P64 | P0-1/2/3（CA-3 组归属三连）+ P0-6（订阅回收）+ P0-7（计时器收口） | 立即（正确性） |
| P65 | P0-4/P2-1（watcher 回调补插值+patch）+ P0-5（写串行化）+ P1-6/7（disabled 级联+组转换分级） | 高 |
| P66 | P1-1..5（状态机竞态五项）+ P2-14（root effects） | 高 |
| P67 | D-1..D-9 逐项决策（对齐 or 注记）| 人工裁定 |
| P68+ | P2 清单按价值排序 | 常规 |

## 7. 与相邻文档的关系

- 18 号：第一轮审计（已收敛）——本文验证其 11 项实施的实际完成度（发现 P0-1/2/3 落在 CA-3 未测路径）
- 11 §3.4：本文登记载体
- 12 §11.1：接受差异注记（本文 D 系列裁定后追加）
- 14 §7.64+：修复批次日志
