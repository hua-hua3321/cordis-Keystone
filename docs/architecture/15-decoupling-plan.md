---
type: architecture-doc
tags: [cordis-csharp, architecture, decoupling, work-plan]
created: 2026-08-15
---

# 15 — 解耦工作计划（第三方依赖隔离审计）

> 目的：审计当前项目中对第三方包的直接耦合点，评估严重度，给出分阶段解耦工作计划。
> 触发：用户评审 MCP 桥隔离后提出"还有多少地方直接耦合、没做隔离"。
> 状态：**✅ 全部执行完成（P16-P20）**——D1 能力域接线/隔离、D3 序列化抽象、D2 配置解析收敛、D4 AI 层边界+死依赖清理、D5 回归闭环。C1/C1b/C2/C3/C6/C6b/C8 已闭合；C4/C5/C7 记录保持（组合层/ADR 例外区背书）。执行记录见 14-implementation-log（ID-14~17）。

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

**执行状态（2026-08-15，P16-P20）**：✅ 已闭合 C1/C1b/C2（P16-D1）、C6/C6b（P17-D3）、C3（P18-D2）、C8（P19-D4）；📌 记录保持 C4/C7（AI 组合层预期，ADR-0008 决策 2/3）、C5（Roslyn 例外区，ADR-0002）。

## 3. 分阶段解耦计划

> 顺序原则：先修"高"（公共面被迫引用第三方 + 架构缺口），再修"中"（API 整洁度），"低"记录不动作。每项含：目标形态、验收条件、决策通道。

### 阶段 D1：能力域接线 + 隔离（C1+C1b+C2，🔴）—— ✅ 已执行（P16，2026-08-15）

- **目标**：`KeystoneHost` 按 01-overview 接线能力域；`CapabilityDomain`/`CapabilityActor` 公共面不再暴露 Proto.Actor 类型
- **落地**（14 §7.16 / ID-14）：`CapabilityHandle` 封装 PID 作框架句柄；`CapabilityDomain` 构造器私有化 + `Create`（自有 ActorSystem）/`Attach`（注入测试缝，隔离测试豁免）；`CapabilityActor` 降 internal；`KeystoneHost` 增 `EnableCapabilityDomain`（默认开）+ `GetCapabilityDomain()`，StartAsync 创建 / ShutdownAsync 释放。200/200 全绿 + Runtime AOT 零 IL 警告。

### 阶段 D2：配置解析面收敛（C3，🟡）—— ✅ 已执行（P18，2026-08-15）

- **目标**：`EntryParser` 公共面不再暴露 YamlDotNet 类型
- **落地**（14 §7.18 / ID-16）：`NodeToObject` 降 private（无外部调用，仅内部递归）；隔离测试锁定 EntryParser 公共静态签名无 YamlDotNet 泄漏。206/206 全绿。

### 阶段 D3：序列化器抽象（C6，🟡）—— ✅ 已执行（P17，2026-08-15）

- **目标**：兑现 ADR-0004"MessagePack 默认 / JSON 可配置"，序列化器不钉死契约
- **调研结论**：跨域边界（Proto.Actor 同进程引用传递）无实际序列化，`[MessagePackObject]` 是契约声明；唯一执行序列化的消费点是 FileEventStore（事件持久化）→ 抽象应用到该通道
- **落地**（14 §7.17 / ID-15）：方案 A——`IContractSerializer`（泛型 + 源生成 AOT 安全）+ `MessagePackContractSerializer`（默认）+ `JsonContractSerializer`（STJ 源生成上下文注入）；`FileEventStore` 构造器可选注入（默认 MessagePack 兼容）。205/205 全绿 + Core/Runtime AOT 零 IL 警告。

### 阶段 D4：AI 组合层 API 收敛 + 死依赖清理（C4+C7+C8，🟡/🟢）—— ✅ 已执行（P19，2026-08-15）

- **目标**：区分"组合层对外入口"（可暴露 MAF）与"框架通用 API"（不应暴露）；清理死依赖
- **落地**（14 §7.19 / ID-17）：
  - **C8 已闭合**：移除 `Microsoft.Agents.AI.Workflows` 死依赖（WorkflowBridge 纯 Task，MAF 图构建未接线前不引）
  - **C4 记录保持**：`SkillRegistry.FromManifest` 返回 MAF `AgentSkillsSource`——唯一消费方是 AI 层内部（组合层预期，ADR-0008 决策 3）
  - **C7 记录不动作**：`KeystoneSkill : AgentSkill` 组合层预期
- **验收**：206/206 全绿 + AI AOT 零 IL 警告（移除 Workflows 后）

### 阶段 D5：回归闭环 —— ✅ 已执行（P20，2026-08-15）

- **内容**：全量回归 206/206（重构建验证）、六工程 AOT 发布零 IL 警告、15-plan 状态更新、AGENTS.md 同步、git 提交
- **验收**：✅ 全绿 + 文档闭合 + 提交纪律（R03）

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
- [x] 阶段 D2 已执行（P18，14 §7.18/ID-16）
- [x] 阶段 D4 已执行（P19，14 §7.19/ID-17：C8 闭合、C4/C7 记录保持）
- [x] 阶段 D5 回归闭环已执行（P20：206/206 + 六工程 AOT 零 IL 警告）
- [x] 每阶段按 13 §6 纪律执行：测试先行 + 决策沉淀 + 文档同步（ID-14~17）

## 关联

- ADR-0004（消息契约/序列化）、ADR-0008（AI 组合）、ADR-0002（Roslyn 例外区）、ADR-0013/0014（配置抽象）
- 01-overview（三层架构：管理层 spawn 能力域）、09-management-layer（宿主嵌入形态）
- 14-implementation-log（执行记录）
