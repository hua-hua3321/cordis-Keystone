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
| P18 解耦 D2 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 配置解析面收敛（§7.18：C3 闭合，EntryParser 零 YamlDotNet 泄漏，206/206 全绿） | §7.18 |
| P19 解耦 D4 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | AI 层边界 + Workflows 死依赖清理（§7.19：C8 闭合，C4 记录保持，206/206 全绿） | §7.19 |
| P20 解耦 D5 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 回归闭环（§7.20：206/206 + 六工程 AOT 零 IL 警告 + 15-plan 全部完成） | §7.20 |
| P21 集成验收 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 端到端集成测试（§7.21：真实插件组全链跑通 + B5 跨插件服务解析修复，207/207 全绿） | §7.21 |
| P22 接入 B3/B4 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 管道接入能力域 + 宿主事件面（§7.22：actor 持管道兑现 + Events 公开，209/209 全绿） | §7.22 |
| P23 Cordis 差距复核 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 实现后差距复核（§7.23：G-C1~C14 清单 + 16-cordis-gap-review 文档） | §7.23 |
| P24 差距 G-C1 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 插件配置注入（§7.24：EntryOptions.Config → schema 校验 → 默认值 → InitializeAsync，212/212 全绿） | §7.24 |
| P25 差距 G-C2 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 依赖恢复 re-arm（§7.25：依赖重现自动重启，213/213 全绿） | §7.25 |
| P26 差距 G-C3 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 服务值卸载注销（§7.26：运行期 Provide 值卸载后注销，215/215 全绿） | §7.26 |
| P27 差距 G-C4 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 事件 false 短路语义（§7.27：serial/bail 对齐 isBailed，218/218 全绿） | §7.27 |
| P28 差距 G-C6 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | waterfall terminal 注入（§7.28：发布者注入内置行为 + 返回值，221/221 全绿） | §7.28 |
| P29 差距 G-C5 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | M4 方法级延迟注入（§7.29：GetLazy 首次访问解析，224/224 全绿） | §7.29 |
| P30 差距 G-C7 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 日志导出器抽象（§7.30：ILogSink + Console sink，228/228 全绿） | §7.30 |
| P31 差距 G-C8 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 热更新 API（§7.31：ReloadPlugin/UpdatePlugin，231/231 全绿） | §7.31 |
| P32 差距 G-C11 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 日志级别默认阈值（§7.32：三级过滤对齐 Cordis levels，236/236 全绿） | §7.32 |
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
| ID-16 | 2026-08-15 | P18 | 配置解析面收敛（15-plan D2）：`EntryParser.NodeToObject` 降 private，YamlDotNet 类型退出公共面 | 无外部调用（仅内部递归）；Parse(string) 返回纯框架类型足够；隔离测试锁定 | `src/Keystone.Config/Entries/EntryParser.cs` | 否（15-plan D2） |
| ID-17 | 2026-08-15 | P19 | AI 组合层边界（15-plan D4）：①移除 `Microsoft.Agents.AI.Workflows` 死依赖（WorkflowBridge 纯 Task，MAF 图构建未接线前不引）；②`SkillRegistry` 返回 MAF 类型保持（唯一消费方=AI 层内部，组合层预期） | 死依赖违反单向组合克制；C4 按"仅组合层内部消费→保持"标准（与 Mcp 桥不同：Mcp 是框架通用能力需隔离，skills 是 AI 专属） | `Keystone.AI.csproj`、`Directory.Packages.props` | 否（15-plan D4） |
| ID-18 | 2026-08-15 | P21 | 跨插件服务解析修复（集成验收 W21-02）：ContextFacade.Provide 有父时写公共祖先（root）——03 §2.1 组合语义（子不覆盖父、首次注册公共区）；Get/TryGet 沿父链向上（自己 → 父 → 根） | 集成测试暴露 DEV-02（03 §2"先自己再父"未实现）；兄弟插件共享 root 经父链可达；隔离实例（独立 root）天然隔离（03 §2.2 不变） | `src/Keystone.Runtime/Context/ContextFacade.cs` | 否（14 §5 DEV-02） |
| ID-19 | 2026-08-15 | P22 | 能力域管道接入（B3）：CapabilityActor 内建中间件管道——handler 作 terminal，插件中间件（IMiddleware）before/after 包裹（01 §2"actor 持管道"兑现）；短路 = KS:PIPELINE:MIDDLEWARE_REJECTED 失败结果（非抛异常） | 01 §2 架构承诺；04 §2 形状 A 公开面；waterfall 否决语义（ADR-0006）；请求级独立 ContextFacade（实例隔离） | `src/Keystone.Runtime/Actors/CapabilityActor.cs`、`CapabilityDomain.cs` | 否（01/04 文档备注） |
| ID-20 | 2026-08-15 | P24 | 插件配置注入（G-C1）：Host 经 `ConfigSchemaProvider`（条目→schema）+ `ConfigResolver`（校验+默认值）解析 entry.Config 传入 `InitializeAsync`；无 schema 直传；校验失败 = 该插件 FAILED（09 §2 隔离，不整域回滚） | 兑现 Cordis resolveConfig（fiber.ts:641）+ 10-plugin-sdk §2"apply 收完整配置"；ConfigSchema/Resolver 从零调用接入宿主 | `KeystoneHostOptions`、`KeystoneHost`、`PluginLoader`、`PluginRuntime` | 否（16-cordis-gap-review G-C1 备注） |
| ID-21 | 2026-08-15 | P25 | 依赖恢复 re-arm（G-C2）：依赖重现（Available=true）→ Disposed/Unloading 依赖方自动 StartAsync；订阅生命周期区分——自动卸载（依赖消失）保留订阅待 re-arm，显式 StopAsync/热重载销毁订阅（终态，防 ALC 泄漏） | 兑现 Cordis epoch 驱动（fiber.ts:625-639）+ ADR-0007 决策 3 的对称性（重现→重启）；热重载测试暴露旧 ALC 被订阅持有 → 显式停止销毁 | `src/Keystone.Runtime/Plugins/Lifecycle/PluginRuntime.cs` | 否（16-cordis-gap-review G-C2 备注） |
| ID-22 | 2026-08-15 | P26 | 服务值卸载注销（G-C3）：`IServiceStore.Remove`（属主校验）+ ContextFacade 属主追踪（_ownedServices）+ PluginRuntime 卸载钩子 RemoveOwnedServices——运行期 Provide 值在插件卸载后从 root/本地 store 注销 | 兑现 Cordis provide disposer（reflect.ts）；防依赖方拿陈旧值；manifest 名由 registry.Unregister 处理、运行期名由 store.Remove 处理（双轨补齐） | `IServiceStore`、`ServiceStore`、`ContextFacade`、`PluginRuntime` | 否（16-cordis-gap-review G-C3 备注） |
| ID-23 | 2026-08-15 | P27 | 事件决策短路语义（G-C4）：`IsBailed` 对齐 Cordis isBailed——serial/bail 中 null/false 不算决策值（不短路），0/空串等其余值短路 | events.ts:13-15 语义精确对齐；避免返回 false 的监听器提前截断链 | `src/Keystone.Runtime/Events/EventBus.cs` | 否（16-cordis-gap-review G-C4 备注） |
| ID-24 | 2026-08-15 | P28 | waterfall 发布者注入 terminal（G-C6）：`PublishWaterfallAsync` 增 `Func<Task<object?>>? terminal`（最内层 next，可被否决）+ 返回值；监听器不调 next → 否决（terminal 未执行，null） | Cordis waterfall 返回值语义（events.ts:234-243）："发布者注入内置行为，可被否决"的核心用法 | `src/Keystone.Runtime/Events/EventBus.cs`、`IEventBus.cs` | 否（16-cordis-gap-review G-C6 备注） |
| ID-25 | 2026-08-15 | P29 | M4 方法级延迟注入（G-C5）：`IPluginContext.GetLazy<T>` 返回 `Lazy<Task<T>>`——首次访问 .Value 才解析（服务不可用抛 GatingServiceNotFound）；Lazy 缓存（只解析一次） | 兑现 12 文档 M4 声称的 Lazy 对应物；对齐 Cordis @Inject 方法级（registry.ts:45-59）：初始化声明、方法执行时解析 | `src/Keystone.Runtime/Context/IPluginContext.cs`、`ContextFacade.cs` | 否（16-cordis-gap-review G-C5 备注） |
| ID-26 | 2026-08-15 | P30 | 日志导出器抽象（G-C7）：`ILogSink`（Write(LogRecord)，对齐 Cordis Exporter）+ `ConsoleLogSink`（结构化行 + 可选 ANSI 配色）；RingBufferLoggerProvider sinks 注入 + 分发 | 兑现 05 §5 "Console（默认）+ 可选 File/exporter"承诺；日志从内存快照变为可输出 | `src/Keystone.Runtime/Logging/`（ILogSink/ConsoleLogSink/RingBufferLoggerProvider） | 否（16-cordis-gap-review G-C7 备注） |
| ID-27 | 2026-08-15 | P31 | 热更新 API（G-C8）：`ReloadPluginAsync`（冷重启：重编译 + 新 ALC）+ `UpdatePluginAsync`（热更新：config 变 → PatchContext 瀑布可否决 → 重载）；FileSystemWatcher 由嵌入方经 ConfigUpdate 事件接线 | 兑现 09 §5 ReloadPlugin/UpdatePlugin + 08 §6.1 变更分级；宿主用 YAML 字符串启动无文件源，watcher 不内置 | `src/Keystone.Hosting/KeystoneHost.cs` | 否（16-cordis-gap-review G-C8 备注） |
| ID-28 | 2026-08-15 | P32 | 日志级别默认阈值（G-C11）：RingBufferLoggerProvider 三级过滤——按 category 覆盖 → defaultLevel → 全局默认 Information；IsEnabled 无 override 不再恒 true | 对齐 Cordis levels[name] ?? levels.default ?? INFO（logger.ts:155）；Debug 日志默认被过滤（真实语义缺陷修复） | `src/Keystone.Runtime/Logging/RingBufferLoggerProvider.cs` | 否（16-cordis-gap-review G-C11 备注） |

