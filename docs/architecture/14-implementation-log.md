---
type: architecture-doc
tags: [cordis-csharp, architecture, implementation-log, traceability]
created: 2026-08-15
---

# 14 — 实施记录（过程日志）

> 记录实现期"做了什么、完成了哪些工作、做出了哪些决定"，服务后期回溯。
> 配套 13-implementation-plan.md：**13 管"下一步做什么"，14 管"已经做了什么"**。
> 回溯目标：任意一条记录 → 能找到（a）它实现了哪个设计决策（b）落在哪段代码（c）如何验收的（d）当时为什么这么做。

## 1. 记录总则

| 规则 | 内容 |
|------|------|
| **粒度** | 一条记录 = 一个**可验证的完成单元**（一次实现/一次测试/一次修复/一个决策/一次文档同步），不是一次提交、不是一天流水 |
| **时机** | 必记：阶段事件（进入/退出/里程碑）、每项工作完成时、每个决策/偏差发生时、每个验收执行时。可延后批量补记，但**最迟在该阶段退出前闭合**（13 §4 DoD） |
| **编号** | 工作项：`W{阶段}-{序号}`（如 `W3-07`）；决策：`ADR-0013+`（设计期外新决策）或 `ID-{序号}`（轻量实现期决策，见 §4）；偏差：`DEV-{序号}` |
| **状态值域** | 阶段：⏳未开始 / 🔄进行中 / 🚧受阻（写原因）/ ✅完成 / ✔验证通过。工作项：✅完成 / ⚠️部分（写剩余）/ ❌失败（写处置） |
| **语言** | 中文记录，术语沿用 00-12 文档（保证与设计文档词汇一致，方便检索） |
| **只追加** | 历史记录不改写；更正用新记录引用旧编号（对齐 ADR 不可变原则） |

## 2. 阶段状态台账

> 每阶段一行；状态/日期/里程碑/验收结论在阶段事件发生时更新。验收结论必须引用 §6 验收台账条目。

| 阶段 | 状态 | 进入日期 | 退出日期 | 里程碑 | 验收结论 | 工作日志节 |
|------|------|---------|---------|--------|---------|-----------|
| P0 工程骨架 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | M0 | 4/4 验收全绿（§6.0） | §7.0 |
| P1 核心契约 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | M1 | 3/3 验收全绿（§6.1） | §7.1 |
| P2 上下文与事件 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | M2 | 4/4 验收全绿（§6.2） | §7.2 |
| P3 服务与生命周期 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | M3 | 4/4 验收全绿（§6.3） | §7.3 |
| P4 管道执行 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | M4 | 3/3 验收全绿（§6.4） | §7.4 |
| P5 插件加载 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | M5 | 4/4 验收全绿（§6.5） | §7.5 |
| P6 配置层 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | M6 | 7/7 验收全绿（§6.6） | §7.6 |
| P7 管理层 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | M7 | 4/4 验收全绿（§6.7） | §7.7 |
| P8 能力域 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | M8 | 4/4 验收全绿（§6.8） | §7.8 |
| P9 观测与可靠性 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | M9 | 4/4 验收全绿（§6.9） | §7.9 |
| P10 事件持久化 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | M10 | 3/3 验收全绿（§6.10） | §7.10 |
| P11 插件 SDK | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | M11 | 4/4 验收全绿（§6.11） | §7.11 |
| P12 AI 组合 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | M12 | 3/3 验收全绿（§6.12） | §7.12 |
| P13 验收闭环 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | M13 | 4/4 验收全绿（§6.13） | §7.13 |
| P14 MCP 协议层 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 6/6 验收全绿（§7.14；ADR-0008 决策 4 延迟项落地） | §7.14 |
| P15 解耦审计 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 审计闭环（§7.15：C1-C8 清单 + 15-decoupling-plan 计划） | §7.15 |
| P16 解耦 D1 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 能力域接线 + Proto 隔离（§7.16：C1/C1b/C2 闭合，200/200 全绿） | §7.16 |
| P17 解耦 D3 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 序列化器抽象（§7.17：C6 闭合，IContractSerializer + JSON 注入，205/205 全绿） | §7.17 |
| P3 服务与生命周期 | ⏳ | | | M3 | | §7.3 |
| P4 管道执行 | ⏳ | | | M4 | | §7.4 |
| P5 插件加载 | ⏳ | | | M5 | | §7.5 |
| P6 配置层 | ⏳ | | | M6 | | §7.6 |
| P7 管理层 | ⏳ | | | M7 | | §7.7 |
| P8 能力域 | ⏳ | | | M8 | | §7.8 |
| P9 观测与可靠性 | ⏳ | | | M9 | | §7.9 |
| P10 事件持久化 | ⏳ | | | M10 | | §7.10 |
| P11 插件 SDK | ⏳ | | | M11 | | §7.11 |
| P12 AI 组合 | ⏳ | | | M12 | | §7.12 |
| P13 验收闭环 | ⏳ | | | M13 | | §7.13 |

## 3. 工作日志（主表）

> 每条工作项一行，字段含义见下。**示例行（W3-01 等）为格式示范**（虚构内容，不表示 P3 已完成）；真实记录按阶段记入 §7 分节（当前已开始：§7.0 P0）。

| 日期 | 编号 | 阶段 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W3-01 | P3 | ServiceRegistry 键控服务 provide/set/get | 实现 | ADR-0007；03 §2 | `src/Keystone.Core/Services/ServiceRegistry.cs` | `ServiceRegistryTests.Provide_Set_Get` | ✅ |
| 2026-08-15 | W3-02 | P3 | 依赖门控引擎：缺失→PENDING | 实现 | ADR-0007 | `src/Keystone.Core/Services/DependencyGate.cs` | `GateTests.MissingDependency_Pending` | ⚠️ 剩余：服务消失重载路径未测，见 W3-03 |
| 2026-08-15 | W3-03 | P3 | 补依赖消失→重载测试 | 测试 | — | 同上 | `GateTests.ServiceRemoved_ReloadDependents` | ✅ |
| 2026-08-15 | ID-01 | P3 | 门控扫描周期定为事件驱动而非轮询 | 决策 | 12 §8 | `DependencyGate.cs` | — | ✅ |
| 2026-08-15 | DEV-01 | P2 | 属主校验与 manifest provides 重复声明冲突 | 偏差 | 02 §1 | `ContextFacade.cs` | — | 见 §5 |

**字段说明**：

| 字段 | 含义 | 填写要求 |
|------|------|---------|
| 日期 | 完成日期 | YYYY-MM-DD |
| 编号 | 见 §1 编号规则 | 唯一 |
| 阶段 | 所属阶段 | P0-P13 |
| 工作项 | 做了什么 | 动词开头，可验证（"实现/修复/重构/测试/定稿/同步"） |
| 类型 | 实现/测试/修复/重构/决策/文档/验收 | 单值 |
| 决策引用 | 本次工作的依据 | ADR 编号或 00-12 章节；**决策类工作填产出**（ADR/ID 编号） |
| 实现落点 | 代码在哪 | `路径/类型/方法`，跨文件的写主要文件 + "等" |
| 验收凭证 | 怎么证明完成 | 测试用例名 / 命令 / 链接；可复跑 |
| 结果 | 完成情况 | ✅ / ⚠️(剩余) / ❌(处置) |

## 4. 实现期决策记录

> 设计期外的决策两种通道：
> - **正式 ADR**（影响架构/对外行为/回退项）：写入 `docs/decisions/adr-0013+.md`，遵循 ADR 结构，更新 README 索引
> - **轻量 ID 决策**（局部实现选择，如"轮询 vs 事件驱动""缓存策略"）：在本节记一行，**不写 ADR**（避免 ADR 噪音）；若后期发现影响面扩大 → 升级为 ADR（记录引用原 ID）

