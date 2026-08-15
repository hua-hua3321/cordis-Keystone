---
type: adr
tags: [cordis-csharp, decisions, events, persistence, event-sourcing]
created: 2026-08-15
status: accepted
---

# ADR-0009：事件持久化 — 事实事件 append-only 事件日志

> 决策状态：**accepted**（2026-08-15）
> 关联待定项：`docs/architecture/03-context.md` §4（事实事件→持久日志只有一句，无设计）
> 来源：Keystone 方案补充排查（03-context §4"事实事件持久化"开放项）

## 背景（Context）

`03-context.md` §4 事件分层定义了三类事件，其中**事实事件（session events，对应 Cordis durable facts）**标注"必须存活（任务完成/失败）→ 持久日志"，但只有这一句声明：**存储介质、重放语义、保留策略均无设计**（补充排查确认的开放项）。

对齐 Cordis 侧实证：harness 的事实事件 = 追加式会话日志（`ctx.sessions`，仅追加的 SessionEvent 日志，用于 reload 后恢复）。C# 版需要把"持久日志"落实为可落地的契约。

同时 ADR-0008 已组合 MAF（`Microsoft.Agents.AI` 有 AgentSessionStore、Workflows checkpoint）——需要明确**框架级事实事件日志**与 MAF 会话存储的边界，避免重复设计。

## 决策（Decision）

### 决策 1：事实事件 = append-only 事件日志，`IEventStore` 抽象

- 事实事件**只追加、不修改、不删除**（event-sourcing 风格），TaskId 为索引维度（对齐 06-contracts §3）
- 存储经 `IEventStore` 抽象，**默认实现 = 本地文件**：结构化日志（MessagePack 序列化，遵守 ADR-0004 显式序列化契约 + 规则 0）
- 可插拔：数据库/对象存储实现后续按需（`IEventStore` 是框架契约，不进插件 SDK 白名单）

```csharp
public interface IEventStore
{
    Task AppendAsync(StoredFact fact);                          // 追加（异步，不阻塞主链路）
    IAsyncEnumerable<StoredFact> ReplayAsync(ReplayQuery query); // 重放（TaskId/能力域/时间范围）
    Task<int> PruneAsync(RetentionPolicy policy);               // 保留策略执行
}
```

### 决策 2：重放语义

- 重放按 **TaskId / 能力域 / 时间范围** 查询（`ReplayQuery`）
- 事件不可变 → 重放天然幂等；重放用于：崩溃恢复、审计、测试断言
- 重放接口返回 `IAsyncEnumerable`，不一次性加载全量（大日志友好）

### 决策 3：保留策略（配置化，不阻塞主链路）

- 默认策略：TTL + 大小上限 + 归档（配置层声明，08-configuration-layer）
- 写入异步 + 失败降级：写入失败不影响主链路（记日志 + 告警），事实事件丢失语义由调用方声明（`durable: true` 的事件才必须写成功）
- 事件分级：`durable: true`（必须落盘，写失败 → 任务标记失败/告警）vs 默认（尽力写，失败降级）

### 决策 4：与 MAF 组合的边界（ADR-0008）

- cordis-csharp 的 `IEventStore` 是**框架级事件日志契约**（TaskId 维度），独立自持
- MAF 的 AgentSessionStore / Workflows checkpoint 是 **agent 会话状态**（会话维度），作为可选的 `IEventStore` 宿主实现桥接（适配层组合），**不替换框架契约**

## 理由（Rationale）

1. **与 Cordis 语义对齐**：harness 事实事件就是 append-only 会话日志（仅追加、重放恢复），本决策把它落实为显式契约。
2. **不阻塞主链路是硬约束**：事件写入是旁路（观察者性质，03 §4 事件分层），失败不得拖垮任务主链；`durable: true` 分级给"必须存活"的语义一个显式出口（对齐 06-contracts §2 判断表的"持久需要"列）。
3. **AOT/序列化纪律**：MessagePack 显式契约 + `IEventStore` 接口，无运行时反射（规则 0、ADR-0004）。
4. **与 MAF 不重复**：契约独立、实现可桥接——ADR-0008 的单向依赖与"不重造"哲学一致。
5. **重放幂等免费**：不可变事件日志天然可重放，审计/恢复/测试共用一条路径。

## 权衡 / 风险（Trade-offs / Risks）

| 风险 | 说明 | 缓解 |
|------|------|------|
| 事件格式演进 | 已落盘事件结构变更 → 重放读旧格式 | 事件带版本字段（`StoredFact.SchemaVersion`）+ 迁移策略实现期定 |
| 写入放大 | 每个事实事件一次文件追加 | 批量 flush（缓存合并）+ 异步写入；`durable: true` 才同步确认 |
| 存储膨胀 | 长期运行日志无限增长 | 保留策略（TTL/上限/归档）配置化 + Prune 定时执行 |
| 与 MAF 边界模糊 | AgentSessionStore 与 IEventStore 概念重叠 | 决策 4 显式分层：框架契约（TaskId）vs 会话状态（会话），文档固化 |

## 备选方案（Alternatives）

| 方案 | 描述 | 结论 |
|------|------|------|
| A（采纳） | append-only 事件日志 + IEventStore 抽象 + 重放/保留 | **采纳**：语义对齐 Cordis，契约可插拔 |
| B | 只写日志文件不建抽象（ILogger 直接记录事实事件） | 不采纳：重放/查询/保留无契约，审计与恢复能力丢失 |
| C | 事实事件直接组合 MAF 会话存储，不自持契约 | 不采纳：TaskId 维度与会话维度错位，框架级事件日志是 06-contracts 链路追踪的根基，必须自持 |

## 影响（Consequences）

- `docs/architecture/03-context.md` §4：事实事件行补"存储 = append-only 事件日志（ADR-0009）"
- `docs/architecture/06-contracts.md` §3：链路追踪补重放维度（ReplayQuery 按 TaskId）
- 新增 `IEventStore` 契约（框架侧，非插件 SDK）；默认本地文件实现（MessagePack）
- 配置层：`eventStore` 段（介质/保留策略/归档），落 08-configuration-layer §5 schema 校验
- `docs/decisions/README.md` 索引增补 ADR-0009
- 07 G 系列无直接对应（本项源自补充排查的 03 §4 开放项），在 11-gap-register 登记状态

## 关联

- `docs/architecture/03-context.md` §4（事件分层）、`docs/architecture/06-contracts.md` §3（链路追踪）、`docs/architecture/08-configuration-layer.md` §5（配置 schema）
- ADR-0004（显式序列化契约：事实事件 payload 走 MessagePack）
- ADR-0008（MAF 组合边界：AgentSessionStore 可桥接为 IEventStore 宿主实现）
- ADR-0002 / 规则 0（AOT 就绪：默认实现无运行时反射）
