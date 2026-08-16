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
| P33 多实例集成 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 宿主级多实例集成测试（§7.33：插件组多实例并行/隔离/管道独立/TaskId/事件，237/237 全绿） | §7.33 |
| P34 文档达标 DC-1 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 实例级持久 context（§7.34：01 §4 actor 持 context 兑现，238/238 全绿） | §7.34 |
| P35 文档达标 DC-3/DC-6 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | quiesce 入口拒绝+超时审计 + rebind 报错+热重载顺序（§7.35，244/244 全绿） | §7.35 |
| P36 文档达标 DC-5 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 依赖超时接线（§7.36：依赖永不就绪→FAILED，246/246 全绿） | §7.36 |
| P37 文档达标 DC-4 | ✔ 验证通过 | 2026-08-15 | 2026-08-15 | — | 监督策略（§7.37：OneForOne + 重启计数 + 超阈值停止，248/248 全绿） | §7.37 |
| P38 文档达标 DC-8 | ✔ 验证通过 | 2026-08-16 | 2026-08-16 | — | 静态插值接线（§7.38：!!env/!!file tag 语法 + 环检测 + 宿主提供者注入，258/258 全绿） | §7.38 |
| P39 文档达标 DC-11 | ✔ 验证通过 | 2026-08-16 | 2026-08-16 | — | 事实事件接入运行链（§7.39：IFactEvent + 任务/生命周期事实 + 宿主 EventStore，266/266 全绿） | §7.39 |
| P40 文档达标 DC-10 | ✔ 验证通过 | 2026-08-16 | 2026-08-16 | — | 管道 swap 原子替换（§7.40：实例化缓存 + SwapPipelineAsync + 保留 actor/context，269/269 全绿） | §7.40 |
| P41 文档达标 DC-7 | ✔ 验证通过 | 2026-08-16 | 2026-08-16 | — | 宿主分层叠加（§7.41：StartAsync 多层按序叠加 + 逐层插值，274/274 全绿）——P1 四项闭合 | §7.41 |
| P42 文档达标 DC-16 | ✔ 验证通过 | 2026-08-16 | 2026-08-16 | — | disabled 挂起运行行为（§7.42：挂起不加载/父组继承/恢复 API，279/279 全绿；isolate 未接线记剩余） | §7.42 |
| P43 文档达标 DC-20 | ✔ 验证通过 | 2026-08-16 | 2026-08-16 | — | 日志 category 前缀 + 宿主 LoggerFactory（§7.43：{域}/{插件} 命名 + 子 context 继承，281/281 全绿；IOptions 命名选项记剩余） | §7.43 |
| P44 文档达标 DC-13 | ✔ 验证通过 | 2026-08-16 | 2026-08-16 | — | Trace 接入 + 幂等去重（§7.44：Activity 贯穿调用链 + TaskId 结果缓存，284/284 全绿） | §7.44 |
| P45 文档达标 DC-15 | ✔ 验证通过 | 2026-08-16 | 2026-08-16 | — | CRUD 落盘写回 + position（§7.45：ConfigFilePath 防抖写回 + 冲刷/排空 + Serialize 索引重载死代码修复，291/291 全绿） | §7.45 |
| P46 文档达标 DC-17 | ✔ 验证通过 | 2026-08-16 | 2026-08-16 | — | manifest configSchema + semver/白名单（§7.46：ConfigSchema 字段 + GeneratedRegex 校验 + 编译白名单，311/311 全绿） | §7.46 |
| P47 文档达标 DC-18 | ✔ 验证通过 | 2026-08-16 | 2026-08-16 | — | 事件分级落盘/归档/定时 Prune（§7.47：StoredFact.Durable + archivePath 归档 + FactRetentionScheduler 宿主接线，317/317 全绿） | §7.47 |
| P48 文档达标 DC-19 | ✔ 验证通过 | 2026-08-16 | 2026-08-16 | — | IPluginSource/IPluginHost 抽象边界（§7.48：获取端抽象 + LocalPluginSource + 运行形态扩展点预留 + 宿主优先接线，323/323 全绿） | §7.48 |
| P49 文档达标 DC-14 | ✔ 验证通过 | 2026-08-16 | 2026-08-16 | — | 取消贯穿全链（§7.49：DomainRequest 载 CT + context 链暴露 + fail-fast/取消异常归一，328/328 全绿） | §7.49 |
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
| ID-29 | 2026-08-15 | P34 | 实例级持久 context（DC-1）：CapabilityActor 构造时创建实例 context（父 = parentContext 可选），跨请求复用；中间件/请求在实例 context 上执行 | 01 §3/§4"actor=context 同生命周期"兑现；此前每请求新建 context 状态丢失；父链接入插件服务解析 + 共享事件总线（03 §2） | `src/Keystone.Runtime/Actors/CapabilityActor.cs`、`CapabilityDomain.cs` | 否（17-doc-compliance-audit DC-1） |
| ID-30 | 2026-08-15 | P35 | 文档达标 DC-3/DC-6：①全局 quiesce 补入口拒绝 + 总超时 + 未收敛审计（09 §4）；②rebind 重复注册报错 + 热重载先卸载再启动（02 §3/ADR-0007） | 兑现 09 §4 六步关闭语义与 rebind 语义；热重载顺序修复防同名注册冲突/误删 | `KeystoneHost`、`KeystoneHostOptions`、`ServiceRegistry`、`PluginLoader` | 否（17-doc-compliance-audit DC-3/DC-6） |
| ID-31 | 2026-08-15 | P36 | 依赖超时接线（DC-5）：WaitForDependenciesAsync 加超时（DependencyWaitTimeout 默认 30s，构造器可注入短超时）→ 超时 FAILED（GatingDependencyTimeout），错误经 AwaitAsync 可查 | ADR-0007 风险表"依赖永不就绪→启动超时→FAILED+告警"；此前 PENDING 无限挂起；配置存在但未接线（DC-5 模式） | `src/Keystone.Runtime/Plugins/Lifecycle/PluginRuntime.cs` | 否（17-doc-compliance-audit DC-5） |
| ID-32 | 2026-08-15 | P37 | 监督策略（DC-4）：CapabilityDomain.Spawn 配 OneForOneStrategy（Restart decider + MaxRestarts 默认 3/窗口 5s，超阈值停止不再重启 = 域不可用升级） | 05 §2/09 §3 监督承诺；此前裸 props 无监督配置；Proto OneForOneStrategy 承载重启计数 + 窗口语义 | `src/Keystone.Runtime/Actors/CapabilityDomain.cs`、`CapabilitySupervisionOptions.cs` | 否（17-doc-compliance-audit DC-4） |
| ID-33 | 2026-08-16 | P38 | 静态插值双层形态（DC-8，ADR-0012）：YAML 整值标量走 tag 形态（!!env NAME/!!file path，YamlDotNet TagName）；文本内容内引用走冒号前缀形态（!!env:NAME）；缺失保留标记（tag 形态重构为 `!!env NAME` 字符串，不静默替换）；环检测 visited 改展开栈语义（add→递归→remove，同文件多处引用非环） | ADR-0012 tag 机制保留（YamlDotNet 自定义 tag）；字符串中间嵌入标记不支持（ADR 示例均为整值）；原实现 visited 只增不减（误报环） | `src/Keystone.Config/Interpolation/StaticInterpolator.cs`、`Entries/EntryParser.cs`、`src/Keystone.Hosting/KeystoneHostOptions.cs` | 否（17-doc-compliance-audit DC-8） |
| ID-52 | 2026-08-16 | P56 | **发现接口收窄 + 同步契约**：`IServiceDiscovery` 定稿为"只读+通知"（`IsAvailable(name,realm)+Subscribe`），删 Register*/Unregister*——写生命周期已由 effect-disposer 覆盖（Provide 注册删键 disposer），发现层重复暴露写方法 = 浅接口；`IsAvailable` 永远同步本地读，未来分布式 adapter 也是"本地缓存+后台同步"（OnChanged→publish / 远端 watch→缓存），网络永不上门控热路径——今天零 async 感染 | 教训：**seam 接口的胖瘦决定未来 reshape 成本**——单 adapter 的 seam 形状未经第二实现验证，唯一对策是接口保持 2~3 成员使 reshape 代价趋零，并把契约（仅元数据/本地同步读/后台写同步）写进文档；另证实现状 ServiceRegistry 锁内发事件靠 Monitor 同线程重入"侥幸成立"，跨线程即死锁——新 store 出锁通知是修复真实隐患 | `docs/architecture/18-cordis-code-parity-audit.md` | 否（随可行性复核轮） |
| ID-51 | 2026-08-16 | P55 | **CA-1 决策收口 + 抽象接缝裁定**：①schema 裁定对齐 Cordis `Dict<name→true|"label">`（EntryOptions.Isolate 改 map + `IsolateSpec=Private|Shared(label)` + 列表 shim 迁移兼容）；②抽象化接缝——**值层（KeyedServiceStore 持活对象）进程内不可分布式，可分布式的是发现元数据**；故接缝画在发现层 `IServiceDiscovery`（异步 + 元数据 payload + watch + TTL），内存实现投影值 store（可用=ContainsKey 零冗余），未来 Redis/Consul/etcd 换实现零改动（同构 Steeltoe IDiscoveryClient、Aspire IServiceEndpointProvider）；③修正 P52"删 ServiceRegistry"为"升格"——IServiceRegistry → IServiceDiscovery 抽象，内存实现从独立冗余状态改为投影值 store，单一事实源不丢 | 教训：**抽象化前先分清"哪些状态天然进程内（活对象）vs 哪些可序列化分布（元数据）"**——把值层也抽象成可交换，Redis 实现无法兑现（存不了 .NET 实例），是错误抽象；.NET 生态先例（Steeltoe/Aspire）只抽象发现层，不抽象值层，印证接缝位置 | `docs/architecture/18-cordis-code-parity-audit.md`、`11-gap-register.md` | 否（随收口轮） |
| ID-50 | 2026-08-16 | P54 | **隔离默认语义裁定（源码级）**：Cordis `Context` 构造 isolate 映射为空（context.ts:72 `Object.create(null)`），`provide` 对未隔离名回落到 root 默认符号 `Symbol(name)`（reflect.ts:290 `??=`）——**不写 isolate = 共享**；隔离仅显式 `isolate: {name: true}`（LocalRealm #entryId 私有）/`{name:"label"}`（GlobalRealm @label 命名共享）。据此推翻 01 §4"每实例独立子 IServiceProvider"、03 §2.2"整 scope 隔离保留为默认/每实例独立 scope 根"——这些是当年 MS.DI IServiceScope 类比硬套，与 Cordis 相悖。能力域实例默认 realm 裁定 = ""（共享），隔离走 isolate 显式声明 | 教训：**框架语义（默认值/隔离方向）必须回到上游源码裁定，不能被"设计期类比"固化**——01/03 的错误类比存在了数月未被发现，与 CA-9（grep 漏检）、CA-1（漏看 isolate.ts 数据模型）同源：都缺"回到上游完整形态复核"这一步 | `docs/architecture/01-overview.md`、`03-context.md`、`18-cordis-code-parity-audit.md` | 否（随语义裁定轮） |
| ID-49 | 2026-08-16 | P53 | **决策批判修正**：①CA-1 漏判 schema 分叉——Cordis isolate 是 Dict<name→true|"label">（true=LocalRealm 条目私有域 #entryId / "label"=GlobalRealm 共享命名域 @label，isolate.ts 全文），Keystone 是列表（EntryParser.cs:92 StringList）且 08 §3 定位"组级"；此前"机制已有缺接线"结论只覆盖了运行时侧，未覆盖配置模型侧。②CA-3 并行缺陷——Keystone 门控有超时（DC-5 GatingDependencyTimeout，PluginRuntime.cs:285 不无限 PENDING），Cordis PENDING 无限等，组内全并行会让依赖兄弟的新条目伪超时。③CA-13 RebindPolicy 冗余（provider 重启已被 P25/P26 unload→re-register 链覆盖；owner 比对复现不了 epoch 的 fiber-uid 语义）。④CA-12 首步拆两阶段（默认 provider 先于 ServiceOptions）。⑤CA-6/7 与 ADR-0013 源抽象的张力 | 教训：**方案评审要回到上游事实的完整形态**（Cordis 字段是 map 不是 list——只看了 context.ts 的 isolate 方法签名，没看 loader/config/isolate.ts 的数据模型，导致方案建立在错误 schema 上） | `docs/architecture/18-cordis-code-parity-audit.md`（v3：§2 + §5.1） | 否（随批判轮） |
| ID-48 | 2026-08-16 | P52 | **研判修正记录**：①CA-9 初判"计时器不随卸载回收"为 grep 误报——初版检索 `_ctx.Effect` 漏检实际写法 `ctx.Context.Effect`（TimerHandle 构造尾已注册 + PluginRuntime.cs:354 quiesce 收敛 DisposeEffectsAsync）；残留真实问题仅两个轻微点（DisposeAsync 的 _cts.Dispose() 与在途 Task.Delay 注册竞态可抛 ObjectDisposedException 漏网 / effect 收敛不等在途回调）；②CA-1 收窄——ContextFacade 每 context 独立 store + Resolve 沿父链 + 类注释已声明"独立链=天然隔离"，缺的是 EntryOptions.Isolate 配置接线（宿主三处工厂一律挂 root）与 registry 门控域感知；解决方案从 ServiceStore 键扩展改为 ContextFacade isolateNames 分支（侵入更小），分最小/完整两档待选 | 教训入库：**抽样 grep 不能作为"不存在"结论的依据**——否定性结论必须全文读码或双模式检索复核；P45 死代码 bug（ID-41）与本次误报同源（声明与实现的错位靠单一检索模式必然漏检） | `docs/architecture/18-cordis-code-parity-audit.md`（v2） | 否（随研判轮） |
| ID-47 | 2026-08-16 | P51 | **审计方法决策**：17 审计（文档承诺 vs 实现）闭合后，功能差距复查不再采信任何文档状态表（11/16/17 的 ✅ 不作依据）——直接提取 vendored Cordis 源码运行面（8 核心文件类成员 + loader EntryTree/Group + include 文件管线，≈95 行为点）与本仓 src/ 逐项 grep/读码验证。产出 CA-1~18（未实现 12 + 差异 6 ≈ 18%）+ 每项实现提案（18 §2/§3），**全部待人工决策不实施**（18 §5 决策矩阵：P0 正确性 2 项——CA-9 计时器僵尸副作用 / CA-10 组删除孤儿插件） | 口径升级动机：P45 Serialize 索引重载死代码 bug（ID-41）实证"文档说有 ≠ 代码能用"；登记为 18 新文档而非并入 17（17 是文档达成度口径，18 是代码等价口径，结论不互通） | `docs/architecture/18-cordis-code-parity-audit.md`、`11-gap-register.md` §3.3 | 否（提案集，决策后分流） |
| ID-46 | 2026-08-16 | P50 | 配置热重载管线（DC-9，08 §6）：①ConfigDiffer.Diff（树扁平化按 id 对齐；五路分类 = 新增/移除/仅 config 变/结构变(name·inject·isolate)/disabled 翻转——08 §6.1 分级判定；config 比对逐键，引用相等短路）；②ApplyConfigAsync 编排（_applyingConfig 自旋串行化防 watcher/CRUD 竞态交错 08 §6.3；逐条目路由既有动作：Create/Remove/SetEntryDisabled/ReloadPlugin（冷重启）/UpdatePlugin（热更新·瀑布可否决）；ConfigReloaded/PluginUpdating/PluginReloading 事件）；③ConfigFileWatcher（FileSystemWatcher + 100ms 防抖合并；回调异常吞掉续听——旁路降级保留旧树"最后好树保持运行"）；④EnableConfigWatch（ConfigFilePath 必填校验；重读 → EntryParser → ApplyConfigAsync；随宿主 Dispose 停） | 复用既有单条目动作（不另起一套应用逻辑——单一事实源）；组级事务（08 §6.2）与失败回滚逐条目（§6.1 聚合异常）为后续增强，本项落触发/diff/分级主链；watcher 默认关闭（显式 EnableConfigWatch——嵌入方控制） | `src/Keystone.Hosting/ConfigDiffer.cs`、`ConfigDiff.cs`、`ConfigFileWatcher.cs`、`ConfigReloadedEventArgs.cs` 等、`KeystoneHost.cs` | 否（17-doc-compliance-audit DC-9） |
| ID-45 | 2026-08-16 | P49 | 取消传播通道（DC-14，06 §1）：①DomainRequest 增 CT 参数（默认 default）——CT 属运行态不入 TaskEnvelope DTO；本地消息按引用传递即达 actor，远程化演进时换超时预算；②ContextFacade 增请求 CT 槽 + `IPluginContext.CancellationToken`（自身槽未设置沿父链取——插件 handler 闭包读自身 context 即得实例级请求 CT；均无 = None 无请求语义）；actor 串行循环内 Set/Reset（单写者）；③已取消请求 fail-fast（不执行 handler，记录 PipelineCancelled 失败结果——幂等缓存可回放）；中间件/handler 抛 OperationCanceledException → 同归一（失败非监督重启） | CT 暴露选 context 链而非改 IMiddleware/handler 签名（公共面加法最小——中间件/handler 均经 context 读同一槽）；Proto 传输层对已取消 token 抛 ArgumentException 拒于发送——actor 侧语义经 SendRaw 测试缝（InternalsVisibleTo Keystone.Runtime.Tests）验证 | `Actors/DomainRequest.cs`、`Actors/CapabilityActor.cs`、`Actors/CapabilityHandle.cs`、`Context/{IPluginContext,ContextFacade}.cs` | 否（17-doc-compliance-audit DC-14） |
| ID-44 | 2026-08-16 | P48 | 插件获取/运行抽象边界（DC-19，ADR-0001 决策 1-2）：①IPluginSource = 获取端抽象（FetchAsync(manifest, ct) → PluginSource）——演进路径本地→签名→远程仅替换实现，编译/ALC/dispose 管线不动；②LocalPluginSource 初始实现（manifest.Main 相对多根目录解析 + {root}/{id}/{main} 回退；未找到 → ConfigProviderFailed 精确报错）；③IPluginHost = 运行形态扩展点**预留**（IsolationModel 描述符；DefaultPluginHost.Instance = same-process-alc 本期唯一形态，方案 B 独立进程未来经此引入）；④KeystoneHostOptions.PluginSource/PluginHost + LoadEntryAsync 抽象优先于 SourceProvider 委托（委托保留向后兼容） | 接口放 Runtime/Plugins/Loading（与 PluginSource 同层）；获取端 async（远程分发天然异步）；PluginHost 仅描述符不接装配流程（预留面最小化——ADR-0001 "不进入本期默认配置"） | `src/Keystone.Runtime/Plugins/Loading/{IPluginSource,LocalPluginSource,IPluginHost,DefaultPluginHost}.cs`、`Hosting/` | 否（17-doc-compliance-audit DC-19） |
| ID-43 | 2026-08-16 | P47 | 事件保留闭环（DC-18，ADR-0009 决策 3）：①StoredFact 增 Key(8) Durable——EventBus.PersistFactAsync 落盘时携带（旧数据缺键反序列化 = false 尽力写，向前兼容）；②FileEventStore 增 archivePath 构造参数——Prune 被清事实先同帧格式追加归档（可重放/审计）再重写主文件，未配置 = 纯删除原行为；③FactRetentionScheduler（PeriodicTimer 循环 PruneAsync，单轮失败降级吞掉续跑——旁路硬约束）；④宿主 KeystoneHostOptions.RetentionPolicy/PruneInterval（默认 1h）→ StartAsync 起 DisposeAsync 停（EventStore 与 Retention 同时配置才启用） | 归档 = 同帧格式追加文件（复用 FileEventStore 重放器可读，不引入新格式）；定时器在宿主层不在存储层（存储保持无后台线程的可嵌入性）；降级语义与 P39 EventBus 非 durable 吞错对齐 | `src/Keystone.Runtime/Persistence/StoredFact.cs`、`FileEventStore.cs`、`FactRetentionScheduler.cs`、`src/Keystone.Hosting/KeystoneHost.cs` | 否（17-doc-compliance-audit DC-18） |
| ID-42 | 2026-08-16 | P46 | manifest 校验扩展（DC-17，10 §6）：PluginManifest 增 ConfigSchema 可选参数（null = 无 schema 声明，G-C1 原始直传语义不变）；ManifestSchemaValidator 增两项——version 语义化版本（semver 2.0 形态：MAJOR.MINOR.PATCH[-prerelease][+build]，GeneratedRegex + NonBacktracking 防 ReDoS，MA0009/MA0023 合规）、dependencies ⊆ AssemblyWhitelist 公共集合（cordis-runtime/contracts + Keystone.* + M.E.Logging.Abstractions；越界精确报错——规则 0：System.Reflection.Emit 等宿主禁用依赖在编译期拦截） | 10 §6 "version 必填，语义化版本"+"程序集编译白名单"；白名单做公共静态集合（嵌入方可审查/扩展面）；semver 用 NonBacktracking（AOT 安全 + 线性时间） | `src/Keystone.Runtime/Plugins/Manifest/PluginManifest.cs`、`src/Keystone.Sdk/Manifest/ManifestSchemaValidator.cs` | 否（17-doc-compliance-audit DC-17） |
| ID-40 | 2026-08-16 | P45 | CRUD 落盘形态（DC-15，09 §5/08 §6.3）：`KeystoneHostOptions.ConfigFilePath` → 惰性 ConfigFileWriter；全部变更点（Create/Remove/Move/SetEntryDisabled/UpdatePlugin-apply 成功后）ScheduleWriteBack（NotifyConfigUpdate 前置 F9 + 防抖快照 DumpConfig）；`FlushConfigAsync` 冲刷 + Shutdown 排空（写失败不阻断关闭，08 §6.3 readonly 报错不崩溃）+ Dispose 释放；CreateEntry/MoveEntry 增 position（根/组内 Insert 指定下标，越界回退追加） | position 语义 = 插入下标（09 §5 "含插入位置"）；写回经防抖（多次变更合并一次写）；UpdatePlugin 经 PatchContext 瀑布——否决不落盘 | `src/Keystone.Hosting/KeystoneHost.cs`、`KeystoneHostOptions.cs` | 否（17-doc-compliance-audit DC-15） |
| ID-41 | 2026-08-16 | P45 | **死代码 bug 修复**：`EntrySerializer.Serialize` 的 `entries.Select(SerializeEntry)` 方法组绑定 `Enumerable.Select` 的 `(TSource, Func<TSource,int,TResult>)` 索引重载——`SerializeEntry(entry, int indent = 0)` 的可选参数被喂元素下标：第 N 条目缩进 N 空格，≥2 条目写回即损坏。改为显式 lambda `e => SerializeEntry(e)` | DC-15 "ConfigFileWriter 死代码"审计的真实代价实证：零调用路径 bug 从未被测试暴露——接线时立即现形；回归测试固化（IndexRegressionTests 断言无 " - id" 缩进泄漏） | `src/Keystone.Config/Persistence/EntrySerializer.cs` | 否（随 P45） |
| ID-39 | 2026-08-16 | P44 | Trace/幂等接线形态（DC-13，06 §3-§4）：ExecuteTracedAsync 包裹 TraceContext.StartTask（TaskId/ParentTaskId/能力域/操作 tag；finally Dispose 恢复前序 Activity——请求结束不残留）；幂等缓存 = actor 内 Dictionary<Guid, TaskResultEnvelope> + Queue FIFO（容量 1024 防无界）；缓存命中直接回结果——**不重执行、不重发事实**（事实已随首执行记录） | 06 §4 "TaskId 即幂等键，重试不得重复执行副作用"；缓存放 actor 实例级（每实例独立能力域请求面；跨实例幂等属编排层职责）；Activity 生命周期 = 单请求（结束即恢复，长命 actor 不持跨请求 trace） | `src/Keystone.Runtime/Actors/CapabilityActor.cs` | 否（17-doc-compliance-audit DC-13） |
| ID-38 | 2026-08-16 | P43 | 日志命名接线形态（DC-20，05 §5）：ContextFacade 增 logCategoryPrefix（root 设 {能力域名}，子 context 构造继承——GetLogger 输出 {域}/{插件 ID}）+ loggerFactory 子 context 复用 root 工厂；KeystoneHostOptions.LoggerFactory 注入根 context（null = NullLogger 原行为） | 05 §5 category={能力域}/{插件 ID}；前缀经 context 链继承（对齐服务解析链形态）而非宿主逐插件传——插件 context 创建点（contextFactory）不变 | `src/Keystone.Runtime/Context/ContextFacade.cs`、`Hosting/KeystoneHostOptions.cs`、`KeystoneHost.cs` | 否（17-doc-compliance-audit DC-20） |
| ID-37 | 2026-08-16 | P42 | disabled 挂起形态（DC-16，08 §3）：EnumerateActiveLeaves 过滤（自身或祖先 disabled=true 的叶子不参与加载/门控拓扑/manifest 校验——挂起条目 inject 引用放宽，恢复时经加载路径再校验）；`SetEntryDisabledAsync(id, bool)`——true=卸载（条目树保留，挂起不删），false/null=加载恢复（"改回即恢复"）；manifest 校验走 active 集（挂起依赖引用缺失不再阻断启动） | 08 §3 "挂起不删，改回即恢复；父组 disabled → 子树全部挂起（组自身永不挂起）"；挂起条目不是运行成员——其 inject 引用不应参与可达性 fail-fast（否则挂起一个依赖方就阻塞整树启动） | `src/Keystone.Hosting/KeystoneHost.cs` | 否（17-doc-compliance-audit DC-16） |
| ID-36 | 2026-08-16 | P41 | 宿主分层叠加形态（DC-7，08 §4）：`StartAsync(IReadOnlyList<string>)` 多层重载——逐层独立解析（DC-8 插值按层展开）→ EntryTree.ApplyLayers 叠加（patch 按 id 合并/insert 插入/层内重复 fail-fast）；`StartAsync(string)` 转发单层（原语义零破坏）；空层列表 = ConfigValidationFailed | 08 §4 叠加以条目 id 为主键；patch 语义 = 替换整个 config（08 §4 原文"替换整个 config"）非逐 key 合并；环境选择 = 调用方选层（overlay 由嵌入方组装，框架不内置环境探测） | `src/Keystone.Hosting/KeystoneHost.cs` | 否（17-doc-compliance-audit DC-7） |
| ID-35 | 2026-08-16 | P40 | 管道缓存 + swap 形态（DC-10，ADR-0003 决策 2）：CapabilityActor 构造时构建管道缓存（跨请求复用，无中间件=直通管道）；terminal 经 actor 级当前请求槽路由 handler（链缓存不绑定特定请求；actor 串行循环内无竞争）；SwapPipeline 消息 → 新链构建后 volatile 换引用（原子替换）；保留 actor/context，在途请求已捕获旧链（串行无半新半旧交错） | 04 §8"管道配置热更新=原子替换"；原实现每请求重建（terminal 捕获 envelope 所致）→ 槽路由解耦请求与链；CapabilityDomain.SwapPipelineAsync 公开面（Send 单向消息，无需响应） | `src/Keystone.Runtime/Actors/CapabilityActor.cs`、`SwapPipeline.cs`、`CapabilityDomain.cs` | 否（17-doc-compliance-audit DC-10） |
| ID-34 | 2026-08-16 | P39 | 事实事件接线形态（DC-11，ADR-0009）：`IFactEvent` 标记接口（TaskId/Capability/Payload/Durable）+ EventBus 构造注入 `IEventStore`——emit 分发**先记录后分发**（观察者异常不丢事实）；durable=true 写失败传播、false 尽力写降级（ADR-0009 决策 3）；事实流：能力域 actor 任务完成/失败（Capability=实例名）+ PluginRuntime 生命周期（经 context 总线，无 context 阶段用外部总线参数）；宿主 EventStore 选项挂根总线（子链共享） | 03 §4 事实事件=emit 分发模式（拦截/策略事件不持久）；先记录后分发保证观察者异常不丢事实；内置事实全 Durable=false（持久化失败不改变主链语义——任务结果已定，不因旁路写失败翻转） | `src/Keystone.Runtime/Events/IFactEvent.cs`、`Facts*.cs`、`EventBus.cs`、`Actors/CapabilityActor.cs`、`Plugins/Lifecycle/PluginRuntime.cs`、`Context/ContextFacade.cs`、`Hosting/KeystoneHostOptions.cs` | 否（17-doc-compliance-audit DC-11） |

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