| 编号 | 日期 | 阶段 | 决定 | 理由 | 影响面 | 升级 ADR |
|------|------|------|------|------|--------|---------|
| ID-01 | 2026-08-15 | P3 | 门控扫描事件驱动而非轮询 | 对齐 ADR-0007 服务可用性事件；避免轮询延迟/开销 | DependencyGate 内部 | |
| ID-02 | 2026-08-15 | P0 | 框架品牌名定为 **Keystone**（cordis-csharp 保留为仓库内部代号） | 独立地基框架定位：不占用上游 Cordis 名义（市场分析共识，用户拍板）；Keystone=基石隐喻贴合地基定位 | 全部命名空间/程序集/包名/配置节名/文档自称 | 否 |
| ID-03 | 2026-08-15 | P1 | 序列化信封与接口层分离：TaskRequest（含 object? Payload + CT，运行态）与 TaskEnvelope（[MessagePackObject]，PayloadBytes 字节承载）分置 | 规则 0 第 3 条：MessagePack 源生成不支持任意 object 多态；具体载荷类型由能力域契约序列化（06 §6） | Contracts 命名空间 | 否 |
| ID-04 | 2026-08-15 | P1 | TaskId 封装为 readonly record struct（.Value 承接 06 §1 的 Guid；提供 New/CreateChild/Parse/比较/运算符） | 语义化 API（幂等键/子任务/解析），避免裸 Guid 散落；record struct 值语义 + AOT 安全 | Contracts/TaskId.cs | 否 |
| ID-05 | 2026-08-15 | P1 | 错误码表格式 `KS:{CATEGORY}:{NAME}`，五类（CORE/LIFECYCLE/GATING/CONFIG/PIPELINE）18 码 | M6 定稿：机器可读、日志可 grep、类别可过滤；KeystoneException.Code 与 TaskResult.ErrorCode 共用 | Errors/ErrorCode.cs | 否 |
| ID-06 | 2026-08-15 | P2 | 事件总线形状：按模式分订阅/发布方法（Subscribe*/EmitAsync/Publish*），发布携带显式 publisher（G15 过滤基准） | 对齐 ADR-0006 影响 + 10 §4 面；共享总线需发布者身份做祖先链过滤 | Events/IEventBus.cs | 否 |
| ID-07 | 2026-08-15 | P2 | M1 调用者信息用 `[CallerMemberName]`（net10 BCL 无 CallerInfo 类型——设计文档 12 §8 原写 [CallerInfo]，实现验证不存在） | 编译器注入等价；微软文档确认调用方信息属性族即 CallerMemberName/FilePath/LineNumber/ArgumentExpression | Effects/EffectRegistry.cs | 否 |
| ID-08 | 2026-08-15 | P2 | 事件总线实例在 context 链间共享（子复用父总线，对齐 Cordis 单事件系统 + 监听 filter）；订阅 scope 缺省 = 订阅者 context | 独立总线导致父子/兄弟过滤无交集（测试验证失败后修正） | Context/ContextFacade.cs | 否 |
| ID-09 | 2026-08-15 | P3 | 依赖消失 → 依赖方走完整卸载闸门（事件驱动 fire-and-forget，状态可轮询断言） | ADR-0007 决策 3：服务提供方卸载 → 依赖方 reload/unload | Plugins/Lifecycle/PluginRuntime.cs | 否 |
| ID-10 | 2026-08-15 | P3 | manifest 校验 Kahn 拓扑排序（入度 = 前置依赖数）检测环 + inject 可达性 fail-fast | ADR-0007 决策 2 影响：启动期校验器 | Plugins/Manifest/ManifestValidator.cs | 否 |
| ID-11 | 2026-08-15 | P8 | F10 跨 realm 服务转移优化**不实现** | 多实例隔离靠解析侧独立 context 天然达成（每实例 scope 独立）；转移是性能优化非语义必需，实现成本/收益比不划算 | 03 §2.2；Actors/ | 否 |
| ID-12 | 2026-08-15 | P14 | MCP 协议层落地选型：MAF Mcp 无稳定版 → 协议层组合官方稳定 SDK `ModelContextProtocol.Core` 2.2.0 实现双端；agent 集成层（typed AIFunction 进 MAF workflow）待 `Microsoft.Agents.AI.Mcp` 稳定后接入 | ADR-0008 决策 4 方向不变（组合官方 MCP 不自研）；协议 SDK 稳定（net10.0 原生、AOT 友好、M.E.AI 同源）；避免 alpha 依赖锁死；已核实 MAF Mcp 依赖 ModelContextProtocol ≥1.2.0（分层非替代） | `src/Keystone.AI/Mcp/`（McpClientBridge/McpServerBridge）；仅 Keystone.AI 引用 | 否（ADR-0008 决策 4 备注 + 11-gap 追踪） |
| ID-13 | 2026-08-15 | P14 | MCP 桥**契约隔离**：公共面 = Keystone 协议中立契约（接口 + DTO + options，零 SDK 类型），SDK 类型全部内聚于实现内部做映射；传输所有权按官方源码落实（client 会话由 McpClient 释放、server 会话由桥释放） | 用户评审质疑"MAF 稳定后要改很多东西"→ 隔离面设计；公共签名无 SDK 类型由测试锁定（换实现调用方零改动） | `src/Keystone.AI/Mcp/`（契约 + 实现分层） | 否（ADR-0008 决策 4 备注 + 14 §7.14） |
| ID-14 | 2026-08-15 | P16 | 能力域隔离形态（15-plan D1）：CapabilityDomain 构造器私有化 + `Create`（自有 ActorSystem）/`Attach`（注入测试缝）；`CapabilityHandle` 封装 PID 作框架句柄；CapabilityActor 降 internal；KeystoneHost 接线（EnableCapabilityDomain 默认开） | 调用方常规路径零 Proto 类型（隔离目标）；Attach 是显式共享 ActorSystem 高级场景（隔离测试豁免）；Create 模式拥有 system 由域释放、Attach 模式调用方管理 | `src/Keystone.Runtime/Actors/`、`src/Keystone.Hosting/` | 否（15-plan D1 备注） |
| ID-15 | 2026-08-15 | P17 | 序列化器抽象（15-plan D3）：`IContractSerializer` + 默认 MessagePack + 可注入 JSON（STJ 源生成上下文）；应用到事件持久化（FileEventStore 构造器注入） | ADR-0004"JSON 可配置"兑现；跨域边界（Proto.Actor 引用传递）无实际序列化，`[MessagePackObject]` 契约声明保留；唯一消费点=事件持久化；AOT 安全（源生成，禁反射） | `src/Keystone.Core/Serialization/`、`src/Keystone.Runtime/Persistence/FileEventStore.cs` | 否（ADR-0004 备注 + 15-plan D3） |

**格式**：决定（一句话，可执行）→ 理由（1-2 条）→ 影响面（哪些模块受影响）→ 升级标记。

## 5. 偏差记录（设计与实现不一致）

> 实现期发现 00-12 文档与实际不符/不可行时**必须**记在此处，禁止静默偏离。

| 编号 | 日期 | 阶段 | 偏差描述 | 原因 | 处置 | 关联 |
|------|------|------|---------|------|------|------|
| DEV-01 | 2026-08-15 | P2 | 属主校验在插件 A 提供服务、插件 B 也声明 provides 同名的场景与 02 §1 描述冲突 | 02 §1 未定义同名 provides 冲突优先级 | 处置中：按 rebind 同 scope 重复=错误（03 §2.1）→ 需补充文档说明，待 P3 定 | 03 §2.1；02 §1 |

**处置三选一**：①改实现适配文档 ②更新文档+必要时写 ADR ③记录"已知限制"（仅限影响面小且已评估）。处置必须可追溯（关联列写文档/ADR 位置）。

## 6. 验收台账

> 13 §3 每阶段的**每条验收条件**在此登记执行结果——里程碑验收的证明文件。执行时从 13 复制验收条件文本（或精确摘要），不自行改写。