**格式**：决定（一句话，可执行）→ 理由（1-2 条）→ 影响面（哪些模块受影响）→ 升级标记。

## 5. 偏差记录（设计与实现不一致）

> 实现期发现 00-12 文档与实际不符/不可行时**必须**记在此处，禁止静默偏离。

| 编号 | 日期 | 阶段 | 偏差描述 | 原因 | 处置 | 关联 |
|------|------|------|---------|------|------|------|
| DEV-01 | 2026-08-15 | P2 | 属主校验在插件 A 提供服务、插件 B 也声明 provides 同名的场景与 02 §1 描述冲突 | 02 §1 未定义同名 provides 冲突优先级 | 处置中：按 rebind 同 scope 重复=错误（03 §2.1）→ 需补充文档说明，待 P3 定 | 03 §2.1；02 §1 |
| DEV-02 | 2026-08-15 | P21 | 03 §2 承诺"服务解析链：scope 内先查自己，再查父 scope"，但实现 ServiceStore 每 context 独立、Get 只查本地——跨插件 inject 服务不可解析（集成测试 W21 暴露） | 实现期 P2 遗漏父链解析；Provide 写本地导致兄弟插件互不可见 | ①改实现适配文档：ContextFacade.Provide 有父时写公共祖先（root，03 §2.1 组合语义）+ Get/TryGet 沿父链向上（W21-02，ID-18）；隔离实例（独立 root）天然隔离不变 | 03 §2/§2.2；ADR-0007 |

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