### 7.33 P33 宿主级多实例集成测试

> 01 §4 多实例模型兑现：同一插件组 spawn 多个能力域实例，各自独立 context/管道、并行处理不同任务。此前仅 Runtime 层单测验证隔离，宿主级完整链路缺失。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W33-01 | 多实例集成测试：calc 插件组（业务）+ observer（事件观察）+ audit-mw（管道中间件）——宿主启动后 spawn 3 实例并行处理不同任务（add/mul/sub），验证多实例隔离（独立结果）/管道每实例独立（before/after）/TaskId 贯穿/事件观察（共享总线 ID-08） | 测试（集成） | 01 §4；03 §2.2 | `tests/Keystone.Hosting.Tests/MultiInstanceIntegrationTests.cs` | 1 集成用例绿（3 次连跑稳定） | ✅ |
| 2026-08-15 | W33-02 | 全量回归 237/237 + Hosting AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.34 P34 文档达标 DC-1：实例级持久 context

> 用户指出"按文档要求做"。workflow 审计 30 项差距（5 域 × 6），DC-1 是核心（01 §4 每实例独立持久 context，此前实现每请求新建）。本次修复 + 审计文档产出。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W34-01 | workflow 并行审计：5 域（core-context/plugin-lifecycle/pipeline-config/sdk-ai/security-aot）对照文档 vs 实现 → 30 项差距（高 12 + 中 18） | 审计 | — | — | 5 份域审计 + 汇总 | ✅ |
| 2026-08-15 | W34-02 | **DC-1 修复**：CapabilityActor 持实例级持久 context（构造时创建，跨请求复用；父 = parentContext 可选）；中间件/请求在实例 context 上执行（接入父链服务解析 + 共享事件总线） | 实现（TDD） | 01 §3/§4；03 §2 | `src/Keystone.Runtime/Actors/CapabilityActor.cs`、`CapabilityDomain.cs` | `Instance_context_is_persistent_across_requests`（中间件经 ctx 跨 3 请求累积=3） | ✅ |
| 2026-08-15 | W34-03 | 全量回归 238/238 + Runtime AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.35 P35 文档达标 DC-3/DC-6

