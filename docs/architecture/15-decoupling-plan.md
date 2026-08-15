---
type: architecture-doc
tags: [cordis-csharp, architecture, decoupling, work-plan]
created: 2026-08-15
---

# 15 — 解耦工作计划（第三方依赖隔离审计）

> 目的：审计当前项目中对第三方包的直接耦合点，评估严重度，给出分阶段解耦工作计划。
> 触发：用户评审 MCP 桥隔离后提出"还有多少地方直接耦合、没做隔离"。
> 状态：**执行中**——D1 ✅（P16）、D2/D3/D4 待执行；执行记录走 14-implementation-log（ID 决策通道）。

## 1. 审计方法

1. 全量扫描 6 个 src 工程的第三方 `using`（MessagePack/YamlDotNet/AgileConfig/Roslyn/Proto.Actor/MAF/MCP）
2. 逐点判定耦合性质：**公共签名泄漏**（public 方法参数/返回类型/属性/基类暴露第三方类型）、**架构未接线**（设计承诺但未实现）、**配置泄漏**（配置类型暴露）、**实现内部**（可接受）
3. 严重度分级：
   - 🔴 **高**：调用方必须引用第三方包才能使用框架公共 API（或架构承诺缺失）
   - 🟡 **中**：公共 API 泄漏第三方类型，但当前无外部消费 / 属组合层预期 / 属 ADR 例外区
   - 🟢 **低**：实现内部使用，无公共面泄漏

## 2. 耦合点清单（含独立复核合入）

| # | 位置 | 耦合类型 | 泄漏的第三方类型 | 严重度 | 依据 |
|---|------|---------|----------------|--------|------|
| C1 | `Keystone.Runtime/Actors/CapabilityDomain.cs` | 公共签名泄漏 | Proto.Actor `PID`/`ActorSystem` | 🔴 | `Spawn` 返回 `PID`（24 行）；构造器收 `ActorSystem`（15 行）；`RequestAsync(PID,…)`（34 行） |
| C1b | `Keystone.Runtime/Actors/CapabilityActor.cs` | 继承泄漏 + 签名 | Proto.Actor `IActor`/`IContext` | 🔴 | `: IActor`（10 行）；`public ReceiveAsync(IContext)`（20 行）（复核遗漏项） |
| C2 | `Keystone.Hosting/KeystoneHost.cs` | 架构未接线 | —（能力域整体未接线） | 🔴 | 01-overview L28-29/47、09 L18/21/35/45 承诺"管理层 spawn 能力域 + 监督"，全 src 无一处实例化 ActorSystem/CapabilityDomain（仅测试用）——**能力域 actor 层是生产死代码** |
| C3 | `Keystone.Config/Entries/EntryParser.cs` | 公共签名泄漏 | YamlDotNet `YamlNode` | 🟡 | 仅 `public NodeToObject(YamlNode?)`（35 行）；`Get/Scalar/Bool` 均 private（102-119 行，复核修正）；无外部消费 |
| C4 | `Keystone.AI/Skills/SkillRegistry.cs`、`KeystoneSkill.cs` | 返回值 + 继承泄漏 | MAF `AgentSkillsSource`/`AgentSkill`/`AgentSkillFrontmatter`/`AgentSkillResource` | 🟡 | `FromManifest→AgentSkillsSource`（13 行）；`KeystoneSkill : AgentSkill`（9 行）；ADR-0008 决策 2/3 组合层背书 |
| C5 | `Keystone.Runtime/Plugins/Loading/RoslynCompiler.cs` | 公共签名泄漏 | Roslyn `MetadataReference` | 🟡 | `Compile(…, IReadOnlyList<MetadataReference>)`（14 行）；ADR-0002 例外区背书 |
| C6 | `Keystone.Core/Contracts/TaskEnvelope.cs`、`TaskResultEnvelope.cs` | 序列化器耦合契约 | MessagePack `[MessagePackObject]`/`[Key]` | 🟡 | ADR-0004 L26/48"JSON 可配置"承诺 vs 契约直接钉死 MessagePack；Keystone.Core.csproj 直引 MessagePack |
| C6b | `Keystone.Runtime/Persistence/StoredFact.cs`、`FileEventStore.cs` | 配置泄漏（内部） | MessagePack | 🟢 | `[MessagePackObject]`（9 行）；序列化在内部实现（33/83 行）（复核新增） |
| C7 | `Keystone.AI/Skills/KeystoneSkill.cs` | 继承泄漏 | MAF `AgentSkill` 基类 | 🟢 | 组合层预期（ADR-0008 决策 3：实现 MAF 技能接口才能被 MAF 消费） |
| C8 | `Keystone.AI/Workflows/WorkflowBridge.cs` + `Keystone.AI.csproj` | 死依赖 | `Microsoft.Agents.AI.Workflows`（引用零使用） | 🟢 | WorkflowBridge 纯 Task.WhenAll 无 MAF 类型；csproj 引用该包但代码零使用；注释自述"由 HostAgent 驱动（实现期细化）"——ADR-0008 决策 2 workflow 域未实现（复核新增） |

> 已隔离良好、无需处理：MCP 桥（ID-13 契约隔离）、AgileConfig（`IAgileConfigClient` 薄抽象 + M.E.C 扩展）、EventBus/Timer/EntryOptions/Manifest（纯框架类型）、`IConfiguration` 抽象引用（ADR-0013）、`ILoggerProvider`/`ILogger`（M.E.Logging 微软平台接口，非第三方）、PluginLoader/PluginAssemblyLoadContext（BCL `AssemblyLoadContext`）、Keystone.Hosting/Sdk 全部（零第三方 using）。

## 3. 分阶段解耦计划