### 7.18 P18 解耦 D2：配置解析面收敛（C3）

> 15-decoupling-plan D2（P2）：EntryParser 公共面不再暴露 YamlDotNet 类型。`NodeToObject` 唯一 public 泄漏（Get/Scalar/Bool/StringList 已 private），无外部调用（仅内部递归）→ 降 private。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W18-01 | `EntryParser.NodeToObject` 降 private（YamlDotNet 类型退出公共面）；隔离测试：EntryParser 公共静态签名无 YamlDotNet 泄漏 | 重构（TDD） | 15-plan D2 | `src/Keystone.Config/Entries/EntryParser.cs`、`tests/Keystone.Config.Tests/EntryParserIsolationTests.cs` | 隔离测试绿 + 现有 EntryParserTests 全绿 | ✅ |
| 2026-08-15 | W18-02 | 全量回归 206/206 | 验收 | — | — | `dotnet test` | ✅ |

### 7.19 P19 解耦 D4：AI 组合层边界 + Workflows 死依赖清理（C4/C8）

> 15-decoupling-plan D4（P2）：① C8 死依赖——`Microsoft.Agents.AI.Workflows` 引用零使用（WorkflowBridge 纯 Task 实现）→ 移除引用（计划倾向①）；② C4——`SkillRegistry.FromManifest` 返回 MAF `AgentSkillsSource`，经核实唯一调用方是 AI.Tests（AI 组合层内部消费）→ 保持（组合层预期，ADR-0008 决策 3）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W19-01 | 移除 `Microsoft.Agents.AI.Workflows` 死依赖（csproj + CPM）；验证 assets 无 Workflows 包 | 重构 | 15-plan D4/C8；ID-17 | `src/Keystone.AI/Keystone.AI.csproj`、`Directory.Packages.props` | restore 绿 + assets 断言无 Workflows | ✅ |
| 2026-08-15 | W19-02 | C4 边界确认：`SkillRegistry.FromManifest` 消费方仅 AI.Tests（组合层内部）→ 保持返回 MAF 类型，文档声明边界 | 评估 | ADR-0008 决策 3；15-plan D4 | `src/Keystone.AI/Skills/SkillRegistry.cs` | 无改动（记录保持） | ✅ |
| 2026-08-15 | W19-03 | 全量回归 206/206 + Keystone.AI AOT 发布零 IL 警告（移除 Workflows 后） | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.20 P20 解耦 D5：回归闭环