> 17-doc-compliance-audit P0 项：DC-3（09 §4 全局 quiesce：入口拒绝 + 超时审计 + 停监督）与 DC-6（02 §3 rebind=报错 + 热重载注册保持）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W35-01 | DC-3：KeystoneHost.ShutdownAsync 补入口拒绝（CreateEntry/Mount/Reload/Update 检查 _shutdown）+ 总关闭超时（ShutdownTimeout 默认 30s）+ 未收敛插件审计（UncollectedPlugins） | 实现（TDD） | 09 §4；ADR-0005 | `src/Keystone.Hosting/KeystoneHost.cs`、`KeystoneHostOptions.cs` | `ShutdownGateTests`（3） | ✅ |
| 2026-08-15 | W35-02 | DC-6：ServiceRegistry.Register 重复（他属主）→ 报错（rebind 语义）；IsAvailable 加锁；ReloadPluginAsync/PluginLoader.ReloadAsync 改**先卸载旧再启动新**（避免同名注册冲突/误删） | 实现（TDD） | 02 §3；ADR-0007 | `src/Keystone.Runtime/Plugins/Services/ServiceRegistry.cs`、`Loading/PluginLoader.cs`、`KeystoneHost.cs` | `RebindAndReloadTests`（3） | ✅ |
| 2026-08-15 | W35-03 | 全量回归 244/244 + Runtime/Hosting AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` × 2 | ✅ |

### 7.36 P36 文档达标 DC-5：依赖超时接线

> 17-doc-compliance-audit DC-5（🔴）：依赖永不就绪 → PENDING 无限挂起（DependencyWaitTimeout 配置存在但未接线）。本次接入：依赖等待超时 → FAILED（GatingDependencyTimeout，ADR-0007 风险表）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W36-01 | PluginRuntime：构造器增 dependencyTimeout（默认 KeystoneSettings.DependencyWaitTimeout 30s）；WaitForDependenciesAsync 加超时（超时抛 GatingDependencyTimeout）；AwaitDependenciesOrFailAsync 提取（超时 → FAILED + 错误可查） | 实现（TDD） | DC-5；ADR-0007 | `src/Keystone.Runtime/Plugins/Lifecycle/PluginRuntime.cs` | `DependencyTimeoutTests`（2） | ✅ |
| 2026-08-15 | W36-02 | 全量回归 246/246 + Runtime AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.37 P37 文档达标 DC-4：监督策略

> 17-doc-compliance-audit DC-4（🔴）：Spawn 裸 props 无监督配置（05 §2/09 §3 承诺 OneForOne + 重启计数 + 超阈值升级不可用）。本次配 OneForOneStrategy（Restart decider + 窗口重试上限，超阈值停止不再重启）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-15 | W37-01 | `CapabilitySupervisionOptions`（MaxRestarts 默认 3 / RestartWindow 默认 5s）+ `CapabilityDomain.Spawn` 配 OneForOneStrategy（Restart decider，超阈值停止 = 域不可用升级） | 实现（TDD） | 05 §2；09 §3；DC-4 | `src/Keystone.Runtime/Actors/CapabilityDomain.cs`、`CapabilitySupervisionOptions.cs` | `SupervisionPolicyTests`（2） | ✅ |
| 2026-08-15 | W37-02 | 全量回归 248/248 + Runtime AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

> **P0 高危 5 项全部修复**：DC-1（P34）/ DC-3（P35）/ DC-6（P35）/ DC-5（P36）/ DC-4（P37）。

> **审计发现**：30 项差距集中在"功能实现了但未按文档接线/语义简化"（quiesce 五步缺拒绝/排空、监督缺失、超时熔断死代码、分层叠加孤立、静态插值死代码、事件持久化孤立、管道无原子替换等）——见 17-doc-compliance-audit.md（待产出）。

### 7.38 P38 文档达标 DC-8：静态插值接线

> 17-doc-compliance-audit DC-8（❌→✅）：StaticInterpolator 零调用 + EntryParser 丢 tag（冒号语法偏差）。本次接通：tag 语法展开 + 环检测 + 宿主提供者注入（ADR-0012/08 §4 兑现）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W38-01 | StaticInterpolator 增 `InterpolateTagged`（EnvTag/FileTag，tag:yaml.org,2002:*）+ 缺失保留标记 + visited 展开栈语义（add→递归→remove，同文件多处引用非环） | 实现（TDD） | DC-8；ADR-0012；ID-33 | `src/Keystone.Config/Interpolation/StaticInterpolator.cs` | `EntryParserInterpolationTests`（8） | ✅ |
| 2026-08-16 | W38-02 | EntryParser 接插值器：`Parse(string, StaticInterpolator?)` + NodeToObject 对 !!env/!!file tag 标量展开（config 子树；visited 跨整次解析共享） | 实现（TDD） | DC-8；ADR-0012；ID-33 | `src/Keystone.Config/Entries/EntryParser.cs` | 同上（含嵌套结构/无插值器兼容） | ✅ |
| 2026-08-16 | W38-03 | 宿主接线：KeystoneHostOptions 增 EnvProvider/FileProvider（任一配置即启用）；StartAsync 解析时展开（展开结果进 schema 校验，08 §5） | 实现（TDD） | DC-8；08 §5 | `src/Keystone.Hosting/KeystoneHostOptions.cs`、`KeystoneHost.cs` | `InterpolatedConfigTests`（2） | ✅ |
| 2026-08-16 | W38-04 | 全量回归 258/258 + Config/Hosting AOT 发布零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` × 2 | ✅ |

### 7.39 P39 文档达标 DC-11：事实事件接入运行链

> 17-doc-compliance-audit DC-11（❌→✅）：IEventStore 孤立，EventBus/PluginRuntime 不写存储。本次接通：IFactEvent 标记 + 任务/生命周期事实 + 宿主 EventStore（ADR-0009/03 §4"任务完成/失败必须存活"兑现）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W39-01 | `IFactEvent` 标记接口（TaskId/Capability/Payload/Durable）+ 内置事实：TaskCompletedFact/TaskFailedFact/PluginStartedFact/PluginFailedFact（全尽力写） | 实现（TDD） | DC-11；ADR-0009 决策 3；ID-34 | `src/Keystone.Runtime/Events/IFactEvent.cs`、`*Fact.cs` | `FactPersistenceTests`（7） | ✅ |
| 2026-08-16 | W39-02 | EventBus 构造注入 `IEventStore`：emit 分发先记录后分发；durable 分级（true 写失败传播 / false 尽力写降级）；非事实事件不持久化 | 实现（TDD） | DC-11；ADR-0009 | `src/Keystone.Runtime/Events/EventBus.cs` | 同上（含 FailingStore 降级/传播 2 例） | ✅ |
| 2026-08-16 | W39-03 | 事实流接线：CapabilityActor 任务完成/失败发布（Capability=实例名；Spawn 可注 eventStore）；PluginRuntime ACTIVE/FAILED/超时发布生命周期事实（经 context 总线，无 context 用外部总线） | 实现（TDD） | DC-11；04 §7 | `Actors/CapabilityActor.cs`、`CapabilityDomain.cs`、`Plugins/Lifecycle/PluginRuntime.cs` | actor 事实 2 例 + `PluginLifecycleFactTests`（1） | ✅ |
| 2026-08-16 | W39-04 | 宿主/ContextFacade 接线：`KeystoneHostOptions.EventStore` → 根总线携带（子链共享）；ContextFacade 构造可选 eventStore（有父总线共享父的） | 实现（TDD） | DC-11 | `Hosting/KeystoneHostOptions.cs`、`KeystoneHost.cs`、`Context/ContextFacade.cs` | `FactStoreHostTests`（1） | ✅ |
| 2026-08-16 | W39-05 | 全量回归 266/266 + Runtime AOT 发布零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.40 P40 文档达标 DC-10：管道 swap 原子替换

> 17-doc-compliance-audit DC-10（❌→✅）：管道每请求重建、无 swap API。本次兑现 ADR-0003 决策 2/04 §8——实例化缓存 + 原子替换 + 保留 actor/context。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W40-01 | CapabilityActor 管道实例化缓存：构造时构建一次（无中间件 = DirectPipeline 直通）；terminal 经 actor 级当前请求槽路由 handler（链与请求解耦） | 实现（TDD） | DC-10；ADR-0003 决策 2；ID-35 | `src/Keystone.Runtime/Actors/CapabilityActor.cs` | `Pipeline_is_cached_not_rebuilt_per_request` | ✅ |
| 2026-08-16 | W40-02 | SwapPipeline 消息（actor 串行循环内换 volatile 引用）+ `CapabilityDomain.SwapPipelineAsync`（Send 单向）；保留 actor/context（状态不丢），在途走旧链 | 实现（TDD） | DC-10；04 §8；ID-35 | `src/Keystone.Runtime/Actors/SwapPipeline.cs`、`CapabilityDomain.cs` | `Swap_replaces_middleware_chain_atomically` + `Swap_preserves_instance_context_state` | ✅ |
| 2026-08-16 | W40-03 | 全量回归 269/269 + Runtime AOT 发布零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.41 P41 文档达标 DC-7：宿主分层叠加

> 17-doc-compliance-audit DC-7（❌→✅）：EntryTree.ApplyLayers 孤立、宿主只吃单 YAML。本次接通多层层叠（08 §4 base → profile → patch → overlay）——P1 四项闭合。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W41-01 | `StartAsync(IReadOnlyList<string>)` 多层重载：逐层解析（含 DC-8 插值）→ EntryTree.ApplyLayers 叠加 → 既有启动管线（manifest 校验/门控加载）；`StartAsync(string)` 转发单层（兼容） | 实现（TDD） | DC-7；08 §4；ID-36 | `src/Keystone.Hosting/KeystoneHost.cs` | `LayeredConfigTests`（5） | ✅ |
| 2026-08-16 | W41-02 | 全量回归 274/274 + Hosting AOT 发布零 IL 警告（MSBuild Server 残留态导致的 MSB3491 经 build-server shutdown + 清 obj 排除，非代码问题） | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.42 P42 文档达标 DC-16：disabled 挂起运行行为

> 17-doc-compliance-audit DC-16（部分）：disabled 字段有模型无行为。本次兑现挂起/恢复/父组继承；isolate 组级隔离（3 §2.2）记剩余。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W42-01 | EnumerateActiveLeaves：挂起叶子（自身/祖先 disabled）不参与加载拓扑与 manifest 校验；父组 disabled → 子树全挂 | 实现（TDD） | DC-16；08 §3；ID-37 | `src/Keystone.Hosting/KeystoneHost.cs` | `DisabledEntryTests`（5） | ✅ |
| 2026-08-16 | W42-02 | `SetEntryDisabledAsync`：true 卸载（树保留）/false 加载恢复；挂起条目不 PENDING 占坑（不等待依赖） | 实现（TDD） | DC-16；ID-37 | 同上 | 同上 | ✅ |
| 2026-08-16 | W42-03 | 全量回归 279/279 + Hosting AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.43 P43 文档达标 DC-20：日志 category 前缀 + 宿主 LoggerFactory

> 17-doc-compliance-audit DC-20（部分）：category 无域前缀、宿主未接 loggerFactory。本次兑现命名规则 + 工厂接线；IOptions 命名选项级别覆盖记剩余。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W43-01 | ContextFacade：logCategoryPrefix（root 设域前缀，子 context 构造继承）+ loggerFactory 子 context 复用 root 工厂；GetLogger = {域}/{name} | 实现（TDD） | DC-20；05 §5；ID-38 | `src/Keystone.Runtime/Context/ContextFacade.cs` | `LoggingCategoryTests`（2） | ✅ |
| 2026-08-16 | W43-02 | KeystoneHostOptions.LoggerFactory → 根 context（EnableCapabilityDomain=false 时无前缀）；null = NullLogger 原行为 | 实现（TDD） | DC-20；ID-38 | `Hosting/KeystoneHostOptions.cs`、`KeystoneHost.cs` | 同上（含回退用例） | ✅ |
| 2026-08-16 | W43-03 | 全量回归 281/281 + Runtime/Hosting AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` × 2 | ✅ |

### 7.44 P44 文档达标 DC-13：Trace 接入 + TaskId 幂等去重

> 17-doc-compliance-audit DC-13（❌→✅）：TraceContext 零调用、无幂等机制。本次接通能力域调用链 Activity 贯穿 + TaskId 结果缓存（06 §3/§4 兑现）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W44-01 | ExecuteTracedAsync：请求执行包裹 keystone.task Activity（四 tag 贯穿；finally Dispose 恢复前序）——中间件/服务内读 Activity.Current 即得 TaskId（H1） | 实现（TDD） | DC-13；05 §5；06 §3；ID-39 | `src/Keystone.Runtime/Actors/CapabilityActor.cs` | `Request_execution_carries_trace_context` + `Activity_carries_capability_and_operation_tags` | ✅ |
| 2026-08-16 | W44-02 | TaskId 幂等去重：重复请求回缓存结果不重执行（FIFO 1024 上限）；命中不重发事实 | 实现（TDD） | DC-13；06 §4；ID-39 | 同上 | `Duplicate_task_id_returns_cached_result_without_reexecution` | ✅ |
| 2026-08-16 | W44-03 | 全量回归 284/284 + Runtime AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.45 P45 文档达标 DC-15：CRUD 落盘写回 + position

> 17-doc-compliance-audit DC-15（❌→✅）：_tree 纯内存、ConfigFileWriter 死代码。本次接线写回管线 + position 参数；接线过程挖出并修复 Serialize 索引重载死代码 bug（ID-41）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W45-01 | CreateEntry/MoveEntry 增 position 参数（根/组内 Insert 指定下标；越界回退追加） | 实现（TDD） | DC-15；09 §5；ID-40 | `src/Keystone.Hosting/KeystoneHost.cs` | `CreateEntry_with_position_inserts_at_index` + `MoveEntry_with_position_reorders` | ✅ |
| 2026-08-16 | W45-02 | CRUD 落盘写回：ConfigFilePath 选项 → 惰性 ConfigFileWriter；全变更点防抖写回 + FlushConfigAsync + Shutdown 排空 + Dispose 释放 | 实现（TDD） | DC-15；08 §6.3；ID-40 | 同上 + `KeystoneHostOptions.cs` | `CrudPersistenceTests`（4 落盘用例 + 1 纯内存回退） | ✅ |
| 2026-08-16 | W45-03 | **死代码修复**：Serialize 方法组索引重载 bug（第 N 条目缩进 N 空格）+ 回归测试 | 修复（TDD） | ID-41 | `src/Keystone.Config/Persistence/EntrySerializer.cs` | `EntrySerializerIndexRegressionTests`（1） | ✅ |
| 2026-08-16 | W45-04 | 全量回归 291/291 + Config/Hosting AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` × 2 | ✅ |

### 7.46 P46 文档达标 DC-17：manifest configSchema + semver/白名单校验

> 17-doc-compliance-audit DC-17（❌→✅）：缺 configSchema 字段、version 只查非空、依赖无白名单。本次补齐 manifest schema 三缺口。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W46-01 | PluginManifest.ConfigSchema 可选参数（null = 无声明原始直传，G-C1 语义不变） | 实现（TDD） | DC-17；10 §6；ID-42 | `src/Keystone.Runtime/Plugins/Manifest/PluginManifest.cs` | `Config_schema_is_optional_and_preserved` | ✅ |
| 2026-08-16 | W46-02 | version 语义化版本校验（GeneratedRegex NonBacktracking：semver 含预发布/构建元数据形态） | 实现（TDD） | DC-17；ID-42 | `src/Keystone.Sdk/Manifest/ManifestSchemaValidator.cs` | `Semantic_versions_pass`（6）+ `Non_semantic_versions_fail_fast`（5） | ✅ |
| 2026-08-16 | W46-03 | dependencies 编译白名单（公共 AssemblyWhitelist 集合；越界精确报错 fail-fast） | 实现（TDD） | DC-17；规则 0；ID-42 | 同上 | `Whitelisted_dependencies_pass`（6）+ `Non_whitelisted_dependency_fails_fast` | ✅ |
| 2026-08-16 | W46-04 | 全量回归 311/311 + Sdk AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.47 P47 文档达标 DC-18：事件分级落盘 + Prune 归档 + 定时执行