> 顺序原则：先修"高"（公共面被迫引用第三方 + 架构缺口），再修"中"（API 整洁度），"低"记录不动作。每项含：目标形态、验收条件、决策通道。

### 阶段 D1：能力域接线 + 隔离（C1+C1b+C2，🔴）—— ✅ 已执行（P16，2026-08-15）

- **目标**：`KeystoneHost` 按 01-overview 接线能力域；`CapabilityDomain`/`CapabilityActor` 公共面不再暴露 Proto.Actor 类型
- **落地**（14 §7.16 / ID-14）：`CapabilityHandle` 封装 PID 作框架句柄；`CapabilityDomain` 构造器私有化 + `Create`（自有 ActorSystem）/`Attach`（注入测试缝，隔离测试豁免）；`CapabilityActor` 降 internal；`KeystoneHost` 增 `EnableCapabilityDomain`（默认开）+ `GetCapabilityDomain()`，StartAsync 创建 / ShutdownAsync 释放。200/200 全绿 + Runtime AOT 零 IL 警告。

### 阶段 D2：配置解析面收敛（C3，🟡）

- **目标**：`EntryParser` 公共面不再暴露 YamlDotNet 类型
- **设计方向**：`NodeToObject`/`Get`/`Scalar`/`Bool`/`StringList` 降为 `private`/`internal`（当前无外部调用者，仅内部递归 + `Parse` 公共入口）；`Parse(string)` → `IReadOnlyList<EntryOptions>` 保持
- **验收**：`EntryParser` 公共签名无 `YamlDotNet.*`；Config 测试全绿（现有测试全走 `Parse(string)`）
- **决策通道**：无需（纯可见性收敛，无行为变化）
- **风险**：极低

### 阶段 D3：序列化器抽象（C6，🟡）—— ✅ 已执行（P17，2026-08-15）

- **目标**：兑现 ADR-0004"MessagePack 默认 / JSON 可配置"，序列化器不钉死契约
- **调研结论**：跨域边界（Proto.Actor 同进程引用传递）无实际序列化，`[MessagePackObject]` 是契约声明；唯一执行序列化的消费点是 FileEventStore（事件持久化）→ 抽象应用到该通道
- **落地**（14 §7.17 / ID-15）：方案 A——`IContractSerializer`（泛型 + 源生成 AOT 安全）+ `MessagePackContractSerializer`（默认）+ `JsonContractSerializer`（STJ 源生成上下文注入）；`FileEventStore` 构造器可选注入（默认 MessagePack 兼容）。205/205 全绿 + Core/Runtime AOT 零 IL 警告。

### 阶段 D4：AI 组合层 API 收敛 + 死依赖清理（C4+C7+C8，🟡/🟢）

- **目标**：区分"组合层对外入口"（可暴露 MAF）与"框架通用 API"（不应暴露）；清理死依赖
- **设计方向**：
  - `SkillRegistry.FromManifest` 返回类型评估：若仅 AI 组合层内部消费 → 保持；若插件/宿主直接调用 → 加 Keystone 侧薄契约（如 `ISkillSource`）再映射 MAF
  - `KeystoneSkill : AgentSkill` 保持（组合层预期，C7 记录不动作）
  - **C8 死依赖**：`Microsoft.Agents.AI.Workflows` 当前零使用——二选一：①移除引用（workflow 域实现期细化后再引）②实现 WorkflowBridge 的 MAF 接线（O2 已在 P12 用纯 Task 验证，MAF 图构建未落地）。倾向①：死依赖违反单向组合的克制原则，等真实接线需求再引
- **验收**：明确消费边界（谁调 SkillRegistry）；移除或接线 Workflows 依赖；若加契约 → 隔离验证测试
- **决策通道**：轻量 ID 决策
- **风险**：低（当前无外部消费）

### 阶段 D5：回归闭环

- **目标**：全量测试 + AOT 冒烟 + 文档同步 + 提交
- **内容**：解耦后全量回归（`dotnet test`）、Keystone.Runtime/AI AOT 发布零 IL 警告、14 日志记录（W 编号 + ID 决策）、ADR 更新（如 D3）、AGENTS.md 状态同步
- **验收**：全绿 + 文档闭合 + 提交纪律（R03）

## 4. 优先级建议

| 优先级 | 阶段 | 理由 |
|--------|------|------|
| P0 | D1（C1+C1b+C2） | ✅ 已执行（P16）：公共 API 被迫引用 Proto.Actor + 能力域 actor 是生产死代码 + 架构 01/09 承诺（监督/多实例）未兑现，影响宿主嵌入形态 |
| P1 | D3（C6+C6b） | ✅ 已执行（P17）：IContractSerializer 抽象兑现 ADR-0004，应用到事件持久化 |
| P2 | D2（C3） | API 整洁度，零成本（可见性收敛）（下一阶段） |
| P2 | D4（C4+C8） | 边界明确 + 死依赖清理，工作量小 |
| — | D5 | 每阶段内闭环，不单独排期 |

## 5. 待办

- [x] 独立复核子代理结果已合入（修正 C3、新增 C1b/C6b/C8）
- [x] 阶段 D1 已执行（P16，14 §7.16/ID-14）
- [x] 阶段 D3 已执行（P17，14 §7.17/ID-15）
- [ ] 阶段 D2 开工（EntryParser 可见性收敛）
- [ ] 阶段 D4 开工（AI 层边界 + Workflows 死依赖）
- [ ] 每阶段按 13 §6 纪律执行：测试先行 + 决策沉淀 + 文档同步

## 关联

- ADR-0004（消息契约/序列化）、ADR-0008（AI 组合）、ADR-0002（Roslyn 例外区）、ADR-0013/0014（配置抽象）
- 01-overview（三层架构：管理层 spawn 能力域）、09-management-layer（宿主嵌入形态）
- 14-implementation-log（执行记录）
