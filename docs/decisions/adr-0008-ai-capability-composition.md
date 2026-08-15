---
type: adr
tags: [cordis-csharp, decisions, ai, maf, mcp, skills, composition]
created: 2026-08-15
status: accepted
---

# ADR-0008：AI 能力域组合策略 — 组合微软官方 MAF/MCP，不重造 AI 底层

> 决策状态：**accepted**（2026-08-15）
> 关联待定项：`docs/architecture/00-tech-stack.md` §2、`docs/architecture/10-plugin-sdk.md` §6、`docs/architecture/06-contracts.md` §6
> 来源：Keystone 方案分析（通用插件底层定位 + AI 生态组合路线）

## 背景（Context）

cordis-csharp 定位为**通用插件运行时**（配置驱动、多实例隔离、热重载、中间件管道），AI 能力域（llm/agents/skills/mcp/workflow）是其中的可选组合层。若 AI 底层全部自研（LLM 适配、技能包、MCP 双端、agent 编排），违背项目"不重造"哲学（`01-overview.md` §1），且成本是组合官方生态的 5-10 倍。

**Microsoft Agent Framework（MAF，[microsoft/agent-framework](https://github.com/microsoft/agent-framework)）** 是微软官方开源 agent 框架（MIT，Python + .NET 双语言，AutoGen/Semantic Kernel 方向的收敛继任者），.NET 侧为 `Microsoft.Agents.AI` NuGet 包族，已核实具备：

| 能力 | 包 |
|------|-----|
| LLM 提供方适配 | `Microsoft.Agents.AI.OpenAI/Anthropic/Foundry/CopilotStudio/GitHub.Copilot` |
| Agent 核心/会话/记忆抽象 | `Microsoft.Agents.AI` / `.Abstractions` |
| MCP client/server（typed AIFunction 工具） | `Microsoft.Agents.AI.Mcp` |
| 图编排（sequential/concurrent/handoff/group + checkpoint + 源生成器） | `Microsoft.Agents.AI.Workflows`（`.Generators`） |
| 声明式 YAML agents/workflows | `Microsoft.Agents.AI.Declarative` |
| 托管/Aspire/隔离键/会话存储 | `Microsoft.Agents.AI.Hosting` |
| Agent Skills（SEP-2640 跨厂商技能格式） | `Microsoft.Agents.AI/Skills` |
| 安全代码执行（进程隔离参考） | `Microsoft.Agents.AI.Hyperlight` / `LocalCodeAct` |
| 治理/审计 | `Microsoft.Agents.AI.Purview` |

**边界确认（互补不竞争）**：MAF 是 agent 领域框架（编排深度），**无插件生命周期**——无状态机、无 quiesce、无热重载卸载、无 ALC 插件隔离；这些正是 cordis-csharp 的差异化（ADR-0005/0007）。组合前提：MAF 基于微软 DI（IServiceProvider），与 cordis-csharp 的 Keyed Services/子容器同源，集成成本低。

## 决策（Decision）

### 决策 1：单向依赖 — 框架核心不依赖 MAF

- cordis-csharp 核心（通用插件运行时）**不引用任何 MAF 包**，独立成立
- 依赖方向严格单向：`AI 能力域适配器 → MAF`；MAF 绝不反向依赖 cordis-csharp

### 决策 2：AI 能力域适配层（组合 MAF 包族）

| 能力域 | 组合来源 | 说明 |
|--------|---------|------|
| llm 域 | `Microsoft.Agents.AI.OpenAI/Anthropic/Foundry` 等 | LLM 适配器 = 插件，进接口白名单 |
| agents 域 | `Microsoft.Agents.AI` | Agent 接口组合进 `IPluginContext` |
| skills 域 | `Microsoft.Agents.AI/Skills` | SEP-2640 技能包消费（见决策 3） |
| mcp 域 | `Microsoft.Agents.AI.Mcp` | 双端：client 消费 MCP 工具 → typed AIFunction；server 暴露能力域为 MCP 技能包 |
| workflow 域 | `Microsoft.Agents.AI.Workflows` | ADR-0004 跨域编排的**实现替换**：fan-out/fan-in + checkpoint + 取消级联现成，不自研编排器 |
| hosting | `Microsoft.Agents.AI.Hosting` + Aspire | 隔离键、会话存储、A2A/AGUI 端点 |
| 可观测性 | MAF 内置 OpenTelemetry | 对齐 `05-reliability.md` §5 |

### 决策 3：SEP-2640 技能格式采用（插件技能包）

- 插件技能包 = **SEP-2640 跨厂商标准**（`skill://index.json` + `SKILL.md`，schema 位于 `schemas.agentskills.io`），经 MAF `AgentMcpSkillsSource` 消费
- `10-plugin-sdk.md` §6 manifest 增 `skills` 字段（`skill://` URI 列表）
- 采用即跟随标准演进（MAF 0021/0029 设计文档 ongoing），技能格式不是 cordis-csharp 私有格式

### 决策 4：MCP 双端经 MAF，不自研

- 生态桥：C# 插件可被任何 MCP 客户端消费；宿主可用整个 MCP 工具市场（跨语言生态）
- 序列化分界（ADR-0004 不变）：域内 MessagePack 强类型直接调用；MCP 边界 JSON-RPC（SDK 自带），两处显式声明

> **实现备注（2026-08-15，P14 落地，见 14-implementation-log §7.14 / ID-12、ID-13）**：`Microsoft.Agents.AI.Mcp` 截至当前**无稳定版**（11 个版本全 alpha，最新 1.17.0-alpha.260804.1），不可作为生产依赖；且已核实其内部依赖 `ModelContextProtocol ≥1.2.0`（协议层与 agent 集成层是**分层关系**，非二选一）。方向不变（组合官方 MCP，不自研），**实现层替换**：MCP 协议层组合微软官方稳定协议 SDK `ModelContextProtocol`（NuGet `ModelContextProtocol.Core` 2.2.0，net10.0 原生支持、源生成 JSON AOT 友好、与 MAF 同源 M.E.AI）。`Keystone.AI/Mcp/` 承载双端适配，**公共面 = Keystone 协议中立契约（ID-13）**：接口/DTO/options 零 SDK 类型，SDK 类型内聚于实现内部映射——协议层升级（2.x→3.x）或 MAF agent 集成层接入（MCP 工具 → typed AIFunction 进 MAF workflow）时**调用方零改动**（由 `Bridge_public_contracts_reference_no_MCP_SDK_types` 测试锁定）。

### 决策 5：Hyperlight 作为 `IPluginHost` 进程隔离参考（未来路线）

- ADR-0001 预留的进程隔离扩展点，未来实现参考 `Microsoft.Agents.AI.Hyperlight`（安全代码执行），**不进入本期**

## 理由（Rationale）

1. **不重造哲学**（`01-overview.md` §1）：LLM 适配/技能/编排/MCP 是纯基础设施，组合官方生态省 5-10x，且微软持续投入维护。
2. **生态问题从"自建"变"接入"**：技能包走 SEP-2640 跨厂商标准、工具走 MCP 市场，cordis-csharp 插件生态不依赖自建市场（回应"生态断代"核心风险）。
3. **差异化不稀释**：生命周期/热重载/隔离（ADR-0005/0007）是 MAF 没有的，组合不改变核心定位。
4. **同源 DI 降低集成成本**：Keyed Services/子容器与 MAF 的 IServiceProvider 体系天然兼容。
5. **单向依赖保住通用性**：核心不依赖 MAF，任何非 AI 的 C# 应用仍可只用 cordis-csharp 通用运行时。

## 权衡 / 风险（Trade-offs / Risks）

| 风险 | 说明 | 缓解 |
|------|------|------|
| 版本跟随成本 | MAF 迭代极快（持续每日更新，0029 等文档仍 proposed） | 适配层隔离：能力域适配器独立于框架核心发布 + 版本锁定策略 |
| AOT 兼容未全面声明 | MAF 用源生成器（`Workflows.Generators`）为 AOT 友好信号，但官方未声明全面兼容 | **组合包 AOT 验收门**：组合前验证 + 提交前冒烟（规则 0 验证方式扩展） |
| 定位边界模糊 | 若 cordis-csharp 退化为"MAF 的壳"，差异化叙事失效 | 显式声明核心不依赖 MAF；能力域适配器是可选组合 |
| 技能格式演进中 | SEP-2640 仍在演进（0029 proposed） | 采用记录为本 ADR，跟随标准决策文档演进，适配层吸收变化 |
| 依赖方向反向 | MAF 反过来依赖 cordis-csharp → 框架失去通用性 | 决策 1 硬约束：单向依赖，代码评审强制 |

## 备选方案（Alternatives）

| 方案 | 描述 | 结论 |
|------|------|------|
| A（采纳） | AI 能力域组合微软官方 MAF/MCP | **采纳**：不重造 + 生态接入 + 差异化保持 |
| B | 自研 AI 底层（LLM 适配/技能/MCP/编排） | 不采纳：违背不重造哲学，成本 5-10x，重复造轮子 |
| C | 以 MAF 为宿主（cordis-csharp 变成 MAF 插件） | 不采纳：失去通用插件运行时定位，生命周期能力无处安放 |

## 影响（Consequences）

- `docs/architecture/00-tech-stack.md`：§2 增 **T10 MAF 包族**（AI 能力域底层）、**T11 .NET 10 文件式应用**（插件脚本形态，`#:package` ↔ manifest `dependencies`）；新增组合包 AOT 验收门
- `docs/architecture/10-plugin-sdk.md`：§6 manifest 增 `skills` 字段（`skill://` URI，SEP-2640）
- `docs/architecture/06-contracts.md`：§6 跨域编排实现层 = 组合 MAF Workflows（**ADR-0004 决策不变，实现替换**）
- `docs/architecture/01-overview.md` / `README.md`：定位修订——"不重造"清单补 AI 底层组合
- `docs/decisions/README.md` 索引增补 ADR-0008
- **不回退项**：核心不得依赖 MAF；MAF 不得反向依赖 cordis-csharp；跨域编排的 TaskId 贯穿语义（ADR-0004）不得被组合实现稀释

## 关联

- `docs/architecture/01-overview.md` §1（不重造清单）、`docs/architecture/00-tech-stack.md` §2（技术栈）、`docs/architecture/10-plugin-sdk.md` §6（manifest）、`docs/architecture/06-contracts.md` §6（跨域编排）
- ADR-0001（`IPluginSource` 远程分发 → SEP-2640 技能包通道；`IPluginHost` 进程隔离 → Hyperlight 参考）
- ADR-0002（AOT 就绪：组合包 AOT 验收门是规则 0 的扩展）
- ADR-0004（跨域编排：决策不变，实现层组合 MAF Workflows）
- 外部：microsoft/agent-framework（MAF）、ModelContextProtocol（MCP 规范）、SEP-2640（Agent Skills 规范）