> 17-doc-compliance-audit DC-18（❌→✅）：StoredFact 无 Durable、Prune 无归档无定时。本次闭合 ADR-0009 决策 3 保留策略三缺口（降级语义 P39 已先行）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W47-01 | StoredFact.Durable（Key(8)，EventBus 落盘携带；旧数据缺键 = 尽力写向前兼容） | 实现（TDD） | DC-18；ADR-0009 决策 3；ID-43 | `StoredFact.cs`、`Events/EventBus.cs` | `Durable_flag_round_trips_through_store` | ✅ |
| 2026-08-16 | W47-02 | FileEventStore.archivePath：Prune 被清事实同帧格式归档（可重放；未配置 = 纯删除） | 实现（TDD） | DC-18；ID-43 | `FileEventStore.cs` | `Prune_archives_removed_facts_before_dropping` + `Prune_without_archive_path_keeps_delete_behavior` | ✅ |
| 2026-08-16 | W47-03 | FactRetentionScheduler 周期 Prune（失败降级续跑）+ 宿主 RetentionPolicy/PruneInterval 接线 | 实现（TDD） | DC-18；ID-43 | `FactRetentionScheduler.cs`、`Hosting/KeystoneHost.cs` | `Scheduler_executes_prune_periodically` + `Scheduler_swallows_prune_failures` + `HostRetentionTests` | ✅ |
| 2026-08-16 | W47-04 | 全量回归 317/317 + Runtime/Hosting AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` × 2 | ✅ |

### 7.48 P48 文档达标 DC-19：IPluginSource/IPluginHost 抽象边界

> 17-doc-compliance-audit DC-19（❌→✅）：无接口，SourceProvider 委托替代。本次落 ADR-0001 决策 1-2 的两个抽象边界（获取端可替换演进 + 运行形态扩展点预留）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W48-01 | IPluginSource 获取端抽象 + LocalPluginSource（多根解析 + {id}/{main} 回退 + 精确报错） | 实现（TDD） | DC-19；ADR-0001 决策 2；ID-44 | `Runtime/Plugins/Loading/` | `LocalSource_fetches_file_relative_to_root` + `Missing_file_fails` + `Search_falls_back` | ✅ |
| 2026-08-16 | W48-02 | IPluginHost 运行形态扩展点预留 + DefaultPluginHost（same-process-alc） | 实现（TDD） | DC-19；ADR-0001 决策 1；ID-44 | 同上 | `DefaultHost_describes_same_process_alc_model` | ✅ |
| 2026-08-16 | W48-03 | 宿主接线：PluginSource/PluginHost 选项；抽象优先于 SourceProvider 委托（向后兼容） | 实现（TDD） | DC-19；ID-44 | `Hosting/KeystoneHostOptions.cs`、`KeystoneHost.cs` | `Host_loads_plugin_via_source_abstraction` + `Source_abstraction_takes_priority_over_delegate` | ✅ |
| 2026-08-16 | W48-04 | 全量回归 323/323 + Runtime/Hosting AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` × 2 | ✅ |

### 7.49 P49 文档达标 DC-14：取消贯穿全链

> 17-doc-compliance-audit DC-14（❌→✅）：取消止于传输层。本次打通调用方 CT → actor → 中间件/handler 全链（06 §1 兑现）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W49-01 | DomainRequest 载 CT（运行态不入信封 DTO；本地按引用传递）+ actor 设置/复位实例 context 槽 | 实现（TDD） | DC-14；06 §1；ID-45 | `Actors/DomainRequest.cs`、`CapabilityActor.cs` | `Middleware_observes_caller_cancellation_token` | ✅ |
| 2026-08-16 | W49-02 | IPluginContext.CancellationToken（沿 context 链向上取——handler 闭包读自身 context 即得） | 实现（TDD） | DC-14；ID-45 | `Context/{IPluginContext,ContextFacade}.cs` | `Cancellation_flows_down_context_chain_to_plugin_handlers` | ✅ |
| 2026-08-16 | W49-03 | 已取消 fail-fast + OperationCanceledException 归一（PipelineCancelled 失败结果，不升级监督重启） | 实现（TDD） | DC-14；ID-45 | `CapabilityActor.cs` | `Already_canceled_request_fails_fast` + `Canceled_request_returns_failed_envelope` + `Middleware_cancellation_exception_yields_failed_envelope` | ✅ |
| 2026-08-16 | W49-04 | 全量回归 328/328 + Runtime AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.50 P50 文档达标 DC-9：文件变更 → 重载 → diff → 逐条目更新

> 17-doc-compliance-audit DC-9（⚠️→✅）：无配置 watcher/diff，热更新退化为 API 调用。本次落 08 §6 触发管线全链——17 审计 30 项差距（P0 5 + P1 4 + P2 11）**全部闭合**。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W50-01 | ConfigDiffer：按条目 id 五路分类（08 §6.1 分级判定 + config 逐键比对） | 实现（TDD） | DC-9；08 §6.1；ID-46 | `Hosting/ConfigDiffer.cs`、`ConfigDiff.cs` | `Diff_only_config_change` + `Diff_name_change` + `No_change_is_noop` | ✅ |
| 2026-08-16 | W50-02 | ApplyConfigAsync 编排（串行化 + 逐条目路由既有动作 + 三事件） | 实现（TDD） | DC-9；08 §6；ID-46 | `Hosting/KeystoneHost.cs` | `Diff_added/removed` + `Disabled_flip_suspends` + 分级路由断言 | ✅ |
| 2026-08-16 | W50-03 | ConfigFileWatcher（防抖合并 + 旁路降级）+ EnableConfigWatch 接线 | 实现（TDD） | DC-9；08 §6.3；ID-46 | `Hosting/ConfigFileWatcher.cs` | `Watcher_triggers_apply_on_file_change` | ✅ |
| 2026-08-16 | W50-04 | 全量回归 334/334 + Hosting AOT 零 IL 警告 | 验收 | 规则 0 | — | `dotnet test` + `PublishAot=true` | ✅ |

### 7.51 P51 代码级对照审计（纯文档轮）

> 17 审计 30 项闭合后的第二轮：不采信文档状态表，双侧源码逐项比对。产出 = 18 参考审计文档 + 11 §3.3 CA 系列登记 + 实现提案集（待人工决策，未动任何生产代码）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W51-01 | Cordis 侧运行面提取（8 核心文件类成员清单 + loader 5 文件 + include 文件管线；含 fiber._reload/_unload/epoch 细读） | 审计 | ID-47 | — | vendor 源码 grep 清单（会话记录） | ✅ |
| 2026-08-16 | W51-02 | Keystone 侧逐项验证（grep/读码核对 18 个疑点：isolate 消费/计时器 Effect/组级联/epoch/ServiceStore 键形/initial 死代码/级别覆盖接线等） | 审计 | ID-47 | — | 每项代码行号证据（18 §2 各表） | ✅ |
| 2026-08-16 | W51-03 | 18 文档（A/B/C 三档 + 每项实现提案：API 形态/落点/TDD 用例/工作量/开放问题 + §5 决策矩阵）+ 11 §3.3 CA 系列登记 + AGENTS 索引 | 文档 | R10 | `docs/architecture/18-cordis-code-parity-audit.md` 等 | frontmatter 校验通过；334/334 保持全绿（本轮零代码改动） | ✅ |

### 7.52 P52 逐项二次研判（纯文档轮）

> P51 审计的 18 项 CA 全量复核（完整读码替代抽样 grep）+ 每项解决方案深化。产出 = 18 文档 v2 研判版（含两处修正）+ 11 §3.3 同步。未动任何生产代码。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W52-01 | CA-9 复核：TimerHandle 全文读码（effect 挂接在构造尾/RunLoop 异常面/DisposeAsync 竞态）→ 判定误报 + 残留 2 加固点 + 修复方案（await runTask / 移除 Cts.Dispose） | 研判 | ID-48 | — | 代码行号证据（18 §2 CA-9） | ✅ |
| 2026-08-16 | W52-02 | CA-1 复核：ContextFacade 服务解析链全文（_services/Resolve/Provide/root 组合语义）→ 缺口收窄 + isolateNames 分支方案（最小档值域隔离/完整档 registry 域感知，含门控缺口分析） | 研判 | ID-48 | — | 同上（18 §2 CA-1） | ✅ |
| 2026-08-16 | W52-03 | 其余 16 项方案深化：CA-10 级联步骤/CA-3 diff 增量回滚面（比 Cordis 重建式更小）/CA-4 移动记账/CA-6 StartFromFileAsync 入口/CA-12 NullLoggerFactory 兜底实证/CA-13 RebindPolicy 等价实现/CA-15 watcher 防回环配套 | 研判 | 18 §2/§3 | `docs/architecture/18-cordis-code-parity-audit.md`（v2 重写） | frontmatter 校验通过；334/334 保持全绿 | ✅ |

### 7.53 P53 决策批判复核（纯文档轮）

> 对 P52 决策的二次批判：CA-1 schema 分叉漏判（回到 loader/config/isolate.ts 全文发现两档域模型）、CA-3 并行门控超时隐患，及 CA-13/12/4/6 的复核注记。产出 = 18 文档 v3（§2 CA-1/3 修正 + §5.1 复核注记表）。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W53-01 | CA-1 复核：读 cordis isolate.ts 全文 → 两档域模型（LocalRealm/GlobalRealm）+ per-entry 应用 + reflect.store symbol 键；对照 Keystone 列表 schema → 判 schema 分叉 | 批判 | ID-49 | `docs/architecture/18-cordis-code-parity-audit.md` | 代码行号证据（isolate.ts:31-79 / EntryParser.cs:92） | ✅ |
| 2026-08-16 | W53-02 | CA-3 复核：Keystone 门控超时（DC-5）→ 组内全并行伪超时隐患 → 拓扑分层方案 | 批判 | ID-49 | 同上 | PluginRuntime.cs:285 证据 | ✅ |
| 2026-08-16 | W53-03 | CA-13/12/4/6 复核注记（RebindPolicy 冗余/默认 provider 先行/YAGNI/源抽象张力） | 批判 | ID-49 | 同上 §5.1 | frontmatter 校验通过；334/334 保持全绿 | ✅ |

### 7.54 P54 隔离语义对齐 Cordis（纯文档轮）

> 按 Cordis 源码裁定默认隔离语义：默认共享（realm=""），隔离靠 isolate 显式声明。修正 01 §4/03 §2.2 的"每实例独立子容器"错误类比 + 18 §2 CA-1 定稿默认域。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W54-01 | 读 Cordis context.ts 构造 + reflect.provide/get 键解析 → 裁定默认共享（root 默认符号回落） | 裁定 | ID-50 | — | context.ts:72 / reflect.ts:290 证据 | ✅ |
| 2026-08-16 | W54-02 | 修正 01 §4"每实例独立子 IServiceProvider"、03 §2.2"整 scope 隔离保留为默认"+边界用例、18 §2 CA-1 默认域=共享 + realm 模型定稿 | 文档 | R10；ID-50 | `01-overview.md`、`03-context.md`、`18-cordis-code-parity-audit.md` | frontmatter 校验通过；334/334 保持全绿 | ✅ |

### 7.55 P55 CA-1 决策收口（纯文档轮）

> CA-1 全部决策裁定：schema 对齐 Cordis map 两档 + 列表 shim + 抽象接缝（值层内存/发现层可交换）+ ServiceRegistry 升格。产出 = 18 §2 CA-1 终稿。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W55-01 | schema 裁定对齐 Cordis map（IsolateSpec 两档 + 列表 shim） | 裁定 | ID-51 | `18-cordis-code-parity-audit.md` | — | ✅ |
| 2026-08-16 | W55-02 | 抽象接缝裁定：值层内存不可分布 / 发现层 IServiceDiscovery 可交换（Steeltoe/Aspire 先例佐证）+ ServiceRegistry 升格修正 | 裁定 | ID-51 | 同上 | — | ✅ |
| 2026-08-16 | W55-03 | 18 §2 CA-1 终稿（四步方案 + 决策矩阵更新）+ 11 register 同步 | 文档 | R10 | 18/11 | frontmatter 校验通过；334/334 保持全绿 | ✅ |

### 7.56 P56 CA-1 可行性复核（纯文档轮）

> 代码事实验证可行性 + 接口深度优化。产出 = 18 §2 CA-1 第 1/2 步收窄 + 实施序细化。

| 日期 | 编号 | 工作项 | 类型 | 决策引用 | 实现落点 | 验收凭证 | 结果 |
|------|------|--------|------|---------|---------|---------|------|
| 2026-08-16 | W56-01 | 验证现状 `ServiceRegistry` **锁内发事件**（ServiceRegistry.cs:54/73）——现状靠 Monitor 同线程重入侥幸成立，跨线程/阻塞回调即死锁；KeyedServiceStore 出锁通知是修复非装饰 | 验证 | ID-52 | — | 源码行号证据 | ✅ |
| 2026-08-16 | W56-02 | 发现接口收窄：删 Register*/Unregister*（写生命周期已由 effect-disposer 覆盖，重复暴露=浅接口），留 `IsAvailable + Subscribe(+AvailableServices)`；同步契约：本地同步读，网络在 adapter 后台 | 裁定 | ID-52 | 18 §2 第 2 步 | — | ✅ |
| 2026-08-16 | W56-03 | 批量通知（对齐 Cordis `notify(names[])`）+ 实施序细化（schema+shim 零运行时涟漪先行独立提交） | 裁定 | ID-52 | 18 §2 第 1 步/实施序 | — | ✅ |
| 2026-08-16 | W56-04 | 涟漪盘点：测试 19 refs/10 文件；EntryTree 合并触点（EntryTree.cs:55 `Isolate.Count>0` 三元）确认；AGENTS 状态行清欠（P54/P55 漏更） | 盘点 | R10 | AGENTS.md | frontmatter 校验通过；334/334 保持全绿 | ✅ |

### 7.57 P57 CA-1 实施（TDD 依次执行）

> 目标：按 18 §2 CA-1 终稿（P54-P56 全裁定）落地 isolate 服务隔离。任务 T1-T6，每任务 TDD 红→绿→重构 + 全量回归 + AOT 冒烟 + 文档回写 + 独立提交，完成后对照验收标准逐项验收。