> 15-decoupling-plan D5：解耦全部执行后的最终回归 + 状态更新。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W20-01 | 全量回归（重构建）：206/206 全绿 | 验收 | — | — | `dotnet test`（全量重构建） | ✅ |
| 2026-08-15 | W20-02 | 六工程 AOT 发布冒烟：Core/Config/Runtime/Hosting/Sdk/AI 零 IL 警告 | 验收 | 规则 0 | — | `PublishAot=true` × 6 | ✅ |
| 2026-08-15 | W20-03 | 15-decoupling-plan 状态 → 全部完成（C1/C1b/C2/C3/C6/C6b/C8 闭合；C4/C5/C7 记录保持）+ AGENTS.md 同步 | 文档 | — | `15-decoupling-plan.md`、`AGENTS.md` | frontmatter 校验 | ✅ |

### 7.21 P21 集成验收：端到端真实功能测试

> 用户要求"接起来跑通整体"。审查发现各能力域独立（断点 B1-B5），设计端到端集成测试用真实插件组走全链。过程中发现并修复 2 个真实断点。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W21-01 | 集成审查：确认断点——B1 能力域空转（Spawn 无人调用）/B2 插件 handler 无桥/B3 管道未接宿主/B4 宿主未公开事件面/B5 跨插件服务解析缺父链（03 §2 设计未实现） | 审计 | 01/03/06/09 | — | 断点清单 | ✅ |
| 2026-08-15 | W21-02 | **修复 B5（真实缺陷）**：ContextFacade.Provide 有父时写公共祖先（root，03 §2.1 组合语义）+ Get/TryGet 沿父链向上解析——插件兄弟经 root 共享服务，隔离实例（独立 root）天然隔离 | 修复（TDD） | 03 §2/§2.2；ADR-0007 | `src/Keystone.Runtime/Context/ContextFacade.cs` | 现有 89 Runtime 测试全绿 + 新集成验证 | ✅ |
| 2026-08-15 | W21-03 | 端到端集成测试：calculator（真实计算服务）/telemetry（inject 门控注入）/audit（事件观察）三插件，全链验证——配置 YAML → 宿主启动 → Roslyn 编译加载 → 依赖门控 → 服务注入 → 能力域跨域调用（20+22=42，TaskId 贯穿）→ 事件观察（audit 收到）→ 优雅关闭（幂等） | 测试（集成） | — | `tests/Keystone.Hosting.Tests/EndToEndIntegrationTests.cs` | 1 集成用例绿 | ✅ |
| 2026-08-15 | W21-04 | 全量回归 207/207 + Keystone.Runtime AOT 发布零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

> **B4 记录**：宿主未公开事件总线（测试经反射访问 root context）——事件面暴露待后续（宿主 API 完善阶段）。**P22 已闭合**（见 §7.22 W22-03）。

### 7.22 P22 接入 B3/B4：管道入能力域 + 宿主事件面