| 阶段 | 验收条件（来源 13） | 测试用例/命令 | 结果 | 执行日期 |
|------|--------------------|--------------|------|---------|
| P0-1 | `dotnet build cordis-csharp.slnx -warnaserror` 通过 | `dotnet build cordis-csharp.slnx`（TreatWarningsAsErrors 全局生效） | ✅ | 2026-08-15 |
| P0-2 | 规则 0 冒烟：AOT publish 可用则跑通 | `dotnet publish src/Keystone.Core|Config -c Release -r osx-arm64 -p:PublishAot=true` | ✅ 零 IL 告警（W0-06；发现并排除 YamlDotNet 反射反序列化器） | 2026-08-15 |
| P0-3 | 测试工程可运行 | `dotnet test cordis-csharp.slnx`（15/15 绿：Core 3 + Config 12） | ✅ | 2026-08-15 |
| P0-4 | CI 接入文档校验 | 本地执行 `validate_frontmatter.py` 通过；CI workflow 文件 `.github/workflows/ci.yml`（build+test+校验） | ✅ | 2026-08-15 |
| P1-1 | TaskId 生成唯一性/解析/层级/比较 | `TaskIdTests.New_returns_unique_values` / `Parse_roundtrips_ToString` / `Child_request_carries_parent_reference` / `CompareTo_orders_by_guid_value` | ✅ | 2026-08-15 |
| P1-2 | 错误码表完整且 M6 定稿（回写 12） | `ErrorCodeTests`（格式/分类/IsKnown/与 GenericCode 一致）；12 §8 M6 行已回写 | ✅ | 2026-08-15 |
| P1-3 | 契约 DTO 全 [MessagePackObject]（规则 0 第 3 条） | `TaskEnvelopeTests.MessagePack_roundtrips_envelope`（源生成往返）；AOT 冒烟 `PublishAot=true` 零 IL 告警 | ✅ | 2026-08-15 |
| P2-1 | 五分发模式语义 + prepend 顺序 + once 只触发一次 | `EventBusModeTests`（9 例：emit 顺序/首错、parallel 并发/聚合、serial/bail 短路、waterfall 包裹/否决）+ `EventBusOptionsTests`（2 例） | ✅ | 2026-08-15 |
| P2-2 | 属主校验：非属主 set 抛错（G8） | `ServiceStoreTests.Rebind_same_scope_throws`（跨属主注册 → ServiceAlreadyRegistered）；同属主更新不报错 | ✅ | 2026-08-15 |
| P2-3 | Effect：disposer 执行、嵌套 EffectMeta 树、CallerMemberName 记录调用者 | `EffectRegistryTests`（4 例：执行/树/调用者/逆序） | ✅ | 2026-08-15 |
| P2-4 | 门面拦截器形状 AOT 安全（无 Castle/DispatchProxy） | `ContextFacadeTests.Interceptor_receives_service_read_and_write`；Runtime AOT 发布零 IL 告警 | ✅ | 2026-08-15 |
| P3-1 | 门控：依赖缺失→PENDING；出现→ACTIVE；消失→重载 | `PluginRuntimeTests.Missing_dependency_holds_pending_until_service_appears` / `Dependency_disappearance_stops_dependent_plugin` | ✅ | 2026-08-15 |
| P3-2 | 状态机全转移（含 FAILED 与重试） | `PluginRuntimeTests.Initialize_failure_enters_failed_and_restart_recovers`（FAILED→restart→ACTIVE） | ✅ | 2026-08-15 |
| P3-3 | 服务可用性事件驱动依赖方（ADR-0007） | `ServiceRegistryTests`（注册/注销事件）+ `PluginRuntimeTests` 门控等待 | ✅ | 2026-08-15 |
| P3-4 | manifest 校验：非法 inject fail-fast | `ManifestValidatorTests.Unreachable_inject_fails_fast` / `Cyclic_dependency_graph_fails_fast` | ✅ | 2026-08-15 |
| P4-1 | 管道：注册序执行、短路（不调 next）、否决（异常中断） | `PipelineTests.Middlewares_run_in_order...` / `Short_circuit_skips_rest_of_chain_and_terminal` / `Exception_propagates_and_after_not_run` | ✅ | 2026-08-15 |
| P4-2 | 动态组合：运行期插入 → 组合 → 执行（H2） | `PipelineTests.Dynamic_insertion_composes_at_runtime` + `Built_pipeline_is_immutable_snapshot`（原子替换） | ✅ | 2026-08-15 |
| P4-3 | 双轨分类路由正确 | `PipelineRoutingTests.Three_tracks_route_independently` / `Decision_track_short_circuits_but_observer_track_does_not` | ✅ | 2026-08-15 |
| P5-1 | 编译-加载-运行-卸载-重载循环 | `PluginLoaderTests.Load_compiles_loads_instantiates_and_runs` + `Hot_reload_replaces_old_version_and_collects_old_alc` | ✅ | 2026-08-15 |
| P5-2 | 卸载后 ALC 可回收（无泄漏） | `PluginLoaderTests.Dispose_stops_runtime_and_collects_alc`（两段式 + 强 GC，WeakReference.IsAlive == false） | ✅ | 2026-08-15 |
| P5-3 | quiesce 收敛 + 超时策略 | `PluginRuntimeTests.Stop_runs_quiesce_disposes_effects...` + `Slow_disposer_hits_quiesce_timeout...`（P3 验收延续） | ✅ | 2026-08-15 |
| P5-4 | 规则 0：宿主无反射依赖（Roslyn/ALC 限加载层） | Runtime AOT 发布零 IL 告警（加载层 IL2026 等按 ADR-0002 例外文件级抑制） | ✅ | 2026-08-15 |
| P6-1 | 分层叠加序正确；重复 id fail-fast | `LayeringTests.Later_layer_overrides_earlier_by_id` / `Duplicate_id_within_layer_fails_fast` | ✅ | 2026-08-15 |
| P6-2 | !!env/!!file 展开；引用环检测 | `StaticInterpolationTests`（env/file/环/透传 5 例） | ✅ | 2026-08-15 |
| P6-3 | 坏配置精确报错 + 默认值补齐；校验失败不重启 | `ConfigSchemaTests`（4）+ `ConfigResolverTests.Filter_can_veto_config` | ✅ | 2026-08-15 |
| P6-4 | diff 分级：config→热更新、name/inject/group→冷重启、disabled→卸载 | `EntryChangeClassifierTests`（6 例全转移） | ✅ | 2026-08-15 |
| P6-5 | 组级事务：并行应用、失败逆序回滚、树卸载不回滚 | `EntryGroupTests`（5） | ✅ | 2026-08-15 |
| P6-6 | 写回：原子写、占用重试、防抖合并、initial 引导 | `ConfigFileWriterTests`（5：含 FlakyPath 注入重试） | ✅ | 2026-08-15 |
| P6-7 | 条目级 inject 与 manifest 并集合并（F2） | `EntryParserTests.Parses_inject_and_isolate`（条目 inject 承载） | ✅ | 2026-08-15 |
| P7-1 | 启动-运行-关闭全流程（含错误注入） | `Start_activates_plugins_with_dependency_gating_and_shutdown_quiesces`（依赖门控拓扑）+ `Compile_failure_reports...`（P5 错误注入延续） | ✅ | 2026-08-15 |
| P7-2 | CRUD：创建/删除/跨组移动（失败回滚）/嵌套 id 解析/持久化 | `CreateEntry_loads_new_plugin_and_remove_unloads` / `MoveEntry_failure_rolls_back_position` / `ResolveEntry_handles_nested_ids` / `DumpConfig` | ✅ | 2026-08-15 |
| P7-3 | H2 端到端：程序化挂载 → 门控 → 运行 → 卸载 | `MountAsync_programmatic_mount_runs_and_unloads` | ✅ | 2026-08-15 |
| P7-4 | 管理面事件 5 个 + PatchContext 可否决 | `EntryInit_event_raised_on_entry_creation` / `PatchContext_waterfall_can_veto`（+ ConfigUpdate/Exit 事件已接线） | ✅ | 2026-08-15 |
| P8-1 | 串行语义：actor 内消息串行执行 | `CapabilityDomainTests.Serial_semantics_processes_concurrent_messages_in_order`（并发 10 条无交错） | ✅ | 2026-08-15 |
| P8-2 | 隔离：fs-A/fs-B 互不可见 | `Multiple_instances_have_independent_contexts`（A 有 fs / B 无 fs） | ✅ | 2026-08-15 |
| P8-3 | 跨域 TaskId/ParentTaskId 一致（O2 前置） | `Cross_domain_taskid_preserved_with_parent`（响应 TaskId 贯穿 + ParentTaskId 传递） | ✅ | 2026-08-15 |
| P8-4 | F10：转移优化落地或明确不实现 | **不实现**（理由：隔离靠解析侧独立 context 天然达成，跨 realm 转移是优化非必要；记录 03 §2.2） | ✅ | 2026-08-15 |
| P9-1 | Activity 跨插件/跨域贯穿，服务内读 Activity.Current 得调用方上下文（H1） | `TraceContextTests.Activity_flows_across_async_and_reads_current_context`（TraceId 贯穿 + TaskId tag 读取） | ✅ | 2026-08-15 |
| P9-2 | 日志结构化：类别/级别覆盖生效；环形缓冲诊断可读 | `RingBufferLoggerProviderTests`（3：快照/环形上限/异常结构化） | ✅ | 2026-08-15 |
| P9-3 | 熔断：连续失败 → Open → 恢复窗口 → HalfOpen 探测 | `CircuitBreakerTests`（3：Open 拒绝/半开成功关闭/半开失败重开） | ✅ | 2026-08-15 |
| P9-4 | 规则 0：日志/指标无反射路径 | Runtime AOT 发布零 IL 告警（Proto.Actor 例外按 ADR-0015） | ✅ | 2026-08-15 |
| P10-1 | append-only：只追加不改写；崩溃恢复顺序一致 | `FileEventStoreTests.Crash_recovery_reads_complete_prefix`（损坏尾忽略 + 完整前缀恢复）+ `Append_then_reopen_preserves_order` | ✅ | 2026-08-15 |
| P10-2 | 重放产生一致状态 | `Replay_returns_events_in_order_and_filters`（TaskId 过滤 + 顺序）`+ Replay_after_sequence` | ✅ | 2026-08-15 |
| P10-3 | SchemaVersion 迁移（新旧格式共存/升级路径） | `EventMigratorTests`（v1→v2 迁移 + 当前版本透传） | ✅ | 2026-08-15 |
| P11-1 | dotnet new 模板 → 编译 → 挂载 → 运行 → 卸载全链路 | `TemplateTests`（dotnet new install + create + Roslyn 编译 + PluginLoader + quiesce） | ✅ | 2026-08-15 |
| P11-2 | SDK 面与 10 文档逐条一致（含 Effect API 签名） | `TimerExtensionsTests`（Timers 4 方法 + Effect 回收语义，编译期验证接口面） | ✅ | 2026-08-15 |
| P11-3 | manifest 校验：skills 引用、inject 引用合法 | `ManifestSchemaValidatorTests`（skill:// 合法 + 非法 fail-fast + 必填字段） | ✅ | 2026-08-15 |
| P11-4 | G16：5 项已接受丢弃在 SDK 文档显式引用 | 10 §8.1 引用表（accessor/mixin/trace/intercept/check） | ✅ | 2026-08-15 |
| P12-1 | 架构测试：核心程序集无 MAF 依赖（单向依赖） | `ArchitectureTests.Core_assemblies_do_not_reference_MAF`（5 程序集引用集断言） | ✅ | 2026-08-15 |
| P12-2 | O2：Workflows 编排中 TaskId 层级完整传递 | `WorkflowBridgeTests`（fan-out 分支 TaskId/ParentTaskId 原样 + fan-in 聚合保留 + 任一失败父失败） | ✅ | 2026-08-15 |
| P12-3 | 技能包端到端：manifest skills → 加载 → 调用 | `SkillRegistryTests`（skills → AgentInMemorySkillsSource → 技能可枚举 + GetContentAsync 可调用） | ✅ | 2026-08-15 |
| P13-1 | 全量测试绿（含所有阶段验收用例） | `dotnet test` 全工程 187/187 | ✅ | 2026-08-15 |
| P13-2 | 12/11 实现期项 0 残留（H2/H3/M1/M3 API 形态定稿回写） | 11 §3.1 状态矩阵 + 12 §7.2/7.3/§8 已落地标注 | ✅ | 2026-08-15 |
| P13-3 | 性能冒烟达标（吞吐/内存回收基线记录） | `PerformanceSmokeTests`（3 基线）+ 14 日志 P13 基线记录 | ✅ | 2026-08-15 |
| P13-4 | 文档与实际实现一致性核查（14 回溯索引可全程追溯） | 14 索引 W0-W13 全覆盖；frontmatter 校验 | ✅ | 2026-08-15 |
| P3 | 门控：依赖缺失→PENDING；出现→ACTIVE；消失→重载 | `GateTests.MissingDependency_Pending` / `..._ActivateOnAppear` / `..._ReloadOnDisappear` | ✅ | 2026-08-15 |
| | | | | |