| 任务 | 目标（严禁简化） | 影响范围 | 验收标准 | 状态 |
|------|----------------|---------|---------|------|
| T1 schema 对齐 | isolate 改 Dict<name→true\|"label"> 两档（IsolateKind 三态含 None=显式解除）+ 列表 shim + 序列化 map 回写 + 分层按名合并（None 移除）+ ConfigDiffer 结构键档位编码 + fail-fast | Keystone.Config（EntryOptions/EntryParser/EntrySerializer/EntryTree）+ Keystone.Hosting（ConfigDiffer）+ 08 §3 文档 | 11 个新测试红→绿（map 两档/shim/None/非法形态 fail-fast/roundtrip/按名合并+false 移除/Shared 工厂校验）；全量回归全绿；AOT 0 IL；08 §3 更新；独立提交 | ✔ 2026-08-16 |
| T2 KeyedServiceStore | ConcurrentDictionary<(name,realm),(value,ownerId)> + Lock 复合写（属主校验+写）+ **出锁批量通知**（scope 合并）+ IsAvailable=ContainsKey + Provide 返回删键 disposer | Keystone.Runtime/Context 新增组件（纯新增不接线） | 17 个新测试红→绿（跨属主拒绝/同属主重绑/删键属主校验/disposer 幂等/跨线程回调不死锁/单键直发/scope 合并/嵌套并入/remove 并入 scope/按域分区/退订停投/16 线程竞写恰一胜者）；全量 362 绿；Runtime AOT 0 IL；提交 | ✔ 2026-08-16 |
| T3 ContextFacade 接线 | facade 持共享 root store（独立 root 自持）；realm 沿链推导（isolate map 子继承父/子影子覆盖/均无→""）；Resolve 算 realm 查共享 store；Provide 带 realm+disposer 追踪；RemoveOwnedServices 逐 disposer 幂等清理 | ContextFacade/IContext/删 ServiceStore+IServiceStore+ServiceStoreTests + 2 测试调用点 | 14 新测试红→绿（兄弟可见/属主冲突/重绑/幂等清理/私有对无 map 隐藏/双私有隔离/同 label 互见/链继承/子影子覆盖/按名隔离不连坐/同 realm 冲突/GetLazy/独立 root 自持/GatingServiceNotFound）；全量 371 绿；AOT；提交 | ✔ 2026-08-16 |
| T4 发现投影+门控统一 | IServiceRegistry→IServiceDiscovery 只读投影（IsAvailable(name,realm)+Subscribe+AvailableServices）；PluginRuntime 删双注册；门控带 realm；init 后校验 provides⊆owned | ServiceRegistry/IServiceRegistry/PluginRuntime/PluginLoader/KeystoneHost + Runtime/Hosting 测试 | 门控/依赖恢复（G-C2）/DC-5 诊断全绿；provides 未 Provide → 明确 FAILED；全绿；AOT；提交 | ✔ 2026-08-16 |
| T5 宿主端到端 | 三 context 工厂按 entry.Isolate 算 realm；组谱系 #groupId 推导；isolate 变更触发依赖方重载（F10） | KeystoneHost + ConfigDiffer + IsolateMapResolver(Config) + PluginLoader + Hosting.Tests | e2e：同 label 共享/私有隔离/默认共享；配置改 isolate → 受影响条目重载；全绿；AOT；提交 | ✔ 2026-08-16 |
| T6 总验收 | 全量回归 + 六工程 AOT + 文档回写（02/03/08/09/10/11/14/18/AGENTS）+ CA-1 标记已实施 | 全仓 | 345+N 全绿；AOT 全零 IL；文档同步；frontmatter；最终提交 | ✔ 2026-08-16 |

#### T3 执行记录（2026-08-16）

| 编号 | 工作项 | 类型 | 验收凭证 | 结果 |
|------|--------|------|---------|------|
| W57-T3-01 | 红测试 14 个（ContextFacadeIsolateTests：组合语义保持 + realm 全语义矩阵） | TDD | 构译期红（构造参数/isolateMap 不存在） | ✅ |
| W57-T3-02 | ContextFacade 改接：`_store`（链上共享/独立自持）+ `_isolateMap`（构造注入）+ `ResolveRealm`（链上首个含名 map，均无→""）+ `Resolve` 单键查 + `Provide` 按 realm 写并追踪 disposer + `RemoveOwnedServices` 逐 disposer 幂等；IContext.Services → KeyedServiceStore | 实现 | — | ✅ |
| W57-T3-03 | 删除旧 ServiceStore/IServiceStore/ServiceStoreTests（值层单源化；覆盖面已由 KeyedServiceStoreTests 承接）；2 处测试调用点补 realm 参数；xUnit1031 改异步 | 清理 | — | ✅ |
| W57-T3-04 | 全量回归 371/371（Runtime 160→169：+14 新 −5 删）；AOT Runtime 零 IL；独立提交 | 验收 | dotnet test 6 套件 Passed；publish grep 0 | ✅ |

#### T4 执行记录（2026-08-16）

| # | 内容 | 方式 | 验证 | 状态 |
|---|------|------|------|------|
| W57-T4-01 | 红测试 6 个（DiscoveryGatingTests：值即注册激活/声明未兑现 FAILED 点名/兑现激活/门控域过滤/值消失卸载+值回重启/投影批量通知） | TDD | 构译期红（IServiceDiscovery/isolateMap 不存在，20 错） | ✅ |
| W57-T4-02 | IServiceDiscovery（2+1 成员 seam）+ InMemoryServiceDiscovery（直投 KeyedServiceStore，零冗余状态）；删 ServiceRegistry/IServiceRegistry/EventArgs | 实现 | — | ✅ |
| W57-T4-03 | PluginRuntime：删 init 后 Register 循环与 stop 时 Unregister 循环（值即注册）；订阅改批量变更键+命中重评（不信任投递时刻快照）；DependenciesSatisfied/超时诊断带 Realm()；provides⊆owned 聚合校验（缺失全列点名）；ctor 增 isolateMap（门控先于 context 创建） | 实现 | — | ✅ |
| W57-T4-04 | PluginLoader.CreateAsync/KeystoneHost 接线：host 投影 root store 单例 discovery；17 处测试直构点改投影模式 | 实现 | — | ✅ |
| W57-T4-05 | scope 冲刷改集合语义（Distinct，对齐 Cordis notify(names[])）——同键增删并入只投一次 | 修复 | 投影批量测试 3→2 项 | ✅ |
| W57-T4-06 | 修两处既有 flake（暴露于本任务时序变化）：① ConfigInjectionTests 跨 ALC 同名类型改每测试唯一名；② HotReloadTests 跨 ALC 读取改全副本扫描（int 取最大/string Any 断言）——GetAssemblies() 跨 LoadContext 无"最新在后"保证 | 修复 | 各自连续 5-10 次复跑零失败 | ✅ |
| W57-T4-07 | 全量回归 372/372（Runtime 169→172：+6 新 −3 删；Hosting 54→52：−2 registry 单测，覆盖面由 KeyedServiceStoreTests/投影测试承接）；Runtime+Hosting AOT 零 IL；独立提交 | 验收 | dotnet test 6 套件 Passed；publish grep 0 | ✅ |

#### T5 执行记录（2026-08-16）

| # | 内容 | 方式 | 验证 | 状态 |
|---|------|------|------|------|
| W57-T5-01 | 红测试 5 个（IsolateEndToEndTests：组私有域双向路由/@label 跨组路由+异 label 不串/叶自声明独占+不泄漏/F10 组声明移除域迁移端到端/F10 label 整体迁移不悬死） | TDD | 初版 5/5 失败（isolate 未接线，全部经 "" 互见） | ✅ |
| W57-T5-02 | IsolateMapResolver（Keystone.Config）：entry.Isolate → name→realm（Private→#声明处Id 组声明=组内共享/叶自声明=独占；Shared→@label；None→移除解除继承）；谱系外→内累积，子影子覆盖父（对齐 context.isolate() 原型链 shadow） | 实现 | Cordis 源码对照：reflect.provide `store[key=ctx[isolate][name]]`、notify 按域 filter、resolve 同键路由——三方一致 | ✅ |
| W57-T5-03 | ConfigDiffer 结构键改生效 realm（EffectiveEntry 谱系解析）——组级 isolate 声明变化会改变叶子生效键 → 叶子冷重启（F10 组谱系传播） | 实现 | 域迁移测试断言 rm_p/rm_c 重载、rm_fresh 不重载 | ✅ |
| W57-T5-04 | KeystoneHost：BuildIsolateMap（根→目标谱系链解析）+ FindEntryPath；三工厂（LoadEntry/Reload/Mount）同 map 注入 context 工厂与 PluginLoader（门控域==解析域）；ApplyStructuralChangesAsync 两阶段（先整体替换树再逐叶冷重启——组声明先落位，叶子重载读到新谱系） | 实现 | — | ✅ |
| W57-T5-05 | 测试证明手法迭代（2 版）：初版"消极挂起断言"踩宿主加载语义（LoadSourceAsync await 终态，门控不满足阻塞 30s）+ 类型名跨测试并行撞 ALC；终版改值路由证明（各域放可区分值按解析结果断言，全积极断言不阻塞）+ 类型名全套件唯一 | 修复 | 5/5 绿；Hosting 57 测试复跑 3 次零失败 | ✅ |
| W57-T5-06 | 全量回归 377/377（Hosting 52→57：+5）；Config+Hosting AOT 零 IL；独立提交 | 验收 | dotnet test 6 套件 Passed；publish grep 0 | ✅ |

#### T6 执行记录（2026-08-16）

| # | 内容 | 方式 | 验证 | 状态 |
|---|------|------|------|------|
| W57-T6-01 | 全量回归 377/377（Core 30 / AI 19 / Config 69 / Sdk 30 / Hosting 57 / Runtime 172） | 验收 | dotnet test 6 套件 Passed | ✅ |
| W57-T6-02 | 六工程 AOT 冒烟全零 IL 警告（Core/Config/Runtime/Hosting/Sdk/AI） | 验收 | publish -p:PublishAot=true grep 0 | ✅ |
| W57-T6-03 | 文档回写：02 §3 键控服务改自建 KeyedServiceStore 实态（ID-50 修正：弃 MS.DI per-scope 类比；值生命周期替代子容器）；03 §2.1 实现备注更新 (name,realm) 键控 + §2.2 F10 标 P57-T5；09 启动流步骤 6 带生效 realm；10 接口注释带 realm 语义与 provides 兑现契约；11 G7 → 已实施；18 CA-1 标题标 ✅ 已实施 | 文档 | frontmatter 校验通过 | ✅ |
| W57-T6-04 | AGENTS 状态行收口（CA-1 实施完成）+ 最终提交 | 文档 | — | ✅ |

### 7.58 P58 CA-10 组条目 CRUD 级联（TDD）

> 目标：按 18 §2 CA-10 提案修唯一 P0 正确性——删组孤儿泄漏 + 建组空壳。每步 TDD 红→绿 + 全量回归 + AOT 冒烟 + 文档回写 + 独立提交。

| 任务 | 目标（严禁简化） | 影响范围 | 验收标准 | 状态 |
|------|----------------|---------|---------|------|
| T1 级联实现 | RemoveEntryAsync(组id) 逆序逐叶级联卸载（修孤儿）+ CreateEntryAsync 组路径逐叶加载（修空壳，挂起继承 DC-16）+ 抽 DisposeHostedAsync + MoveEntryAsync 差异注明 | KeystoneHost（RemoveEntryAsync/CreateEntryAsync/新 DisposeHostedAsync）+ Hosting.Tests | 5 新测试红→绿（组删级联+逆序序+树无残留/叶删不连坐/带子组建组逐叶 Active/挂起组不加载子叶/组移动纯树零重载）；全量回归；AOT；提交 | ✔ 2026-08-16 |

#### T1 执行记录（2026-08-16）

| # | 内容 | 方式 | 验证 | 状态 |
|---|------|------|------|------|
| W58-T1-01 | 红测试 5 个（GroupCrudCascadeTests）：初红 2/5（组删孤儿 + 建组空壳两核心缺陷），3 保护项（叶删不连坐/挂起组跳过/组移动纯树）先绿锁定既有语义 | TDD | filter 跑 2 Failed | ✅ |
| W58-T1-02 | CreateEntryAsync 组路径 EnumerateActiveLeaves([entry]) 逐叶 LoadEntryAsync（含组自身 disabled 检查 → 挂起组整树不加载；失败隔离语义沿用）；RemoveEntryAsync：树内组 → EnumerateLeaves 逆序逐叶 DisposeHostedAsync；抽 DisposeHostedAsync（EntryDisposing → loader.DisposeAsync → 移除托管）；MoveEntryAsync 注明与 Cordis 差异（纯树操作，fiber 不重挂） | 实现 | 5/5 绿 | ✅ |
| W58-T1-03 | 修复引入的语义回归：MountAsync（H2 编程式挂载）不进树——RemoveEntryAsync 初版 FindEntry 空检查误伤树外托管插件卸载（MountAsync_programmatic 测试红）；改宽容语义（仅树内组走级联解析，树外直接卸载） | 修复 | MountAsync 测试恢复绿 | ✅ |
| W58-T1-04 | 全量回归 382/382（Hosting 57→62：+5）；Hosting AOT 零 IL；独立提交 | 验收 | dotnet test 6 套件 Passed；publish grep 0 | ✅ |

### 7.59 P59 CA-3 组级事务 + CA-4 组合 update（TDD）

> 目标：按 18 §2 提案实施 P1 两项。TDD 红→绿 + 全量回归 + AOT 冒烟 + 文档回写 + 独立提交。

| 任务 | 目标（严禁简化） | 影响范围 | 验收标准 | 状态 |
|------|----------------|---------|---------|------|
| T1 CA-3 事务化 | ApplyConfigAsync 事务化：逐条目失败收集（单错抛因/多错 AggregateException）+ 逆序回滚（Added→Remove/Config→旧值/Structural→旧条目+Reload/Flip→翻回）+ 回滚失败聚合 + ThrowIfShuttingDown 每步 | KeystoneHost（ApplyDiffTransactionallyAsync + CollectPerItemAsync/CollectStepAsync + 阶段 appliers） | 4 新测试（失败回滚新增兄弟/双失败聚合/正向全载/回滚 config 旧值） | ✔ 2026-08-16 |
| T2 CA-4 组合 update | UpdateEntryAsync(id, options, parent, position)：结构键+parent 判定热/冷路径；移动记账 (源父, 原下标) 失败回插精确原位（修 MoveEntry 回滚只回根） | KeystoneHost（UpdateEntryAsync/ApplyEntryUpdateAsync/RestoreEntry/LocateEntry） | 4 新测试（移动+config 一步/纯 config 热路径/结构变冷路径/失败回原下标） | ✔ 2026-08-16 |

#### T1-T2 执行记录（2026-08-16）

| # | 内容 | 方式 | 验证 | 状态 |
|---|------|------|------|------|
| W59-01 | 红测试 8 个：GroupTransactionTests 4（初红：回滚缺失 → 半应用树）+ UpdateEntryTests 4（初红：API 不存在，构译期） | TDD | filter 跑 Failed | ✅ |
| W59-02 | CA-3 实现：ApplyDiffTransactionallyAsync（oldEntries 回滚素材 + 五阶段）+ CollectPerItemAsync（逐条目 allSettled 语义——一条失败不阻断同批其余）+ CollectStepAsync（结构变阶段级）+ RollbackAsync（逆序 + 聚合 + CA1031 pragma 豁免）；每步 ThrowIfShuttingDown | 实现 | 8/8 绿 | ✅ |
| W59-03 | CA-4 实现：UpdateEntryAsync + ApplyEntryUpdateAsync（冷路径直接重载/热路径 PatchContext 瀑布）+ RestoreEntry（moved→移回原位再回旧值）+ LocateEntry((父, 下标) 定位)；树 helpers 签名统一 | 实现 | — | ✅ |
| W59-04 | 中途修正：初版阶段级收集致同批第二失败被吞（AggregateException 只剩 1 因）——改逐条目粒度收集（对齐 Cordis allSettled）；测试自身 bug：同 id 坏源×2 在 YAML 解析期被拦（fail-fast 先于应用层）→ 改双 id | 修复 | 双失败聚合 2 内因 | ✅ |
| W59-05 | 全量回归 390/390（Hosting 62→70：+8）；Hosting AOT 零 IL；独立提交 | 验收 | dotnet test 6 套件 Passed；publish grep 0 | ✅ |

### 7.60 P60 CA-6 initial 引导 + CA-12 服务级选项（TDD）

> 目标：按 18 §2 提案实施 P1 收尾两项。TDD 红→绿 + 全量回归 + AOT 冒烟 + 文档回写 + 独立提交。