> 用户"继续接入"：P21 遗留断点 B3（管道未接宿主/能力域，01 §2"actor 持管道"未兑现）与 B4（宿主未公开事件面）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W22-01 | CapabilityActor 管道化（B3）：actor 内建 PipelineBuilder——中间件链包裹 handler（terminal）；短路 = KS:PIPELINE:MIDDLEWARE_REJECTED 失败结果（waterfall 否决，ADR-0006）；请求级 ContextFacade 供中间件取服务/日志 | 实现（TDD） | 01 §2；04 §2/§4；ADR-0006 | `src/Keystone.Runtime/Actors/CapabilityActor.cs` | `CapabilityDomainPipelineTests`（2） | ✅ |
| 2026-08-15 | W22-02 | `CapabilityDomain.Spawn` 增 `IReadOnlyList<IMiddleware>` 参数（插件中间件链，01 §2 兑现） | 实现（TDD） | 01 §2 | `src/Keystone.Runtime/Actors/CapabilityDomain.cs` | 管道测试绿 | ✅ |
| 2026-08-15 | W22-03 | B4 宿主事件面：`KeystoneHost.Events`（root context 共享总线，StartAsync 后可用） | 实现 | ID-08；09 §5 | `src/Keystone.Hosting/KeystoneHost.cs` | 端到端测试用 host.Events（无反射） | ✅ |
| 2026-08-15 | W22-04 | 端到端升级：能力域调用经中间件管道（req-audit/req-metrics before/after 顺序断言）+ 事件经 host.Events | 测试（集成） | 01 §2；ID-08 | `tests/Keystone.Hosting.Tests/EndToEndIntegrationTests.cs` | 1 集成用例绿（管道 + 事件） | ✅ |
| 2026-08-15 | W22-05 | 全量回归 209/209 + Runtime/Hosting AOT 发布零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` × 2 | ✅ |

### 7.23 P23 Cordis 功能差距复核（实现后）

> 用户要求"参考 Cordis 代码对比差距"。4 独立子代理并行审计（Events/Registry+Service/Reflect+Context/Logger+Utils）+ 主代理独立验证。产出 `16-cordis-gap-review.md`（G-C1~C14）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W23-01 | 4 子代理并行审计 Cordis 4 模块面 vs Keystone 实现（Events/Registry/Reflect/Logger） | 审计 | 07/12 | — | 4 份差距表 | ✅ |
| 2026-08-15 | W23-02 | 主代理独立验证关键点：config 注入缺口（G-C1，ConfigResolver 零调用）/依赖 re-arm（G-C2）/服务值注销（G-C3）/M4 延迟注入（G-C5）/事件 false 语义（G-C4） | 审计 | 07/12 | — | 逐点代码证据 | ✅ |
| 2026-08-15 | W23-03 | 差距文档 `16-cordis-gap-review.md`：3 高 + 5 中 + 6 低（G-C1~C14）+ 根因 + 建议计划 | 文档 | — | `docs/architecture/16-cordis-gap-review.md` | frontmatter 校验 + AGENTS 索引 | ✅ |

### 7.24 P24 差距 G-C1：插件配置注入

> 16-cordis-gap-review G-C1（🔴 高危）：插件 InitializeAsync 原收空字典，`EntryOptions.Config` 未传递、ConfigSchema/ConfigResolver 零调用。本次接线：schema 校验 + 默认值补齐 + 失败隔离。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W24-01 | KeystoneHostOptions 增 `ConfigSchemaProvider`（条目→schema，null=无 schema 直传）+ `ConfigFilters`（M3 过滤器链） | 实现（TDD） | G-C1；08 §5 | `src/Keystone.Hosting/KeystoneHostOptions.cs` | 构建绿 | ✅ |
| 2026-08-15 | W24-02 | PluginRuntime/PluginLoader 构造增 config 参数（默认空字典兼容）；LoadSourceAsync 传递 | 实现（TDD） | G-C1 | `src/Keystone.Runtime/Plugins/Lifecycle/PluginRuntime.cs`、`Loading/PluginLoader.cs` | 现有测试全绿 | ✅ |
| 2026-08-15 | W24-03 | KeystoneHost.ResolvePluginConfigAsync：entry.Config → ConfigResolver（校验+默认值）；校验失败 → 该插件 FAILED（09 §2 隔离语义，_failedEntries 记录）不阻断其他 | 实现（TDD） | G-C1；09 §2 | `src/Keystone.Hosting/KeystoneHost.cs` | 3 用例绿（默认值/失败隔离/无 schema 直传） | ✅ |
| 2026-08-15 | W24-04 | 全量回归 212/212 + Hosting/Runtime AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` × 2 | ✅ |