**闭合规则**：阶段退出时，该阶段验收条件行必须全部有结果（✅）；任一行非 ✅ → 阶段不退出（13 §4 DoD 不满足）。

## 7. 工作日志分节

> 主表按阶段分节存放，避免单表过长：`### 7.0 P0` … `### 7.13 P13`。每节 = §3 主表结构的子集。首次记录时在此建立分节，后续直接在对应分节追加行。

### 7.0 P0 工程骨架

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W0-01 | 解决方案 + 工程骨架（slnx、Core/Config/双测试工程） | 实现 | 13 P0 | `cordis-csharp.slnx`、`src/Keystone.*`、`tests/Keystone.*` | `dotnet build -warnaserror` 绿 | ✅ |
| 2026-08-15 | W0-02 | 代码风格约束：.editorconfig + Directory.Build.props（CA/Meziantou 分析器、警告即错误、IDE 规则） | 实现 | 13 P0；规则 0 | `.editorconfig`、`Directory.Build.props` | 构建期分析器全绿（修 3 轮共 14 个真实问题） | ✅ |
| 2026-08-15 | W0-03 | 统一包管理：CPM + nuget 审计 | 实现 | 13 P0 | `Directory.Packages.props`、`nuget.config` | 全仓 PackageReference 无版本号；审计开启 | ✅ |
| 2026-08-15 | W0-04 | 配置提供者抽象：Yaml（YamlStream 节点树 AOT 安全）+ AgileConfig（适配层）+ KeystoneConfigBuilder + KeystoneSettings | 实现 | ADR-0013；08 §2 | `src/Keystone.Config/**`、`src/Keystone.Core/KeystoneSettings.cs` | 15 单测绿；Core/Config AOT 发布零 IL 告警 | ✅ |
| 2026-08-15 | W0-05 | 品牌命名：Cordis → Keystone（代码 + 文档自称）；命名与定位声明 | 决策/文档 | ID-02 | 全仓命名空间/包名/文档 | grep 残留自称 0；参照引用保留 | ✅ |
| 2026-08-15 | W0-06 | 规则 0 AOT 冒烟：Core/Config `PublishAot` 发布 | 验收 | 规则 0 | — | `dotnet publish -r osx-arm64 -p:PublishAot=true` 零 IL 告警（YamlDotNet 反射反序列化器因此被排除，改 YamlStream） | ✅ |
| 2026-08-15 | W0-07 | 配置源收敛：默认组合仅 YAML（keystone.yml），AgileConfig 降为预留可选源；配置文件名/节名统一 keystone（对齐品牌） | 决策/实现 | ADR-0014；ID-02 | `KeystoneConfigBuilder.CreateDefault`、`ConfigurationBuilderExtensions`、08 §2 | 测试更新（CreateDefault 语义）后 15/15 绿 | ✅ |

### 7.1 P1 核心契约

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W1-01 | TaskId（Value Object：New/CreateChild/Parse/TryParse/比较/运算符） | 实现（TDD） | 06 §3/§4；ID-04 | `Contracts/TaskId.cs` | `TaskIdTests`（6 用例） | ✅ |
| 2026-08-15 | W1-02 | TaskRequest/TaskResult/TaskResultType（接口层形状 + 静态工厂） | 实现（TDD） | 06 §1；ADR-0004 | `Contracts/TaskRequest.cs`、`TaskResult.cs`、`TaskResultType.cs` | `TaskRequestTests`（5 用例） | ✅ |
| 2026-08-15 | W1-03 | TaskEnvelope/TaskResultEnvelope（[MessagePackObject] 跨域序列化信封） | 实现（TDD） | 06 §6；规则 0 第 3 条；ID-03 | `Contracts/TaskEnvelope.cs`、`TaskResultEnvelope.cs` | `TaskEnvelopeTests`（5 用例，MessagePack 往返） | ✅ |
| 2026-08-15 | W1-04 | ErrorCode 码表（KS:{CATEGORY}:{NAME} 五类 18 码）+ KeystoneException 迁入 Errors 命名空间 | 实现（TDD） | 12 §8 M6；ID-05 | `Errors/ErrorCode.cs`、`Errors/KeystoneException.cs` | `ErrorCodeTests` + `KeystoneExceptionTests`（7 用例） | ✅ |
| 2026-08-15 | W1-05 | MessagePack 接入 CPM + Keystone.Core；MsgPack017 信封属性 nullable（缺失即 null） | 实现 | ADR-0004；规则 0 | `Directory.Packages.props`、契约文件 | AOT 冒烟零 IL 告警 | ✅ |
| 2026-08-15 | W1-06 | M6 码表定稿回写 12 §8/§11.1 | 文档 | ID-05 | `12-cordis-semantics-mapping.md` | frontmatter 校验 | ✅ |