| 任务 | 目标（严禁简化） | 影响范围 | 验收标准 | 状态 |
|------|----------------|---------|---------|------|
| T1 CA-6 initial 接线 | InitialEntries 选项 + StartFromFileAsync()：无文件+initial → EnsureInitialAsync 写入再启动；文件存在 → 忽略；皆无 → 报错 | KeystoneHostOptions + KeystoneHost（StartFromFileAsync） | 4 新测试（写入并启动/已存在不覆盖/皆无报错/无路径报错） | ✔ 2026-08-16 |
| T2 CA-12 服务级选项 | ServiceOptions（服务名→选项字典）+ 日志首例接线：未注入 LoggerFactory 且 ServiceOptions["logger"] → RingBufferLoggerProvider（capacity/defaultLevel/levels）替代 NullLogger 兜底；显式 LoggerFactory 优先；自建 factory Shutdown 释放 | KeystoneHostOptions + KeystoneHost（BuildServiceLoggerFactory/RingBufferLogs 诊断面/DisposeOwnedLoggerFactory） | 4 新测试（levels 过滤/defaultLevel/显式工厂优先/无选项现状保持） | ✔ 2026-08-16 |

#### T1-T2 执行记录（2026-08-16）

| # | 内容 | 方式 | 验证 | 状态 |
|---|------|------|------|------|
| W60-01 | 红测试 8 个：InitialBootstrapTests 4（初红：InitialEntries/StartFromFileAsync 不存在）+ ServiceOptionsLoggerTests 4（初红：ServiceOptions/RingBufferLogs 不存在） | TDD | 构译期红 24 错 | ✅ |
| W60-02 | CA-6 实现：StartFromFileAsync（ConfigFilePath 必配；EnsureInitialAsync 复用 Config 层既有死代码——本接线后激活）；CA-12 实现：BuildServiceLoggerFactory（capacity/defaultLevel/levels 解析 + RingBufferLoggerProvider 构造）+ RingBufferLogs 诊断属性 + DisposeOwnedLoggerFactory（CA2000 生命周期闭环） | 实现 | 8/8 绿 | ✅ |
| W60-03 | 测试侧修正：插件源补 using Microsoft.Extensions.Logging（LogDebug/LogError 扩展方法）；多余 using 清理 | 修复 | — | ✅ |
| W60-04 | 全量回归 398/398（Hosting 70→78：+8）；Hosting AOT 零 IL；10 §4 服务消费定式写入；独立提交 | 验收 | dotnet test 6 套件 Passed；publish grep 0 | ✅ |

### 7.61 P61 P2 批四项：CA-9 计时器竞态加固 + CA-7 readonly 降级 + CA-15 noSave + CA-5 运行期 patch（TDD）

| 任务 | 目标（严禁简化） | 影响范围 | 验收标准 | 状态 |
|------|----------------|---------|---------|------|
| T1 CA-9 竞态加固 | effect disposer 改 async：Cancel 后 await _runTask（收敛在途回调）；DisposeAsync 移除 _cts.Dispose()（消 ObjectDisposedException 竞态源头）；RunLoop catch 扩 ObjectDisposedException 兜底 | Sdk TimerExtensions | quiesce 后在途回调已完成（慢回调 TCS 断言）；无未观察任务异常（TaskScheduler 探针） | ✔ 2026-08-16 |
| T2 CA-7 readonly 降级 | ConfigFileWriter：IsReadOnly 状态 + OnReadOnly 回调（一次性）；拒绝访问（UnauthorizedAccessException/0x80070005）→ 置位降级（区别 0x80070020 占用重试）；readonly 后 Write/Schedule/Flush 静默跳过 | Config Persistence | 0x80070005 → 第二次写不抛 + 回调恰一次；0x80070020 → 仍重试成功不降级 | ✔ 2026-08-16 |
| T3 CA-15 noSave | UpdatePluginAsync(id, config, save: bool = true)；ApplyConfigAsync(newTree, save = true) 贯通事务（_suppressWriteBack 抑制子操作写回 + finally 解除 + 回滚同样抑制）；watcher 回调 save: false 防回环 | Hosting | watcher apply 后文件保持新值（未被写回重写）；noSave 内存树更新但不落盘 | ✔ 2026-08-16 |
| T4 CA-5 运行期 patch | Config 层纯函数 EntryPatcher.Apply（插入组/根 + 按 id 覆盖非 null 字段 + name 不匹配跳过 onWarn + 空 patches 恒等）；KeystoneHostOptions.ConfigPatches → StartAsync 解析后 manifest 校验前应用 | Config Entries（新 EntryPatch/EntryPatcher）+ Hosting（ApplyConfigPatches） | 5 Config 测试 + 2 宿主测试（插入参与校验加载/覆盖 config 生效） | ✔ 2026-08-16 |

#### 执行记录（2026-08-16）

| # | 内容 | 方式 | 验证 | 状态 |
|---|------|------|------|------|
| W61-01 | 红测试 11 个：CA-9×2（在途回调/未观察异常）/ CA-7×2（拒绝降级/占用重试）/ CA-15×2（watcher 不回写/noSave 不落盘）/ CA-5×5（插入根/插入组/覆盖合并/不匹配警告/恒等）+ 宿主×2 | TDD | 构译期/断言红 | ✅ |
| W61-02 | 实现：TimerHandle 保存 _runTask + async disposer await + 去 _cts.Dispose；ConfigFileWriter readonly 状态机（Volatile 读写 + IsAccessDenied/IsSharingViolation 分叉）；save 参数贯通（UpdatePluginAsync/ApplyConfigAsync/ApplyConfigEntryAsync + _suppressWriteBack try/finally）；EntryPatch/EntryPatcher + ApplyConfigPatches | 实现 | 11/11 绿 | ✅ |
| W61-03 | 语义修正：CA-5 覆盖为条目级浅合并（config 整字段替换——对齐 Cordis entry patch；测试初版深合并期望改浅）；CA-7 回调命名 OnReadOnly（审计措辞） | 修复 | — | ✅ |
| W61-04 | 全量回归 411/411 + Hosting AOT 零 IL（P62 前）；独立提交 | 验收 | dotnet test 6 套件 Passed；publish grep 0 | ✅ |

### 7.62 P62 CA-2 插件源文件 watcher（TDD）

| 任务 | 目标（严禁简化） | 影响范围 | 验收标准 | 状态 |
|------|----------------|---------|---------|------|
| T1 CA-2 | PluginFileWatcher（复用 ConfigFileWatcher 防抖模式，IncludeSubdirectories 覆盖 {root}/{id}/{main} 布局）；EnablePluginWatch()（opt-in，与 EnableConfigWatch 对称）；变更文件按 manifest.Main 匹配 active 条目 → PluginReloading + ReloadPluginAsync；ReloadPluginAsync 改走 PluginSource.FetchAsync（冷重启重取源——原先走静态 SourceProvider 读不到新代码） | Hosting（新 PluginFileWatcher + EnablePluginWatch + OnPluginSourceChangedAsync）+ Runtime（LocalPluginSource.Roots 暴露） | 文件改写 → 重载事件 + 状态仍 Active（热替换非失败）；无匹配文件变更 = 无操作 | ✔ 2026-08-16 |

#### 执行记录（2026-08-16）

| # | 内容 | 方式 | 验证 | 状态 |
|---|------|------|------|------|
| W62-01 | 红测试 2 个：变更触发重载（PluginReloading 事件）/ 无匹配变更无操作 | TDD | EnablePluginWatch 不存在构译红 | ✅ |
| W62-02 | 实现 + 两处根因修复：① ReloadPluginAsync 取源走 PluginSource.FetchAsync（对齐 LoadEntryAsync——静态 SourceProvider 是旧代码副本）；② watcher 直调 ReloadPluginAsync 无事件（事件只在包装路径发）→ 回调先发 PluginReloading | 实现 | 事件触发 + Active | ✅ |
| W62-03 | 连带真 bug：watcher 线程重载 vs 插件线程 Provide 并发 → RemoveOwnedServices 枚举崩溃（Collection was modified）——ContextFacade._provides 加锁 + 快照迭代 | 修复 | 重载链不再崩 | ✅ |
| W62-04 | 全量回归 413/413（398→413：P61 +13/P62 +2）+ Hosting AOT 零 IL；独立提交 | 验收 | dotnet test 6 套件 Passed；publish grep 0 | ✅ |

### 7.63 P63 CA 审计收尾：ADR-0016 + 三处状态回写（无代码变更）

> 目标：18 审计全部实施后的文档收口——11 §3.3 仍标"待决策"（实施后未回写）、12 缺 CA-14/16/18 注记、CA-17 观察项未挂、CA-8/11 无人裁定。

| 任务 | 目标 | 影响范围 | 验收标准 | 状态 |
|------|------|---------|---------|------|
| T1 ADR-0016 | CA-8 JSON 格式弃用决策落 ADR（人工裁定：仅弃用 CA-8；CA-11 保留扩展点） | decisions/（新 adr-0016） | ADR 完整 + frontmatter 过 | ✔ 2026-08-16 |
| T2 11 §3.3 回写 | 18 项状态 → 实际态（11 ✅ + CA-8 弃用 + CA-11 ⏸ + CA-13 ⏸ + CA-14/16/17/18 ✅）；§4 叙述同步 | 11-gap-register | 每行与 14 log/18 一致 | ✔ 2026-08-16 |
| T3 12 §11.1 注记 | CA-14/16/18 接受差异注记（等价面 + 理由防复查误判） | 12-cordis-semantics-mapping | 新 §11.1 表三项 | ✔ 2026-08-16 |
| T4 CA-17 观察项 | 写队列自旋等待挂观察（触发条件 + Channel 备选） | 11 §4.1 | 观察表落位 | ✔ 2026-08-16 |

#### 执行记录（2026-08-16）

| # | 内容 | 方式 | 验证 | 状态 |
|---|------|------|------|------|
| W63-01 | ADR-0016（YAML-only 收敛：插值互斥/矩阵/YAML 超集/IConfigProvider 后门；CA-6/7 文件后端张力圈定非本 ADR 范围）+ decisions/README 索引 | 文档 | frontmatter 校验过 | ✅ |
| W63-02 | 三处回写（11 §3.3+§4+§4.1、12 §11.1、18 §5 决策表） | 文档 | 交叉一致 | ✅ |
| W63-03 | 无代码变更——回归维持 413/413（P62 已验证）；frontmatter + 独立提交 | 验收 | validate_frontmatter | ✅ |

### 7.64 P64 前置：第二轮全量等价性复核审计（19 号文档）

> 目标：P57-P63 十一项实施后，对 Cordis 全源重新全量对照——验证既判项实际完成度 + 扫描漏网。6 路并行深审（SV 服务值层 / CF 上下文生命周期 / EV 事件 / LG 日志 / LD 加载器配置树 / IN 文件管线）+ 第 7 路表面自查（utils 导出/cosmokit/bin.js/README 生态包）。

| 任务 | 目标 | 产出 | 状态 |
|------|------|------|------|
| T1 全源枚举 | Cordis 核心 9 文件（2693 行）+ plugin-include（377）+ plugin-loader（~1000）+ cosmokit/bin.js 逐文件行为点枚举 | 108 项发现（双侧 文件:行号） | ✔ 2026-08-16 |
| T2 对抗验证 | P57 CA-1 / P58 CA-10 / P59 CA-3/4 / P60 CA-12 / P61 / P62 近期实现的正确性复查（找未测路径缺陷） | 两档域 schema 等 verified ✓；但发现 P0-1/2/3 落在 CA-3 未测路径、P0-7 落在 CA-9 未覆盖模式 | ✔ 2026-08-16 |
| T3 汇总落档 | 19 号文档（P0 七项/P1 七项/D 九项/P2 卅一项/verified 凭证/决策矩阵）+ 11 §3.4 登记 + AGENTS 索引 | docs/architecture/19-second-parity-verification-audit.md | ✔ 2026-08-16 |
| T4 总裁定落档 | 人工裁定：全部语义分歧**按 Cordis 方式**（D-1 真热更新实施/D-6 Provide 报错式+Set/D-8 事件广播/P2-13 PENDING+FAILED re-arm/EntryGroup 删除等）——19 §8 裁定表 + §9 修订批次 P64-P69 | 文档 | frontmatter 过 | ✔ 2026-08-16 |

#### W64-01 P64：事务与生命周期正确性批（19 §1/§3：P0-1/2/3 + P0-6 + P0-7 + D-4 + D-5 + D-9）

| 任务 | 目标 | 影响范围 | 验收 | 状态 |
|------|------|---------|------|------|
| T1 事务三连（P0-1/2/3） | diff Added 携带谱系（AddedEntry(Entry, ParentId, Position) + ConfigDiffer.Locate）→ 子叶进组；Added 去重（父组 Create 已连带加载的子跳过——新组+子不再 duplicate 必败）；结构步逐条目"替换树→即刻登记 undo→重载"（中途失败也复原已替换树）；Added 失败清树（CreateEntryAsync 先插树后加载，编译失败撤树） | ConfigDiff.cs/ConfigDiffer.cs/KeystoneHost.cs（AddedEntry.cs 新建） | TransactionGroupingTests 5 例（红→绿）；GroupTransactionTests 原用例暴露 bad 进组的半应用残留 → 失败清树修复 | ✔ |
| T2 Removed 回滚（D-5） | ApplyRemovedStageAsync：删除前捕获（原条目, 父 id, 下标），删除后登记复合 undo——组先于子按声明序整体重建+重载，已随组恢复的子跳过（对齐 group.ts:95-101 全量重建；P59 注记作废） | KeystoneHost.cs | Removed_entries_are_restored_on_failure | ✔ |
| T3 失败复原运行时（D-4） | UpdateEntryAsync torn 标记（即将"先卸旧"时置位）→ catch 先 RestoreEntry 再 RestoreRuntimeAsync（旧条目重启，尽力而为吞复原异常——对齐 entry.ts:232-243） | KeystoneHost.cs | UpdateEntry_failure_restores_plugin_runtime | ✔ |
| T4 订阅回收（P0-6） | ContextFacade 五个 Subscribe* 经 TrackSubscription 挂 effect（"event-subscription"）——quiesce 自动退订，handler 不再钉死 ALC（对齐 events.ts:254-259 监听器即 fiber effect）；CA2000 注记（句柄刻意丢弃——Dispose 语义已变为执行 disposer） | ContextFacade.cs | SubscriptionLifecycleTests 4 例 | ✔ |
| T5 计时器收口（P0-7） | TimerHandle：effect disposer 改为 DisposeAsync()（三合一：取消+等在途、弃置已武装 debounce Timer、置 _disposed）；throttle/debounce fire 经 RunFireTracked 挂 _runTask WhenAll 链（持锁与 DisposeAsync 读取互斥）——quiesce 等在途回调、卸载后不再触发 | TimerExtensions.cs | TimerQuiesceHardeningTests 3 例 | ✔ |
| T6 Effect 句柄 Dispose=执行（D-9） | EffectNode.Disposed→Interlocked TryMarkDisposed（与 DisposeAllAsync 并发恰一次）；Registration.Dispose 执行 disposer（GetAwaiter().GetResult()）——`using var h = ctx.Effect(cleanup)` 惯例成立（对齐 fiber.ts:427-442） | EffectRegistry.cs | SubscriptionLifecycleTests 3 例（执行/幂等/与 DisposeAll 恰一次） | ✔ |
| T7 回归 | 全量 6 套件 | 新增 12 测试 | 425/425（4 轮 + 5 轮复跑全绿）；Hosting AOT 零 IL 警告 | ✔ |
| T8 测试加固 | PluginFileWatchTests 收尾改轮询 + 容忍 reload 瞬态窗口（旧已卸新未挂）；HostRetentionTests 停后静止双采样（在途回调落地窗口） | 两测试文件 | 5 轮全量无抖动 | ✔ |

#### W65-01 P65：配置管线批（19 §1/§2/§4：P0-4 + P0-5 + P1-6 + P1-7 + P2-1 + P2-2 + P2-9）