### 7.25 P25 差距 G-C2：依赖恢复 re-arm

> 16-cordis-gap-review G-C2（🔴 高危）：依赖消失 → 卸载（已实现），依赖重现 → 自动重启（缺失）。本次补全 re-arm + 订阅生命周期区分。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W25-01 | 依赖订阅加重现分支（Available=true 且 Disposed/Unloading → StartAsync 自动重启） | 实现（TDD） | G-C2；ADR-0007 决策 3 | `src/Keystone.Runtime/Plugins/Lifecycle/PluginRuntime.cs` | `DependencyReArmTests`（1） | ✅ |
| 2026-08-15 | W25-02 | 订阅生命周期区分：StopCoreAsync（依赖消失自动卸载）保留订阅待 re-arm；StopAsync（显式停止/热重载）销毁订阅（终态）——修复热重载旧 ALC 不可回收回归 | 修复（TDD） | G-C2 | 同上 | 热重载测试恢复绿 | ✅ |
| 2026-08-15 | W25-03 | StartCoreAsync 状态检查接受 Disposed（恢复路径）；启动时重置 error/settled | 实现（TDD） | G-C2 | 同上 | re-arm 测试绿 | ✅ |
| 2026-08-15 | W25-04 | 全量回归 213/213 + Runtime AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.26 P26 差距 G-C3：服务值卸载注销

> 16-cordis-gap-review G-C3（🔴 高危）：插件运行期 Provide 的服务值在卸载后滞留 root store，依赖方拿陈旧值。本次补 `IServiceStore.Remove` + 属主追踪 + 卸载注销。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W26-01 | `IServiceStore.Remove(serviceName, ownerId)` + ServiceStore 实现（属主校验移除） | 实现（TDD） | G-C3 | `src/Keystone.Runtime/Context/IServiceStore.cs`、`ServiceStore.cs` | 构建绿 | ✅ |
| 2026-08-15 | W26-02 | ContextFacade 属主追踪（_ownedServices）+ `RemoveOwnedServices`（root/本地 store 属主注销） | 实现（TDD） | G-C3 | `src/Keystone.Runtime/Context/ContextFacade.cs` | 测试绿 | ✅ |
| 2026-08-15 | W26-03 | PluginRuntime.StopCoreAsync 卸载钩子：插件 dispose 后调用 RemoveOwnedServices（运行期值注销） | 实现（TDD） | G-C3 | `src/Keystone.Runtime/Plugins/Lifecycle/PluginRuntime.cs` | `ServiceValueUnloadTests`（2） | ✅ |
| 2026-08-15 | W26-04 | 全量回归 215/215 + Runtime AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.27 P27 差距 G-C4：事件 false 短路语义

> 16-cordis-gap-review G-C4（🟡 中危）：serial/bail 的 `false` 短路语义偏差——Cordis `isBailed` 排除 false/null，Keystone `result is not null` 把 false 当短路。本次对齐。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W27-01 | `EventBus.IsBailed`（对齐 Cordis isBailed：null/false 不短路）+ PublishSerialAsync/PublishBail 改用 | 实现（TDD） | G-C4；events.ts:13-15 | `src/Keystone.Runtime/Events/EventBus.cs` | `BailSemanticsTests`（3） | ✅ |
| 2026-08-15 | W27-02 | 全量回归 218/218 + Runtime AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.28 P28 差距 G-C6：waterfall 发布者注入 terminal

> 16-cordis-gap-review G-C6（🟡 中危）：waterfall 发布者无法注入内置行为（terminal 硬编码空操作）。本次支持 terminal 注入 + 返回值（Cordis waterfall 返回值语义）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W28-01 | `PublishWaterfallAsync` 增 `Func<Task<object?>>? terminal`（发布者注入最内层 next）+ 返回 terminal 结果；监听器不调 next → 否决（terminal 未执行，返回 null） | 实现（TDD） | G-C6；events.ts:234-243 | `src/Keystone.Runtime/Events/EventBus.cs`、`IEventBus.cs` | `WaterfallTerminalTests`（3） | ✅ |
| 2026-08-15 | W28-02 | 全量回归 221/221 + Runtime AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.29 P29 差距 G-C5：M4 方法级延迟注入