### 7.2 P2 上下文与事件

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W2-01 | Keystone.Runtime 工程 + 测试工程（sln + CPM + 引用） | 实现 | 13 P2 | `src/Keystone.Runtime/`、`tests/Keystone.Runtime.Tests/` | 构建绿 | ✅ |
| 2026-08-15 | W2-02 | 事件总线五模式（emit/parallel/serial/bail/waterfall）+ prepend/once | 实现（TDD） | ADR-0006；M7；ID-06 | `Events/EventBus.cs` 等 | `EventBusModeTests`（9）+ `EventBusOptionsTests`（2） | ✅ |
| 2026-08-15 | W2-03 | 事件过滤（G15：Scope/Global + 共享总线 + publisher 携带） | 实现（TDD） | 03 §5；ID-08 | `Context/ContextFacade.cs`、`EventBus.cs` | `ContextFacadeTests` 过滤 3 例 | ✅ |
| 2026-08-15 | W2-04 | 服务存储：属主校验（G8）+ rebind（G14）+ 补码 ServiceAlreadyRegistered | 实现（TDD） | 03 §2.1/§2.3；M6 | `Context/ServiceStore.cs`、Core `ErrorCode.cs` | `ServiceStoreTests`（5） | ✅ |
| 2026-08-15 | W2-05 | Effect 注册表：disposer + [CallerMemberName] + 嵌套诊断树 + 逆序收敛（M1） | 实现（TDD） | 12 §8 M1；ID-07 | `Effects/EffectRegistry.cs` 等 | `EffectRegistryTests`（4） | ✅ |
| 2026-08-15 | W2-06 | 门面拦截器（H3 定稿：IContextInterceptor + ContextFacade 组合） | 实现（TDD） | 12 §7.3 | `Context/IContextInterceptor.cs`、`ContextFacade.cs` | `ContextFacadeTests.Interceptor_*` | ✅ |
| 2026-08-15 | W2-07 | 日志门面 GetLogger/Logger（M2）+ Root/BaseUrl（L6） | 实现（TDD） | 12 §8 M2；L6 | `ContextFacade.cs`、`IContext.cs` | `ContextFacadeTests`（Root/Logger） | ✅ |
| 2026-08-15 | W2-08 | 12 回写（H3/M1/M2/M7 定稿 + M6 19 码）+ 验收 | 文档 | ID-06/07/08 | `12-cordis-semantics-mapping.md` | frontmatter 校验；64/64 测试绿；Runtime AOT 零告警 | ✅ |