| 任务 | 目标 | 影响范围 | 验收 | 状态 |
|------|------|---------|------|------|
| T1 watcher 同管线（P0-4/P2-1） | EnableConfigWatch 回调补 BuildInterpolator + ApplyConfigPatches——与 StartAsync 完全同管线（对齐 Cordis 每次 _apply 重跑 patch/插值；修复前裸 Parse → !!env 字面注入 + patch 覆盖被文件回退） | KeystoneHost.cs | Watcher_replays_interpolation/config_patches 2 例（红→绿） | ✔ |
| T2 写串行化（P0-5） | ConfigFileWriter 增 _writeGate SemaphoreSlim(1,1)——WriteCoreAsync 全程串行（Timer 防抖 Flush 与显式 Flush/Write 并发不再竞写同一 .tmp；对齐 Cordis writeQueue 链式单消费） | ConfigFileWriter.cs | WriteSerializationTests 2 例（门闩式确定性验证：第二写在第一写完成前不得进入原子步） | ✔ |
| T3 disabled 级联（P1-6） | SetEntryDisabledAsync 组翻转级联子叶（disable → EnumerateLeaves 全卸；enable → EnumerateActiveLeaves 恢复）+ IsAnyAncestorDisabled 祖先检查（disabled 组内叶单独 re-enable 不再绕过直载；对齐 entry.ts:88-98 + group.ts:108-112） | KeystoneHost.cs | Group_disable_runtime_flip_cascades + Re_enable_inside_disabled_group 2 例 | ✔ |
| T4 形状/归属结构键（P1-7） | ConfigDiffer.StructuralKey 入 ParentId + IsGroup 形状 + Flatten 携带父 id；StructurallyChanged → StructuralChange(Entry, ParentId, Position)；应用侧"移除旧位 + 新谱系落位"（跨组移动检出+应用，叶↔组转换检出）；结构阶段先于 Added（父先落位）；组结构变加载未托管新子叶 | ConfigDiffer.cs/ConfigDiff.cs/StructuralChange.cs（新建）/KeystoneHost.cs | Leaf_to_group_conversion_loads_children + Diff_applies_move_between_groups 2 例 | ✔ |
| T5 EntryPatcher 对齐（P2-2） | a) 恒 detached（CloneEntry 结构克隆——组递归/字典拷贝；对齐 structuredClone）；c) insert 与 overrides 互斥（对齐 include insert 分支 continue）；b) bool? Disabled false 清除语义核实已正确（`??` 对 false 不短路）——Config null 清除受 C# 类型系统限制（null=未提供），注记 12 §11.1 | EntryPatcher.cs | EntryPatcherAlignmentTests 3 例 + Empty_patches 语义更新（同引用恒等 → 内容恒等 detached） | ✔ |
| T6 EntryGroup 死代码删除（P2-9） | standalone EntryGroup.cs + EntryGroupTests 删除（其回滚语义与宿主实现相反——双实现漂移风险；宿主 ApplyDiffTransactionallyAsync 为唯一真源） | -2 文件 | 构建/全量绿 | ✔ |
| T7 回归 | 全量 6 套件 | 新增 11 测试（净 +10：删 EntryGroupTests 5） | 431/431；Hosting AOT 零 IL 警告 | ✔ |
| T8 并发加固（回归期发现） | PluginLoader.DisposeAsync/ReloadAsync 互斥（_disposeLock + _disposed 一次性进入）——watcher 触发的 reload 与宿主 Shutdown 并发时旧实现两侧都过 null 检查 → 已清字段 NRE（全量并行下 ~1/4 复现） | PluginLoader.cs | Hosting 套件连跑 6 轮 + 全量并行多轮零复现 | ✔ |

#### W66-01 P66：状态机与门控批（19 §2/§3/§4：P1-1..5 + D-7 + P2-13 + P2-14 + P2-16）

| 任务 | 目标 | 影响范围 | 验收 | 状态 |
|------|------|---------|------|------|
| T1 AwaitAsync 真等待（P1-1） | _settled 死字段修复——StartCoreAsync/RestartAsync 建 TCS（RunContinuationsAsynchronously），Active/Failed/Disposed/Pending-落定 CompleteSettled；AwaitAsync 对 Pending/Loading await settled（修复前立即返回） | PluginRuntime.cs | AwaitAsync_waits_until_terminal_state（PENDING 等待场景） | ✔ |
| T2 停止取消在途等待（P1-2） | _lifecycleCts 每启动重建；StopCoreAsync 入口 Cancel；WaitForDependenciesAsync 链接取消；取消异常 → 静默返回（不翻 FAILED——停方接管状态机）；init 成功后取消检查 → 弃暂存 + 补收敛 | PluginRuntime.cs | Stop_during_pending_wait_does_not_flip_failed（终态 Disposed） | ✔ |
| T3 停止互斥门（P1-3） | _stopGate SemaphoreSlim(1,1)——并发停止串行化，后到者见落定态直返（恰一次 quiesce/dispose）；终态停并入 rearm 停时补转移 PENDING→DISPOSED | PluginRuntime.cs | Concurrent_stops_dispose_plugin_exactly_once | ✔ |
| T4 rearm 全路径无未观察异常（P1-4） | FireAndForget(ObserveAsync)——吞异常观察；Unloading 期依赖重现 → StartAfterUnloadSettlesAsync（先并入在途卸载再启动）；启动在途并发调用幂等返回（_startBusy） | PluginRuntime.cs | 行为路径（随 P2-13 用例覆盖） | ✔ |
| T5 Loading 期依赖消失（P1-5） | StopAfterLoadSettlesAsync——AwaitAsync 等加载收敛后卸载（对齐 fiber.ts:665-672 epoch 对比：加载完成再卸，不中途撕裂 init） | PluginRuntime.cs | Dependency_loss_during_loading_unloads_to_pending | ✔ |
| T6 PENDING re-arm 语义（P2-13） | 依赖消失卸载落 Pending（_rearmedPending 标记，区别于初始等待）；依赖重现 → 自动重启；FAILED 随依赖变化重评（RestartIfFailedAsync）；显式 StopAsync 仍终态 Disposed（订阅销毁） | PluginRuntime.cs | Dependency_loss_lands_pending_and_reappearance_restarts + 三处旧断言更新（DiscoveryGating/DependencyReArm/PluginRuntimeTests） | ✔ |
| T7 门控 ACTIVE 时机（D-7） | KeyedServiceStore 属主暂存区：BeginStaging/Commit/Discard——init 期 Provide 暂存（外部 IsAvailable/Get 不可见、无通知），ACTIVE 后 Commit 落库 + 单次合并通知（= Cordis reflect.ts:294-296 ACTIVE 补发）；自读带 ownerId 可见暂存值；FAILED → Discard（值从未可见） | KeyedServiceStore.cs/ContextFacade.cs/PluginRuntime.cs | Provider_mid_init_provide_does_not_release_dependent | ✔ |
| T8 provides 属主校验（P2-16） | 兑现检查改 facade.HasProvided（属主本人 + realm 匹配）——他人同名同域值不再蒙混（原 IsAvailable 只查可用） | PluginRuntime.cs | Provides_fulfillment_requires_owner | ✔ |
| T9 root effects 收敛（P2-14） | ShutdownAsync 增 _rootContext.DisposeEffectsAsync()（宿主自注册资源不再进程级泄漏） | KeystoneHost.cs | 代码路径（回归覆盖） | ✔ |
| T10 回归 | 全量 6 套件 | 新增 7 测试；MA0051 拆分 WireDependencyRearm/CleanupCancelledStartAsync/TransitionToFailedAsync/QuiesceAllPluginsAsync | 438/438；Hosting AOT 零 IL 警告 | ✔ |

#### W67-01 P67：API 语义对齐批（19 §3/§4：D-2/D-3/D-8 + P2-6/7/8 + P2-18）

| 任务 | 目标 | 影响范围 | 验收 | 状态 |
|------|------|---------|------|------|
| T1 UpdateEntry 字段合并（D-2） | MergeEntryFields——提供的覆盖/缺省保留（Name/Disabled null 保留；Inject/Isolate 空集保留；Config 提供即整体替换=显式清空出口）；修复前整条目替换：未传字段被清空 + 结构键误判走冷路径 | KeystoneHost.cs | UpdateEntry_merges_fields_unprovided_keep_current（reloads=0） | ✔ |
| T2 parent 缺省不动（D-3） | RootParent("") 显式根哨兵 + 缺省 null = 保持现父（effectiveParent 推导）；修复前组内条目不带 parent 调用被挪根 | KeystoneHost.cs | UpdateEntry_parent_default_keeps_current_group + explicit_root_sentinel_moves_to_root | ✔ |
| T3 事件广播缺省（D-8） | EventSubscriptionOptions.ScopeFilter（缺省 false=广播——对齐 events.ts:159-176 hook.global \|\| !filter 即投递）；显式 true 才做祖先链过滤（等价 internal/service 显式 isolate filter） | EventSubscriptionOptions.cs/EventBus.cs/ContextFacadeTests | EventBroadcastDefaultTests 3 例 + 旧 G15 兄弟测试改废为广播+显式过滤双断言 | ✔ |
| T4 嵌套 id 任意深度（P2-6） | ResolveEntry 逐段下钻（`:` split 全段循环）；修复前 Split(':',2) 仅两级 | KeystoneHost.cs | ResolveEntry_walks_arbitrary_depth（三级） | ✔ |
| T5 无 id 条目 ensureId（P2-7） | EntryParser.Parse 尾部 EnsureIds——entry-{序号} 确定性分配（与 Name 解耦防路径字符入程序集名；撞车 #2/#3；同文件重解析稳定）；修复前分层丢弃 + diff ToDictionary(null) 崩 | EntryParser.cs | No_id_entries_get_generated_ids | ✔ |
| T6 MoveEntry 精确回滚（P2-8） | LocateEntry 原位记账 (源组,原下标)，失败回插原位；修复前回滚到根与报错矛盾 | KeystoneHost.cs | MoveEntry_failure_restores_exact_original_position | ✔ |
| T7 ConsoleSink 文档修正（P2-18） | 05 §5 承诺改"核心默认零 provider（内存缓冲），Console/File/exporter 均 opt-in"——对齐 Cordis 核心（console 属生态包） | 05-reliability.md | 文档校验通过 | ✔ |
| T8 回归 | 全量 6 套件 | 新增 9 测试（Hosting 6 + Runtime 3） | 447/447；Hosting AOT 零 IL 警告 | ✔ |

#### W68-01 P68：服务语义与机械收尾批（19 §3/§4：D-6 + P2-5/19/21/24..30 + LD-5 + 监督观测面修正）

| 任务 | 目标 | 影响范围 | 验收 | 状态 |
|------|------|---------|------|------|
| T1 Provide 报错式（D-6） | 同域二次 Provide 一律抛 ServiceAlreadyRegistered（无论属主——修复前同属主 rebind 静默覆盖）；对齐 reflect.ts:289-291 | KeyedServiceStore.cs/ContextFacade.cs/IPluginContext.cs | Second_provide_same_owner_throws 等 3 例 + 两处旧 rebind 测试改废 | ✔ |
| T2 Set（D-6） | 显式更新通道：属主校验（异属主 ServiceAlreadyRegistered）/未提供 GatingServiceNotFound/静默换值不通知（依赖方门控不重评——换值 ≠ 下线/上线，对齐 reflect.ts:254-265） | KeyedServiceStore.cs/ContextFacade.cs/IPluginContext.cs | Set_updates_value_without_notification 等 3 例 | ✔ |
| T3 结构键统一（P2-5） | StructuralKeyOf 与 ConfigDiffer.StructuralKey 同语义（父/name/inject/生效 isolate/形状；候选条目视角：祖先链取树上现值+自身候选值）——修复前缺 isolate → isolate 变更误走热路径 | KeystoneHost.cs | UpdateEntry_isolate_change_takes_cold_path | ✔ |
| T4 纯内存通知面对齐（P2-27） | ScheduleWriteBack 通知先于文件判定——无 ConfigFilePath 的 CRUD 全触发 ConfigUpdate（修复前 Create/Update 早退不通知、Remove 触发） | KeystoneHost.cs | Pure_memory_crud_fires_config_update_on_create_and_update | ✔ |
| T5 写回管线加固（P2-24/25/26） | OnWriteFailed 事件（Timer 丢弃路径可观测——对齐 Cordis logger.warn）；EACCES 短退避 3 次再降级（对齐 include 重试）；意外异常统一包 KeystoneException（修复 initial 裸 FileNotFoundException） | ConfigFileWriter.cs/WriteFailedEventArgs.cs | Debounced_flush_failure_surfaces_via_OnWriteFailed + Initial_write_failure_wrapped | ✔ |
| T6 序列化保真（P2-28） | 字典列表块形（`key:` 后逐 `- k: v`——修复重复键塌缩丢数据）；特殊标量双引号（`:`/`#`/空格/流标点——修复重解析错切）；空容器显式 `{}`/`[]`（修复塌缩 null） | EntrySerializer.cs | EntrySerializerFidelityTests 3+4 例 | ✔ |
| T7 fire-and-forget emit（P2-29） | EmitFireAndForget：PublishParallelAsync 后台执行不阻塞发布方，异常被观察（对齐 events.ts emit 不 await promise） | ContextFacade.cs/IPluginContext.cs | EmitFireAndForget_returns_before_async_listener_completes | ✔ |
| T8 logger Dispose 断言（P2-21/LG-21） | RingBufferLoggerProvider.IsDisposed + ShutdownAsync 显式回收自建 provider（M.E.L 9+ factory Dispose 不传导——实测确认）；GetLogger xmldoc 错位修正 | RingBufferLoggerProvider.cs/KeystoneHost.cs/ContextFacade.cs | Shutdown_disposes_owned_logger_factory | ✔ |
| T9 levels 键文档（P2-19）+ 决策默认值注记（P2-30） | 10 §4/HostOptions xmldoc：levels 键 = 完整 category（含域前缀）——裸名静默不命中；P2-30 记入 19 号注记（类型系统差异） | 10-plugin-sdk.md/KeystoneHostOptions.cs | 文档校验通过 | ✔ |
| T10 LD-5 注释修正（P2-31） | GroupTransactionTests 头注改实况：应用为声明序**串行**（undo 确定性要求），非 group.ts:71 并行——失败面等价、时序刻意差异 | GroupTransactionTests.cs | 注释与实现一致 | ✔ |
| T11 管道异常回填 + 监督观测面修正（回归发现） | 归因分离：中间件异常 → 失败结果回填 + actor 边界 LoggerMessage 日志（actor 存活）；终端 handler 崩溃 → HandlerFaultException 标记 → 回填 future 后上抛触发 OneForOne 重启（05 §2 监督契约保留）——修复"异常吞进永不完成的 Proto.Future"（全量挂起根因，人工二分 29 类才定位）；两处监督测试观测面更新（崩溃=即时失败结果而非挂到调用方超时） | CapabilityActor.cs | Middleware_exception_returns_failed_result_not_hang + 监督 3 例更新 | ✔ |
| T12 回归 | 全量 6 套件 | 新增 18 测试 | 465/465；Hosting AOT 零 IL 警告 | ✔ |

#### W69-01 P69：真热更新批（19 §3 D-1 / LD-6，ADR-0017）