> 16-cordis-gap-review G-C5（🟡 中危）：12 文档声称 `Lazy<Task<T>>` 对应物但实现缺失。本次落地 `IPluginContext.GetLazy<T>`——首次访问才解析（对齐 Cordis @Inject 方法级）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W29-01 | `IPluginContext.GetLazy<T>`（返回 Lazy\<Task\<T\>\>，首次访问 .Value 才解析）+ ContextFacade 实现 | 实现（TDD） | G-C5/M4；registry.ts:45-59 | `src/Keystone.Runtime/Context/IPluginContext.cs`、`ContextFacade.cs` | `LazyInjectionTests`（3） | ✅ |
| 2026-08-15 | W29-02 | 全量回归 224/224 + Runtime AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.30 P30 差距 G-C7：日志导出器抽象

> 16-cordis-gap-review G-C7（🟡 中危）：无 Exporter 抽象（仅内存 RingBuffer），日志不可见。本次落 `ILogSink` + `ConsoleLogSink`（兑现 05 §5 "Console（默认）"承诺，对齐 Cordis Exporter）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W30-01 | `ILogSink`（Write(LogRecord)，对齐 Cordis Exporter.export）+ `ConsoleLogSink`（结构化行 + 可选 ANSI 级别配色） | 实现（TDD） | G-C7；05 §5；logger.ts:41-47 | `src/Keystone.Runtime/Logging/ILogSink.cs`、`ConsoleLogSink.cs` | `LogSinkTests`（4） | ✅ |
| 2026-08-15 | W30-02 | RingBufferLoggerProvider 增 sinks 注入 + Write 分发到全部 sink（缓冲快照兼容） | 实现（TDD） | G-C7 | `src/Keystone.Runtime/Logging/RingBufferLoggerProvider.cs` | 现有快照测试全绿 | ✅ |
| 2026-08-15 | W30-03 | 全量回归 228/228 + Runtime AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.31 P31 差距 G-C8：热更新 API

> 16-cordis-gap-review G-C8（🟡 中危）：09 §5 承诺 `ReloadPlugin`/`UpdatePlugin` 未实现。本次落 `ReloadPluginAsync`（冷重启）+ `UpdatePluginAsync`（热更新，瀑布可否决）。FileSystemWatcher 由嵌入方接线（宿主用 YAML 字符串启动无文件源，ConfigUpdate 事件可订阅）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W31-01 | `ReloadPluginAsync(id)`：重编译源码 + 新 loader（新 ALC）→ 旧 quiesce + Unload（08 §6.1 冷重启分级） | 实现（TDD） | G-C8；09 §5；08 §6.1 | `src/Keystone.Hosting/KeystoneHost.cs` | `HotReloadTests.ReloadPlugin_restarts` | ✅ |
| 2026-08-15 | W31-02 | `UpdatePluginAsync(id, config)`：更新条目 config → PatchContext 瀑布（可否决）→ 重载（08 §6.1 热更新分级 + ADR-0005） | 实现（TDD） | G-C8；ADR-0005 决策 3 | 同上 | `HotReloadTests.UpdatePlugin_*`（2） | ✅ |
| 2026-08-15 | W31-03 | 全量回归 231/231 + Hosting AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.32 P32 差距 G-C11：日志级别默认阈值