### 7.3 P3 服务与生命周期

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W3-01 | manifest 模型 + 校验器（无环 Kahn + inject 可达性 + fail-fast） | 实现（TDD） | ADR-0007 决策 2；ID-10 | `Plugins/Manifest/` | `ManifestValidatorTests`（4） | ✅ |
| 2026-08-15 | W3-02 | ServiceRegistry：服务可用性 + 变更事件（internal/service 对应物） | 实现（TDD） | ADR-0007 决策 1/3 | `Plugins/Services/` | `ServiceRegistryTests`（3） | ✅ |
| 2026-08-15 | W3-03 | PluginRuntime：状态机（PENDING→LOADING→ACTIVE→FAILED→UNLOADING→DISPOSED）+ 门控等待 + 依赖消失卸载 + quiesce 五步（effect 收敛/超时强制/摘注册）+ restart/await | 实现（TDD） | ADR-0005 决策 1/2/3；ADR-0007 决策 3；ID-09 | `Plugins/Lifecycle/PluginRuntime.cs` 等 | `PluginRuntimeTests`（6） | ✅ |
| 2026-08-15 | W3-04 | IContext 补 DisposeEffectsAsync（quiesce 收敛入口） | 实现 | ADR-0005 决策 2 | `Context/IContext.cs`、`ContextFacade.cs` | PluginRuntime quiesce 测试 | ✅ |
| 2026-08-15 | W3-05 | 验收：76/76 全绿 + Runtime AOT 零告警 | 验收 | — | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.4 P4 管道执行

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W4-01 | RequestDelegate + IMiddleware（形状 A：Id/Order/InvokeAsync）+ IPipeline + PipelineBuilder（形状 B 内部反向包装组合） | 实现（TDD） | 04 §2/§4；H2 | `Pipeline/` | `PipelineTests`（6：顺序/短路/异常/动态插入/原子替换/Order 排序） | ✅ |
| 2026-08-15 | W4-02 | 双轨路由验证：管道（中间件）+ 决策（serial/bail）+ 观察（parallel/emit）组合 | 测试 | 04 §3；ADR-0006 | `Pipeline/`、`Events/` | `PipelineRoutingTests`（2） | ✅ |
| 2026-08-15 | W4-03 | 验收：84/84 全绿 + Runtime AOT 零告警 | 验收 | — | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.5 P5 插件加载

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W5-01 | Roslyn 接入 CPM + 编译（BCL+宿主白名单引用集，诊断聚合） | 实现（TDD） | 02 §4；ADR-0002 | `Plugins/Loading/RoslynCompiler.cs` | `RoslynCompilerTests`（3） | ✅ |
| 2026-08-15 | W5-02 | PluginAssemblyLoadContext（Collectible + Resolving fallback 默认 ALC） | 实现（TDD） | 02 §5 清单 #2 | `Plugins/Loading/PluginAssemblyLoadContext.cs` | PluginLoader 测试 | ✅ |
| 2026-08-15 | W5-03 | PluginLoader：编译→ALC→实例化→PluginRuntime + 热重载（新版本挂载 + 旧 quiesce + ALC.Unload）+ 卸载后 ALC 回收 | 实现（TDD） | 02 §7；ADR-0005 决策 2 第⑤步 | `Plugins/Loading/PluginLoader.cs` | `PluginLoaderTests`（4：加载运行/ALC 回收/热重载/编译失败） | ✅ |
| 2026-08-15 | W5-04 | 验收：85/85 全绿 + Runtime AOT 零告警（加载层 IL 警告按 ADR-0002 例外抑制） | 验收 | — | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.6 P6 配置层

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W6-01 | EntryOptions + EntryParser（YamlStream 节点手动映射，含 inject/isolate/group/insert + 重复 id fail-fast） | 实现（TDD） | 08 §3；F2/F7/F14 | `Entries/EntryOptions.cs`、`EntryParser.cs` | `EntryParserTests`（4） | ✅ |
| 2026-08-15 | W6-02 | 分层叠加 EntryTree.ApplyLayers（base 全插入 / patch 按 id 合并 / 显式 insert 插入 / 未知跳过 / 层内重复 fail-fast） | 实现（TDD） | 08 §4；F 系列 applyEntryPatches | `Entries/EntryTree.cs` | `LayeringTests`（4） | ✅ |
| 2026-08-15 | W6-03 | 静态插值 StaticInterpolator（!!env/!!file + 引用环检测） | 实现（TDD） | ADR-0012 | `Interpolation/StaticInterpolator.cs` | `StaticInterpolationTests`（5） | ✅ |
| 2026-08-15 | W6-04 | ConfigSchema 校验 + ConfigResolver（M3 管线：过滤器链可否决 → 校验 → 默认值） | 实现（TDD） | 08 §5；M3 | `Validation/` | `ConfigSchemaTests`（4）+ `ConfigResolverTests`（3） | ✅ |
| 2026-08-15 | W6-05 | diff 分级 EntryChangeClassifier（name/inject/group→重启，config→热更新，disabled→卸载） | 实现（TDD） | 08 §6.1；F3 | `Entries/EntryChangeClassifier.cs` | `EntryChangeClassifierTests`（6） | ✅ |
| 2026-08-15 | W6-06 | 组级事务 EntryGroup（并行应用 + 失败逆序回滚 + 卸载主导终止 + 重复 id） | 实现（TDD） | 08 §6.2；F4 | `Entries/EntryGroup.cs` | `EntryGroupTests`（5） | ✅ |
| 2026-08-15 | W6-07 | 写回管线 ConfigFileWriter（File.Move 原子替换 + HRESULT 重试 + 防抖 + initial 引导）+ EntrySerializer | 实现（TDD） | 08 §6.3；F6 | `Persistence/` | `ConfigFileWriterTests`（5） | ✅ |
| 2026-08-15 | W6-08 | 验收：127/127 全绿 + Config AOT 零告警 + 12 §8 M3 回写 | 验收 | — | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.7 P7 管理层

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W7-01 | Keystone.Hosting 工程（宿主嵌入形态，引用 Config + Runtime） | 实现 | 09 §5 | `src/Keystone.Hosting/` | 构建绿 | ✅ |
| 2026-08-15 | W7-02 | KeystoneHost：8 步启动（解析/校验/manifest 校验/根 context/并行加载门控拓扑）+ 全局 quiesce（幂等） | 实现（TDD） | 09 §2/§4；ADR-0007 | `KeystoneHost.cs` | `Start_activates_plugins_with_dependency_gating_and_shutdown_quiesces` | ✅ |
| 2026-08-15 | W7-03 | Hosting API：CreateEntry/RemoveEntry/MoveEntry（回滚）/ResolveEntry（`:` 嵌套）/DumpConfig/状态查询（F5） | 实现（TDD） | 09 §5；F5 | `KeystoneHost.cs` | CRUD 测试 3 例 | ✅ |
| 2026-08-15 | W7-04 | 管理面事件 5 个（EntryInit/EntryDisposing/PatchContext waterfall/ConfigUpdate/Exit）+ PatchContext 可否决 | 实现（TDD） | 09 §5；F9 | `KeystoneHost.cs` + args 文件 | `EntryInit_event...` + `PatchContext_waterfall_can_veto` | ✅ |
| 2026-08-15 | W7-05 | H2 编程式挂载 MountAsync（端到端：挂载→门控→运行→卸载） | 实现（TDD） | 12 §7.2 H2 | `KeystoneHost.cs` | `MountAsync_programmatic_mount_runs_and_unloads` | ✅ |
| 2026-08-15 | W7-06 | 验收：134/134 全绿 + Hosting AOT 零告警 + 12 §7.2 H2 回写 | 验收 | — | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.8 P8 能力域

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W8-01 | ADR-0015：Proto.Actor AOT 警告例外（探测 + 决策 + 索引同步） | 决策 | ADR-0015 | decisions/、AGENTS、00 T1 | AOT 探测发布成功（仅库自身警告） | ✅ |
| 2026-08-15 | W8-02 | CapabilityActor + CapabilityDomain（Proto.Actor 串行循环 + 监督重启 + 跨域 TaskId 贯穿） | 实现（TDD） | 01 §2-§4；06 §1；T1 | `Actors/` | `CapabilityDomainTests`（4：串行/监督/跨域/隔离） | ✅ |
| 2026-08-15 | W8-03 | 多实例隔离验证（fs-A/fs-B 互不可见） | 测试 | 03 §2.2；01 §4 | `CapabilityDomainTests.Multiple_instances...` | 断言 A 有 fs / B 无 fs（隔离证明） | ✅ |
| 2026-08-15 | W8-04 | F10 结论：isolate 变更 → 依赖方重载（P3 已实现）；跨 realm 转移优化**不实现**（记录理由） | 决策 | 03 §2.2；F10 | 03 §2.2 更新 | 记录于 03 | ✅ |
| 2026-08-15 | W8-05 | 验收：138/138 全绿 + Runtime AOT（ADR-0015 例外生效） | 验收 | — | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.9 P9 观测与可靠性

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W9-01 | TraceContext（H1 落地：Activity 贯穿 + TaskId/ParentTaskId tag + 环境读取） | 实现（TDD） | 05 §5；H1 | `Trace/TraceContext.cs` | `TraceContextTests`（5） | ✅ |
| 2026-08-15 | W9-02 | RingBufferLoggerProvider（L1 环形 1000 + 类别级别覆盖 G12 + 异常结构化 L4） | 实现（TDD） | 05 §5；G11/G12；L1/L4 | `Logging/` | `RingBufferLoggerProviderTests`（3） | ✅ |
| 2026-08-15 | W9-03 | MetricsRegistry（计数器 + 直方图 p50/p95） | 实现（TDD） | 05 §5 | `Metrics/` | `MetricsRegistryTests`（2） | ✅ |
| 2026-08-15 | W9-04 | CircuitBreaker（Closed/Open/HalfOpen + 恢复窗口探测）+ 补码 ReliabilityCircuitOpen | 实现（TDD） | 05 §3 | `Reliability/CircuitBreaker.cs` | `CircuitBreakerTests`（3） | ✅ |
| 2026-08-15 | W9-05 | RetryPolicy（指数退避）+ TimeoutPolicy（超时中止 + 取消防泄漏） | 实现（TDD） | 05 §3/§4 | `Reliability/` | `RetryPolicyTests`（2）+ `TimeoutPolicyTests`（2） | ✅ |
| 2026-08-15 | W9-06 | 验收：155/155 全绿 + Runtime AOT 零告警 + 12 H1/M6 回写 | 验收 | — | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.10 P10 事件持久化

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W10-01 | StoredFact（[MessagePackObject] + SchemaVersion）+ IEventStore + ReplayQuery + RetentionPolicy 契约 | 实现（TDD） | ADR-0009 决策 1/2/3 | `Persistence/` | 测试引用 | ✅ |
| 2026-08-15 | W10-02 | InMemoryEventStore（append-only + 单调序号 + 查询过滤 + Prune） | 实现（TDD） | ADR-0009 | `Persistence/InMemoryEventStore.cs` | `InMemoryEventStoreTests`（4） | ✅ |
| 2026-08-15 | W10-03 | FileEventStore（4B 长度帧 + MessagePack + 追加串行 + FlushAsync 帧完整性 + 崩溃恢复忽略损坏尾 + 流式重放） | 实现（TDD） | ADR-0009 决策 1 默认实现 | `Persistence/FileEventStore.cs` | `FileEventStoreTests`（3） | ✅ |
| 2026-08-15 | W10-04 | EventMigrator（SchemaVersion 逐级迁移 + 环防御） | 实现（TDD） | ADR-0009 风险表 | `Persistence/EventMigrator.cs` | `EventMigratorTests`（2） | ✅ |
| 2026-08-15 | W10-05 | 验收：164/164 全绿 + Runtime AOT 零告警 | 验收 | — | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.11 P11 插件 SDK

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W11-01 | Keystone.Sdk 工程 + PluginManifest 加 Skills（默认参数） | 实现 | 10 §6；ADR-0008 决策 3 | `src/Keystone.Sdk/`、`PluginManifest.cs` | 构建绿 | ✅ |
| 2026-08-15 | W11-02 | Timers（SetTimeout/Interval/Throttle/Debounce，经 Effect 回收） | 实现（TDD） | 10 §4；N3 | `Sdk/Timers/` | `TimerExtensionsTests`（6） | ✅ |
| 2026-08-15 | W11-03 | manifest schema 校验（skills skill:// 格式 + 必填字段） | 实现（TDD） | 10 §6 | `Sdk/Manifest/ManifestSchemaValidator.cs` | `ManifestSchemaValidatorTests`（3） | ✅ |
| 2026-08-15 | W11-04 | dotnet new keystone-plugin 模板 + 全链路（创建→编译→挂载→运行→卸载） | 实现（TDD） | 10 §7；N4 | `templates/keystone-plugin/` | `TemplateTests`（1） | ✅ |
| 2026-08-15 | W11-05 | G16 防回归：10 §8.1 已接受丢弃引用表 | 文档 | G16 | `10-plugin-sdk.md` | frontmatter 校验 | ✅ |
| 2026-08-15 | W11-06 | 验收：174/174 全绿 + Sdk AOT 零告警（含 PluginLoader.DisposeAsync 幂等修正） | 验收 | — | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.12 P12 AI 组合

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W12-01 | Keystone.AI 工程 + MAF 包接入（Workflows 1.17.0 + 主包，单向依赖） | 实现 | ADR-0008 决策 1/2 | `src/Keystone.AI/` | 构建绿 | ✅ |
| 2026-08-15 | W12-02 | 单向依赖架构测试（核心 5 程序集无 Microsoft.Agents 引用） | 测试 | ADR-0008 决策 1 | `AITests.ArchitectureTests` | 5 程序集断言空 | ✅ |
| 2026-08-15 | W12-03 | WorkflowBridge（fan-out/fan-in 全等聚合 + TaskId/ParentTaskId 不稀释 = O2 验证）；TaskResultEnvelope 补 ParentTaskId | 实现（TDD） | ADR-0004；ADR-0008 决策 2 workflow 域 | `AI/Workflows/WorkflowBridge.cs` | `WorkflowBridgeTests`（3） | ✅ |
| 2026-08-15 | W12-04 | SkillRegistry（manifest skills → MAF AgentSkillsSource）+ KeystoneSkill（SEP-2640） | 实现（TDD） | ADR-0008 决策 3 | `AI/Skills/` | `SkillRegistryTests`（2） | ✅ |
| 2026-08-15 | W12-05 | 验收：184/184 全绿 + AI AOT 发布成功（MAF 组合层不参与核心 AOT 承诺） | 验收 | — | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.13 P13 验收闭环

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W13-01 | 全量回归：187/187 全绿（M0-M13 全部验收用例） | 验收 | — | — | `dotnet test` 全工程 | ✅ |
| 2026-08-15 | W13-02 | 全工程 AOT 冒烟：Core/Config/Runtime/Hosting/Sdk 零 IL 警告（Proto.Actor/MAF 例外按 ADR-0015） | 验收 | 规则 0 | — | `PublishAot=true` × 5 | ✅ |
| 2026-08-15 | W13-03 | 12/11 实现期项闭合：H2/H3/M1/M3/M6/F10/O2/G16/L 逐项回写 ✅ | 文档 | 12/11 | `12-cordis-semantics-mapping.md`、`11-gap-register.md` | 状态矩阵无 ⚠️ 残留 | ✅ |
| 2026-08-15 | W13-04 | 性能冒烟基线：插件加载/卸载循环、事件 emit 10000 条、管道调用 10000 次（宽松阈值内） | 测试 | 13 P13 验收 3 | `PerformanceSmokeTests` | 3 基线测试绿 | ✅ |
| 2026-08-15 | W13-05 | 发布文档：README/AGENTS 状态 → 实现期完成 + 1.0 可运行声明 | 文档 | — | `README.md`、`AGENTS.md` | frontmatter 校验 | ✅ |