| 任务 | 目标 | 影响范围 | 验收 | 状态 |
|------|------|---------|------|------|
| T1 原地通道（D-1） | PluginLoader.UpdateConfigAsync：quiesce 旧 runtime → 同 ALC Activator 新实例（缓存 _pluginType）→ 新 PluginRuntime（新 config）——不重编译/不换 ALC/不碰源码（对齐 fiber.ts update→restart"同代码"语义） | PluginLoader.cs | Config_update_succeeds_even_when_source_is_broken + Config_update_restarts_in_place_on_same_assembly | ✔ |
| T2 宿主接线（D-1） | UpdatePluginAsync 热分支 / UpdateEntryAsync 热分支 / 失败复原（loader 仍在→原地；冷路径失败 loader 已拆→回退冷重启）——PatchContext 瀑布可否决保持；冷路径分级不变（结构变/源码变仍 ReloadPluginAsync） | KeystoneHost.cs | UpdateEntry_config_only_also_takes_in_place_path + Structural_change_still_takes_cold_path_with_new_alc + 既有 108 例回归 | ✔ |
| T3 ADR-0017 | 机制级决策独立成文：原地通道语义/分级表/静态累积接受面/备选否决 | docs/decisions/adr-0017 | frontmatter 校验通过 | ✔ |
| T4 回归 | 全量 6 套件 | 新增 4 测试 | 469/469；Hosting AOT 零 IL 警告 | ✔ |

#### T2 执行记录（2026-08-16）

| 编号 | 工作项 | 类型 | 验收凭证 | 结果 |
|------|--------|------|---------|------|
| W57-T2-01 | 红测试 17 个（KeyedServiceStoreTests：域共存/属主/rebind/disposer 幂等/出锁探针（Task.Run 跨线程读 5s 超时断言）/scope 合并/嵌套/按域分区/16 线程 Barrier 竞写） | TDD | 编译期红（CS0246） | ✅ |
| W57-T2-02 | KeyedServiceStore + ServiceKey 实现：CD 热读 + Lock 复合写（WriteWithOwnerCheck/RemoveWithOwnerCheck）+ RecordChange（scope 并入或单键直发）+ EndScope（栈顶弹出/并入新栈顶/栈空出锁发）+ copy-on-write 订阅 + NotifyScope/Disposer/Subscription 三内部类均 Interlocked 幂等 | 实现 | — | ✅ |
| W57-T2-03 | 修 3 个实现/测试缺陷：realm ""（默认共享域）合法性（ValidateRealm 只拒 null 与非空纯空白，MA0015 参数名）；Drain() 别名 bug（返回同引用又 Clear——删 Drain，_ended 标志已保证单次消费）；2 个测试语义错（块内 using var 先于 scope dispose / 并发即时删键测不到竞写） | 修正 | — | ✅ |
| W57-T2-04 | 全量回归 362/362（Runtime 143→160，新增 17）；AOT Runtime 零 IL；独立提交 | 验收 | dotnet test 6 套件 Passed；publish grep 0 | ✅ |

#### T1 执行记录（2026-08-16）

| 编号 | 工作项 | 类型 | 验收凭证 | 结果 |
|------|--------|------|---------|------|
| W57-T1-01 | 红测试 11 个（IsolateSchemaTests：map 两档/shim/None/3 种非法形态 fail-fast/默认空/roundtrip/按名合并+false 移除/Shared 工厂空白校验） | TDD | 编译期红（CS0103/CS0021） | ✅ |
| W57-T1-02 | IsolateKind（独立文件，MA0048）+ IsolateSpec（readonly record struct，Private/Shared/None 工厂 + ToString 与 YAML 标量一致） | 实现 | — | ✅ |
| W57-T1-03 | EntryOptions.Isolate → IReadOnlyDictionary<string,IsolateSpec>；EntryParser.ParseIsolate（map+列表 shim+严格 fail-fast）；EntrySerializer map 形态按键序回写；EntryTree.MergeIsolate 按名合并（None 移除）；ConfigDiffer 结构键档位编码（name=true/@label/=false） | 实现 | — | ✅ |
| W57-T1-04 | 全量回归 345/345（Config 58→69，新增 11）；AOT 冒烟 Config+Hosting 双零 IL；08 §3 示例+字段表更新；独立提交 | 验收 | dotnet test 6 套件 Passed；publish grep 0 | ✅ |

### 7.65 P70 观测性专项（ADR-0018）

> 目标：消息模型排错从"人工二分"变"按 TaskId 一查到底"——P68 教训（中间件异常吞进永不完成的 Proto.Future，零日志零 trace 零痕迹，人工二分 29 个测试类才定位）。用户裁定：日志与追踪必须比原始 Cordis 更好。OTel 骨架三层（L1 探针纯 BCL / L2 事实复用 EventStore / L3 组合导出仅 Hosting）。

#### W70-01 P70：观测性批（T0-T5）

| 任务 | 目标 | 影响范围 | 验收 | 状态 |
|------|------|---------|------|------|
| T0 OTel 三包 | OpenTelemetry.Exporter.Console/OpenTelemetryProtocol/Extensions.Hosting 入 Hosting（CPM 管版本 1.12）；探针层保持零第三方依赖 | Directory.Packages.props / Keystone.Hosting.csproj | AOT 冒烟零警告零错误（OTel 1.12 全注解，无需 ADR-0015 式例外） | ✔ |
| T1 TraceContext 迁移 | `new Activity(...)` → `ActivitySource.StartActivity`（"Keystone.Runtime"）+ 功能保底 listener（仅本 source 恒 AllData）——OTel 可见 + 采样协商；GetCurrentTaskId 功能零回退（RingBuffer taskId 标签依赖） | TraceContext.cs | Runtime Trace 迁移测试 3 例 | ✔ |
| T2 L3 组合接线 | ObservabilityOptions（Enabled/Console 默认开/OTLP/采样率/慢阈值 5s）+ ConfigureObservability（AddSource 订阅 Runtime 探针源）+ Dispose 收口 | ObservabilityOptions.cs / KeystoneHostOptions.cs / KeystoneHost.cs | 端到端 2 例（span 经 Console 导出可见 + Enabled=false 不建 provider 功能保持） | ✔ |
| T3 actor 切片 | 消息边界常规日志（进 Debug/出 Information 含耗时/慢 Warning，LoggerMessage 源生成）+ KeystoneMeter 七指标 + 慢请求告警 + 监督 decider 包装（GetBaseException 根因上报 + 回调异常不反噬监督路径）+ ActorRestartedFact fire-and-forget 发根总线 | CapabilityActor.cs / CapabilityDomain.cs / KeystoneMeter.cs / ActorRestartedFact.cs / KeystoneHost.cs | ActorObservabilityTests 5 例（专用非并行 collection + MeterListener long/double 双注册） | ✔ |
| T4 config/host 切片 | keystone.config.apply / entry / group.transaction / hotupdate 四 span + hotupdate.operations（hot|cold）+ writer.failures 计数接线（EnsureConfigWriter OnWriteFailed → counter）；PluginLoader 暴露 CurrentConfig（hotupdate span old→new keys 素材）；逐条目 channel 分级（added/removed/disabled/structural=cold，config-changed=hot） | KeystoneHost.cs / PluginLoader.cs / TraceContext.cs（常量面） | ConfigObservabilityTests 4 例（apply/entry/group-tx span + hotupdate span + hot/cold 计数 + writer.failures） | ✔ |
| T5 回归 + AOT + 文档 | 全量 6 套件 + AOT 冒烟 + 14/11/AGENTS 回写 | 全仓 | 483/483（Core 30 / AI 19 / Config 84 / Sdk 35 / Runtime 201 / Hosting 114）；Hosting/Runtime AOT 零 IL；frontmatter 过 | ✔ |

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
| W33-01 | 01 §4 | `tests/Keystone.Hosting.Tests/MultiInstanceIntegrationTests.cs` | 集成用例绿 |
| W34-01 | — | 17-doc-compliance-audit.md（30 项差距） | 审计闭环 |
| W34-02 | ID-29 | `Actors/CapabilityActor.cs`、`CapabilityDomain.cs` | 持久 context 测试绿 |
| ID-29 | 01 §3/§4 | `Actors/`（实例 context） | W34-02 |
| W35-01 | ID-30 | `Hosting/KeystoneHost.cs`、`KeystoneHostOptions.cs` | `ShutdownGateTests`（3） |
| W35-02 | ID-30 | `Services/ServiceRegistry.cs`、`Loading/PluginLoader.cs` | `RebindAndReloadTests`（3） |
| ID-30 | 09 §4；02 §3 | `Hosting/`、`Runtime/Plugins/` | W35-01~03 |
| W36-01 | ID-31 | `Runtime/Plugins/Lifecycle/PluginRuntime.cs` | `DependencyTimeoutTests`（2） |
| ID-31 | DC-5 | `Runtime/Plugins/Lifecycle/PluginRuntime.cs` | W36-01~02 |
| W37-01 | ID-32 | `Actors/CapabilityDomain.cs`、`CapabilitySupervisionOptions.cs` | `SupervisionPolicyTests`（2） |
| ID-32 | 05 §2；09 §3 | `Actors/`（监督策略） | W37-01~02 |
| W38-01~03 | ID-33 | `Config/Interpolation/StaticInterpolator.cs`、`Config/Entries/EntryParser.cs`、`Hosting/KeystoneHostOptions.cs`、`Hosting/KeystoneHost.cs` | `EntryParserInterpolationTests`（8）+ `InterpolatedConfigTests`（2） |
| ID-33 | ADR-0012；DC-8 | `Config/Interpolation/`、`Config/Entries/`、`Hosting/` | W38-01~04 |
| W39-01~04 | ID-34 | `Runtime/Events/IFactEvent.cs`、`*Fact.cs`、`EventBus.cs`、`Actors/CapabilityActor.cs`、`Plugins/Lifecycle/PluginRuntime.cs`、`Hosting/` | `FactPersistenceTests`（7）+ `PluginLifecycleFactTests`（1）+ `FactStoreHostTests`（1） |
| ID-34 | ADR-0009；DC-11 | `Runtime/Events/`、`Actors/`、`Plugins/Lifecycle/`、`Context/`、`Hosting/` | W39-01~05 |
| W40-01~02 | ID-35 | `Actors/CapabilityActor.cs`、`SwapPipeline.cs`、`CapabilityDomain.cs` | `PipelineSwapTests`（3） |
| ID-35 | ADR-0003 决策 2；DC-10 | `Actors/`（管道缓存 + swap） | W40-01~03 |
| W41-01 | ID-36 | `Hosting/KeystoneHost.cs` | `LayeredConfigTests`（5） |
| ID-36 | 08 §4；DC-7 | `Hosting/`（分层叠加） | W41-01~02 |
| W42-01~02 | ID-37 | `Hosting/KeystoneHost.cs` | `DisabledEntryTests`（5） |
| ID-37 | 08 §3；DC-16 | `Hosting/`（disabled 挂起） | W42-01~03 |
| W43-01~02 | ID-38 | `Runtime/Context/ContextFacade.cs`、`Hosting/KeystoneHostOptions.cs`、`KeystoneHost.cs` | `LoggingCategoryTests`（2） |
| ID-38 | 05 §5；DC-20 | `Runtime/Context/`、`Hosting/`（日志命名） | W43-01~03 |
| W44-01~02 | ID-39 | `Actors/CapabilityActor.cs` | `TraceWiringTests`（3） |
| ID-39 | 06 §3-§4；DC-13 | `Actors/`（trace + 幂等） | W44-01~03 |
| W45-01~03 | ID-40/41 | `Hosting/KeystoneHost.cs`、`KeystoneHostOptions.cs`、`Config/Persistence/EntrySerializer.cs` | `CrudPersistenceTests`（6）+ `EntrySerializerIndexRegressionTests`（1） |
| ID-40 | 09 §5；08 §6.3；DC-15 | `Hosting/`（写回管线）、`Config/Persistence/` | W45-01~04 |
| ID-41 | DC-15 死代码实证 | `Config/Persistence/EntrySerializer.cs` | W45-03 |
| W46-01~03 | ID-42 | `Runtime/Plugins/Manifest/PluginManifest.cs`、`Sdk/Manifest/ManifestSchemaValidator.cs` | `ManifestSchemaFieldTests`（18） |
| ID-42 | 10 §6；DC-17 | `Runtime/Plugins/Manifest/`、`Sdk/Manifest/` | W46-01~04 |
| W47-01~03 | ID-43 | `Runtime/Persistence/StoredFact.cs`、`FileEventStore.cs`、`FactRetentionScheduler.cs`、`Hosting/` | `FactRetentionTests`（5）+ `HostRetentionTests`（1） |
| ID-43 | ADR-0009 决策 3；DC-18 | `Runtime/Persistence/`、`Hosting/` | W47-01~04 |
| W48-01~03 | ID-44 | `Runtime/Plugins/Loading/`、`Hosting/` | `PluginSourceAbstractionTests`（4）+ `PluginSourceWiringTests`（2） |
| ID-44 | ADR-0001 决策 1-2；DC-19 | `Runtime/Plugins/Loading/`、`Hosting/` | W48-01~04 |
| W49-01~03 | ID-45 | `Actors/`、`Context/` | `CancellationPropagationTests`（5） |
| ID-45 | 06 §1；DC-14 | `Actors/`、`Context/` | W49-01~04 |
| W50-01~03 | ID-46 | `Hosting/`（diff/watcher/编排） | `ConfigHotReloadTests`（6） |
| ID-46 | 08 §6；DC-9 | `Hosting/` | W50-01~04 |
| W51-01~03 | ID-47 | `docs/architecture/18-cordis-code-parity-audit.md`、`11-gap-register.md` §3.3 | 纯文档（无代码） |
| ID-47 | 审计方法；CA 系列 | `docs/`（18 文档） | W51-01~03 |
| W52-01~03 | ID-48 | `docs/architecture/18-cordis-code-parity-audit.md`（v2） | 纯文档（无代码） |
| ID-48 | 研判修正；否定性结论复核纪律 | `docs/`（18 v2 + 11 §3.3） | W52-01~03 |
| W53-01~03 | ID-49 | `docs/architecture/18-cordis-code-parity-audit.md`（v3） | 纯文档（无代码） |
| ID-49 | 决策批判；方案回到上游完整形态 | `docs/`（18 v3 + 11 §3.3） | W53-01~03 |
| W54-01~02 | ID-50 | `01-overview.md`、`03-context.md`、`18-cordis-code-parity-audit.md` | 纯文档（无代码） |
| ID-50 | 隔离默认语义裁定；推翻设计期类比 | `docs/`（01/03/18） | W54-01~02 |
| W55-01~03 | ID-51 | `docs/architecture/18-cordis-code-parity-audit.md`、`11-gap-register.md` | 纯文档（无代码） |
| ID-51 | CA-1 决策收口；抽象接缝裁定 | `docs/`（18/11） | W55-01~03 |
| W56-01~04 | ID-52 | `18-cordis-code-parity-audit.md`、`AGENTS.md` | 纯文档（无代码） |
| ID-52 | 发现接口收窄；同步契约；锁内发事件隐患证实 | `docs/`（18） | W56-01~03 |
| W70-01 | ADR-0018；05 §5 | `src/Keystone.Hosting/KeystoneHost.cs`、`src/Keystone.Runtime/Trace/`、`src/Keystone.Runtime/Actors/`、`src/Keystone.Runtime/Plugins/Loading/PluginLoader.cs` | ObservabilityWiringTests(2) + ActorObservabilityTests(5) + ConfigObservabilityTests(4) + Trace 迁移(3) |

## 9. 维护规则

- **联动 R10**：14 是文档治理的一部分——阶段事件/决策/偏差的更新与 13、AGENTS.md 状态同步（P0 落地时 AGENTS.md "设计期"→"实现期"）
- **只追加不改写**：历史行不改；更正新增行引用旧编号（§1）
- **阶段退出检查**：14 §2 状态 + §6 验收台账 + §3 分节记录三者同时闭合才算记录闭合（13 §4 DoD）
- **回溯约定**：实现期任何"当时为什么这么做"的疑问 → 先查 §4（决策）→ §5（偏差）→ §3（工作项）→ 三向索引（§8）定位代码