> 16-cordis-gap-review G-C11（🟢 低危但真实语义缺陷）：IsEnabled 无 override 恒 true（Debug 也输出）。本次三级过滤——按 category 覆盖 → defaultLevel → 全局默认 Information（对齐 Cordis levels[name] ?? levels.default ?? INFO）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W32-01 | RingBufferLoggerProvider 三级级别过滤：`defaultLevel` 参数 + IsEnabled 判定（override → defaultLevel → Information） | 实现（TDD） | G-C11；logger.ts:155 | `src/Keystone.Runtime/Logging/RingBufferLoggerProvider.cs` | `LogLevelThresholdTests`（5） | ✅ |
| 2026-08-15 | W32-02 | 全量回归 236/236 + Runtime AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

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
| W18-01 | ID-16 | `Config/Entries/EntryParser.cs` | `EntryParserIsolationTests` |
| ID-16 | 15-plan D2 | `Config/Entries/EntryParser.cs` | W18-01~02 |
| W19-01 | ID-17 | `Keystone.AI.csproj`、`Directory.Packages.props` | assets 断言无 Workflows |
| ID-17 | 15-plan D4 | `Keystone.AI/`（csproj/CPM） | W19-01~03 |
| W20-01~03 | 15-plan D5 | — | 206/206 + AOT × 6 + 文档闭合 |
| W21-01 | 01/03/06/09 | — | 断点清单 B1-B5 |
| W21-02 | ID-18 | `Context/ContextFacade.cs` | 89 Runtime 测试 + 集成验证 |
| W21-03 | — | `tests/Keystone.Hosting.Tests/EndToEndIntegrationTests.cs` | 1 集成用例绿 |
| ID-18 | 03 §2；DEV-02 | `Context/ContextFacade.cs` | W21-02 |
| W22-01 | ID-19 | `Actors/CapabilityActor.cs` | `CapabilityDomainPipelineTests`（2） |
| W22-02 | ID-19 | `Actors/CapabilityDomain.cs` | 管道测试绿 |
| W22-03 | ID-08 | `Keystone.Hosting/KeystoneHost.cs` | 端到端 host.Events |
| W22-04 | 01 §2 | `tests/Keystone.Hosting.Tests/EndToEndIntegrationTests.cs` | 集成用例绿 |
| ID-19 | 01 §2；04 §2 | `Actors/`（管道） | W22-01~05 |
| W23-01~03 | 07/12 | `16-cordis-gap-review.md`（G-C1~C14） | 审计闭环 |
| W24-01~03 | ID-20 | `KeystoneHostOptions`、`KeystoneHost`、`PluginLoader`、`PluginRuntime` | 3 用例绿 |
| ID-20 | G-C1 | `Hosting/`、`Runtime/Plugins/`（config 注入） | W24-01~04 |
| W25-01~03 | ID-21 | `Runtime/Plugins/Lifecycle/PluginRuntime.cs` | `DependencyReArmTests` + 热重载恢复 |
| ID-21 | G-C2 | `Runtime/Plugins/Lifecycle/PluginRuntime.cs` | W25-01~04 |
| W26-01~03 | ID-22 | `Context/IServiceStore.cs`、`ServiceStore.cs`、`ContextFacade.cs`、`PluginRuntime.cs` | `ServiceValueUnloadTests`（2） |
| ID-22 | G-C3 | `Context/`（值注销） | W26-01~04 |
| W27-01 | ID-23 | `Events/EventBus.cs` | `BailSemanticsTests`（3） |
| ID-23 | G-C4 | `Events/EventBus.cs` | W27-01~02 |
| W28-01 | ID-24 | `Events/EventBus.cs`、`IEventBus.cs` | `WaterfallTerminalTests`（3） |
| ID-24 | G-C6 | `Events/`（waterfall terminal） | W28-01~02 |
| W29-01 | ID-25 | `Context/IPluginContext.cs`、`ContextFacade.cs` | `LazyInjectionTests`（3） |
| ID-25 | G-C5/M4 | `Context/`（GetLazy） | W29-01~02 |
| W30-01~02 | ID-26 | `Logging/`（ILogSink/ConsoleLogSink/RingBuffer） | `LogSinkTests`（4） |
| ID-26 | G-C7 | `Logging/`（sink 抽象） | W30-01~03 |
| W31-01~02 | ID-27 | `Keystone.Hosting/KeystoneHost.cs` | `HotReloadTests`（3） |
| ID-27 | G-C8 | `Keystone.Hosting/KeystoneHost.cs` | W31-01~03 |
| W32-01 | ID-28 | `Logging/RingBufferLoggerProvider.cs` | `LogLevelThresholdTests`（5） |
| ID-28 | G-C11 | `Logging/`（级别过滤） | W32-01~02 |

## 9. 维护规则

- **联动 R10**：14 是文档治理的一部分——阶段事件/决策/偏差的更新与 13、AGENTS.md 状态同步（P0 落地时 AGENTS.md "设计期"→"实现期"）
- **只追加不改写**：历史行不改；更正新增行引用旧编号（§1）
- **阶段退出检查**：14 §2 状态 + §6 验收台账 + §3 分节记录三者同时闭合才算记录闭合（13 §4 DoD）
- **回溯约定**：实现期任何"当时为什么这么做"的疑问 → 先查 §4（决策）→ §5（偏差）→ §3（工作项）→ 三向索引（§8）定位代码