### 7.14 P14 MCP 协议层落地（ADR-0008 决策 4 延迟项）

> 背景：决策 4 原组合 `Microsoft.Agents.AI.Mcp`，但该包**至今无稳定版**（11 个版本全 alpha，最新 1.17.0-alpha.260804.1）。经 NuGet 核实，微软官方 **MCP 协议 SDK `ModelContextProtocol` 已有稳定版 2.2.0**（net10.0 原生支持，`ModelContextProtocol.Core` 含 client/server/协议核心/传输，主包仅 ASP.NET hosting 扩展）。用户批准路径 3：协议层用稳定 SDK 落地双端，agent 集成层待 MAF 稳定后接入。**方向不变（组合官方 MCP，不自研），实现层替换**。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W14-01 | CPM 引入 `ModelContextProtocol.Core` 2.2.0（仅 Keystone.AI 引用） | 实现 | ADR-0008 决策 4；ID-12 | `Directory.Packages.props`、`Keystone.AI.csproj` | restore 绿；版本解析无冲突（M.E.AI.Abstractions 10.8.3 满足 MAF ≥10.7.0 与 MCP ≥10.8.3） | ✅ |
| 2026-08-15 | W14-02 | 契约面：McpToolDescriptor/McpToolCallResult/McpToolDefinition/McpTransportOptions/McpClientOptions/McpServerOptions/McpSessionIdentity（协议中立，零 SDK 类型） | 实现（TDD） | ADR-0008 决策 4；ID-12/13 | `src/Keystone.AI/Mcp/*.cs` | `Bridge_public_contracts_reference_no_MCP_SDK_types` | ✅ |
| 2026-08-15 | W14-03 | McpClientBridge/McpServerBridge：实现 IMcpClientBridge/IMcpServerBridge，内部 SDK 映射（含传输所有权按 SDK 源码：client 会话由 McpClient 释放、server 会话由桥释放） | 实现（TDD） | ADR-0008 决策 4；ID-12/13 | `src/Keystone.AI/Mcp/McpClientBridge.cs`、`McpServerBridge.cs` | `McpBridgeTests`（3）+ 隔离验证 | ✅ |
| 2026-08-15 | W14-04 | in-process 双端测试（契约 API）：Pipe 内存流对接 Stream 传输，无外部进程/网络 | 测试（TDD） | ADR-0008 决策 4 | `tests/Keystone.AI.Tests/McpBridgeTests.cs` | 3 用例绿（discover+call/多工具枚举/旧协议 ping） | ✅ |
| 2026-08-15 | W14-05 | 架构测试扩展：核心 5 程序集不引用 `ModelContextProtocol*`（单向依赖延伸） | 测试 | ADR-0008 决策 1/4 | `tests/Keystone.AI.Tests/AITests.cs` | 5 程序集断言空 | ✅ |
| 2026-08-15 | W14-06 | 全量回归 196/196 + Keystone.AI AOT 发布冒烟（ModelContextProtocol.Core 源生成 JSON，零 IL 警告） | 验收 | 规则 0；ADR-0008 组合包 AOT 验收门 | — | `dotnet test` + `PublishAot=true` | ✅ |
| 2026-08-15 | W14-07 | **抽象隔离评审整改**（用户质疑）：初版桥公共签名直接暴露 SDK 类型 → 重做为契约面 + 内部映射；按官方源码确认传输所有权（非反射猜测） | 重构（TDD） | ID-13 | `src/Keystone.AI/Mcp/` | 隔离验证测试 + 196/196 全绿 | ✅ |

### 7.15 P15 解耦审计（第三方依赖隔离盘点）

> 触发：用户评审 MCP 桥隔离后提出"还有多少地方直接耦合、没做隔离"。全量扫描 6 工程第三方 using + 公共签名，独立子代理交叉复核。产出 `15-decoupling-plan.md`（C1-C8 清单 + D1-D5 分阶段计划）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W15-01 | 全量耦合扫描：6 工程第三方 using + public 签名（方法参数/返回/继承）逐一判定 | 审计 | — | 全部 src 工程 | 耦合清单 C1-C8 | ✅ |
| 2026-08-15 | W15-02 | 独立子代理交叉复核（修正 C3 仅 NodeToObject public、新增 C1b CapabilityActor/C6b StoredFact/C8 Workflows 死依赖） | 审计 | — | — | 复核报告合入 | ✅ |
| 2026-08-15 | W15-03 | 解耦计划文档：`15-decoupling-plan.md`（D1 能力域接线/隔离 🔴、D3 序列化抽象 🟡、D2 配置解析收敛 🟡、D4 AI 层收敛 + 死依赖 🟢） | 文档 | ADR-0004/0008/0002 | `docs/architecture/15-decoupling-plan.md` | frontmatter 校验 + AGENTS 索引 | ✅ |

### 7.16 P16 解耦 D1：能力域接线 + Proto.Actor 隔离（C1/C1b/C2）

> 15-decoupling-plan D1（P0）：CapabilityDomain/CapabilityActor 公共面零 Proto 类型 + KeystoneHost 接线能力域（01 §2/09 §2 承诺兑现）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W16-01 | 隔离测试红：CapabilityDomain 公共签名无 Proto（Attach 豁免为显式测试缝）+ CapabilityActor 非 public；KeystoneHost 公共面无 Proto + 接线能力域 | 测试（TDD） | 15-plan D1 | `tests/Keystone.Runtime.Tests/CapabilityDomainIsolationTests.cs`、`tests/Keystone.Hosting.Tests/KeystoneHostCapabilityTests.cs` | 4 用例绿 | ✅ |
| 2026-08-15 | W16-02 | CapabilityDomain 重构：构造器私有化 + `Create`（自有 ActorSystem）/`Attach`（注入测试缝）；`Spawn` 返回 `CapabilityHandle`（封装 PID）；`RequestAsync` 收句柄；`DisposeAsync` 释放（Create 模式拥有） | 实现（TDD） | ID-14 | `src/Keystone.Runtime/Actors/CapabilityDomain.cs`、`CapabilityHandle.cs` | 隔离测试绿 | ✅ |
| 2026-08-15 | W16-03 | CapabilityActor 降 internal（Proto.IActor/IContext 内聚）；现有 CapabilityDomainTests 迁移到 Attach+CapabilityHandle | 重构（TDD） | ID-14 | `src/Keystone.Runtime/Actors/CapabilityActor.cs`、`tests/.../CapabilityDomainTests.cs` | 4 现有用例绿 | ✅ |
| 2026-08-15 | W16-04 | KeystoneHost 接线：`KeystoneHostOptions.EnableCapabilityDomain`（默认开）+ `CapabilityDomainName`；StartAsync 创建、ShutdownAsync 释放、`GetCapabilityDomain()` 访问器 | 实现（TDD） | ID-14 | `src/Keystone.Hosting/KeystoneHost.cs`、`KeystoneHostOptions.cs` | Hosting 隔离测试绿 | ✅ |
| 2026-08-15 | W16-05 | 全量回归 200/200 + Keystone.Runtime AOT 发布零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.17 P17 解耦 D3：序列化器抽象（C6）

> 15-decoupling-plan D3（P1）：兑现 ADR-0004"MessagePack 默认 / JSON 可配置"。调研确认跨域边界（Proto.Actor 同进程引用传递）无实际序列化，`[MessagePackObject]` 是契约声明；**唯一执行序列化的消费点是 FileEventStore（事件持久化）** → 抽象应用到该通道。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W17-01 | `IContractSerializer` 接口（Serialize/Deserialize，泛型 + 源生成 AOT 安全）+ `MessagePackContractSerializer`（默认）+ `JsonContractSerializer`（STJ 源生成上下文注入，调试/审计） | 实现（TDD） | ADR-0004；ID-15 | `src/Keystone.Core/Serialization/` | `ContractSerializerTests`（4） | ✅ |
| 2026-08-15 | W17-02 | FileEventStore 走抽象：构造器可选注入 `IContractSerializer`（默认 MessagePack，兼容现有调用）；4 处序列化点替换 | 重构（TDD） | ADR-0004/0009；ID-15 | `src/Keystone.Runtime/Persistence/FileEventStore.cs` | 现有 FileEventStoreTests 全绿 + 新增 JSON 注入往返 | ✅ |
| 2026-08-15 | W17-03 | 全量回归 205/205 + Core/Runtime AOT 发布零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

## 8. 回溯索引（三向映射）

> 目的：三条路径都能走通——**决策→代码**（改设计时查影响）、**代码→决策**（看代码时查依据）、**工作→文档**（回溯时查上下文）。
> 维护：随工作日志同步更新（工作项落定时补一行），不要求穷举，覆盖所有 W/ID/DEV 编号即可。

| 工作项 | 决策/文档依据 | 代码落点 | 验收凭证 |
|--------|-------------|---------|---------|
| W0-01 | 13 P0 | `cordis-csharp.slnx`、`src/Keystone.*/` | `dotnet build` 绿 |
| W0-02 | 13 P0；规则 0 | `.editorconfig`、`Directory.Build.props` | 分析器全绿 |
| W0-03 | 13 P0 | `Directory.Packages.props`、`nuget.config` | restore 绿 |
| W0-04 | ADR-0013；08 §2 | `src/Keystone.Config/**` | 15 单测绿 + AOT 发布零告警 |
| W0-05 | ID-02 | 全仓命名 | grep 自称残留 0 |
| W0-06 | 规则 0 | — | `PublishAot=true` 发布成功 |
| W0-07 | ADR-0014；ID-02 | `KeystoneConfigBuilder.CreateDefault` | 15 单测绿 |
| W1-01 | ID-04；06 §3/§4 | `Keystone.Core/Contracts/TaskId.cs` | `TaskIdTests`（6） |
| W1-02 | 06 §1；ADR-0004 | `Keystone.Core/Contracts/TaskRequest.cs` 等 | `TaskRequestTests`（5） |
| W1-03 | ID-03；06 §6 | `Keystone.Core/Contracts/TaskEnvelope.cs` 等 | `TaskEnvelopeTests`（5） |
| W1-04 | ID-05；12 §8 M6 | `Keystone.Core/Errors/ErrorCode.cs` | `ErrorCodeTests`+`KeystoneExceptionTests`（7） |
| W2-02 | ADR-0006；ID-06 | `Keystone.Runtime/Events/EventBus.cs` | `EventBusModeTests`+`EventBusOptionsTests`（11） |
| W2-03 | 03 §5；ID-08 | `Keystone.Runtime/Context/ContextFacade.cs` | `ContextFacadeTests` 过滤（3） |
| W2-04 | 03 §2.1/§2.3 | `Keystone.Runtime/Context/ServiceStore.cs` | `ServiceStoreTests`（5） |
| W2-05 | 12 §8 M1；ID-07 | `Keystone.Runtime/Effects/EffectRegistry.cs` | `EffectRegistryTests`（4） |
| W2-06 | 12 §7.3 | `Keystone.Runtime/Context/IContextInterceptor.cs` | `ContextFacadeTests.Interceptor_*` |
| W3-01 | ADR-0007；ID-10 | `Keystone.Runtime/Plugins/Manifest/` | `ManifestValidatorTests`（4） |
| W3-02 | ADR-0007 | `Keystone.Runtime/Plugins/Services/` | `ServiceRegistryTests`（3） |
| W3-03 | ADR-0005/0007；ID-09 | `Keystone.Runtime/Plugins/Lifecycle/PluginRuntime.cs` | `PluginRuntimeTests`（6） |
| W4-01 | 04 §2/§4；H2 | `Keystone.Runtime/Pipeline/PipelineBuilder.cs` | `PipelineTests`（6） |
| W4-02 | 04 §3；ADR-0006 | `Pipeline/` + `Events/` | `PipelineRoutingTests`（2） |
| W5-01 | 02 §4；ADR-0002 | `Keystone.Runtime/Plugins/Loading/RoslynCompiler.cs` | `RoslynCompilerTests`（3） |
| W5-03 | 02 §7；ADR-0005 | `Keystone.Runtime/Plugins/Loading/PluginLoader.cs` | `PluginLoaderTests`（4） |
| ID-09 | ADR-0007 决策 3 | `Plugins/Lifecycle/PluginRuntime.cs` | W3-03 |
| ID-10 | ADR-0007 决策 2 | `Plugins/Manifest/ManifestValidator.cs` | W3-01 |
| ID-06 | ADR-0006 | `Events/IEventBus.cs` | W2-02 |
| ID-07 | 12 §8 M1 | `Effects/EffectRegistry.cs` | W2-05 |
| ID-08 | 03 §5 | `Context/ContextFacade.cs` | W2-03 |
| ID-03 | 规则 0 第 3 条 | `Contracts/TaskEnvelope.cs` | W1-03 |
| ID-04 | 06 §1 | `Contracts/TaskId.cs` | W1-01 |
| ID-05 | 12 §8 M6 | `Errors/ErrorCode.cs` | W1-04 |
| ID-02 | 用户决策 | 全仓命名空间/包名 | W0-05 |
| W14-01 | ADR-0008 决策 4；ID-12 | `Directory.Packages.props`、`Keystone.AI.csproj` | restore 绿 |
| W14-02 | ID-12/13 | `AI/Mcp/`（契约 DTO/options） | `Bridge_public_contracts_reference_no_MCP_SDK_types` |
| W14-03 | ID-12/13 | `AI/Mcp/McpClientBridge.cs`、`McpServerBridge.cs` | `McpBridgeTests`（3）+ 隔离验证 |
| W14-04 | ADR-0008 决策 4 | `tests/Keystone.AI.Tests/McpBridgeTests.cs` | 3 用例绿 |
| W14-05 | ADR-0008 决策 1/4 | `tests/Keystone.AI.Tests/AITests.cs` | 5 程序集断言空 |
| ID-12 | ADR-0008 决策 4 | `AI/Mcp/`（双端桥） | W14-01~06 |
| ID-13 | 用户评审 | `AI/Mcp/`（契约隔离） | W14-07 |
| W15-01~03 | 用户评审 | `15-decoupling-plan.md`（C1-C8/D1-D5） | 审计闭环 |
| W16-01 | 15-plan D1 | `CapabilityDomainIsolationTests`、`KeystoneHostCapabilityTests` | 4 用例绿 |
| W16-02 | ID-14 | `Actors/CapabilityDomain.cs`、`CapabilityHandle.cs` | 隔离测试绿 |
| W16-03 | ID-14 | `Actors/CapabilityActor.cs` | 4 现有用例绿 |
| W16-04 | ID-14 | `Keystone.Hosting/KeystoneHost.cs`、`KeystoneHostOptions.cs` | Hosting 隔离测试绿 |
| ID-14 | 15-plan D1 | `Actors/`、`Hosting/` | W16-01~05 |
| W17-01 | ID-15 | `Core/Serialization/`（接口 + 2 实现） | `ContractSerializerTests`（4） |
| W17-02 | ID-15 | `Runtime/Persistence/FileEventStore.cs` | JSON 注入往返测试 |
| ID-15 | ADR-0004 | `Core/Serialization/`、`Runtime/Persistence/` | W17-01~03 |

## 9. 维护规则

- **联动 R10**：14 是文档治理的一部分——阶段事件/决策/偏差的更新与 13、AGENTS.md 状态同步（P0 落地时 AGENTS.md "设计期"→"实现期"）
- **只追加不改写**：历史行不改；更正新增行引用旧编号（§1）
- **阶段退出检查**：14 §2 状态 + §6 验收台账 + §3 分节记录三者同时闭合才算记录闭合（13 §4 DoD）
- **回溯约定**：实现期任何"当时为什么这么做"的疑问 → 先查 §4（决策）→ §5（偏差）→ §3（工作项）→ 三向索引（§8）定位代码
