---
type: adr
tags: [cordis-csharp, decisions, concurrency, context, hot-reload]
created: 2026-08-15
status: accepted
---

# ADR-0003：context 并发模型 + 管道配置热更新

> 决策状态：**accepted**（2026-08-15）
> 关联待定项：`docs/architecture/03-context.md` §8、`docs/architecture/04-pipeline.md` §8

## 背景（Context）

两个未决问题：

1. **context 并发模型**：一个能力域 context 一次处理几个请求？Proto.Actor 的 actor 消息循环天然串行（安全、吞吐受限），vs 并行 Task 执行管道（吞吐高、需同步 context 状态）。
2. **管道配置热更新**：管道配置（插件顺序/参数）变更时，是重建整个管道还是原地更新？保留 actor/context 吗？与插件热重载（改插件代码）是两个不同维度。

## 决策（Decision）

### 决策 1：context 并发模型 — actor 串行（默认）+ 可选并行域

**默认采用串行**：能力域 actor 的消息循环一次处理一个任务，context 状态天然无竞争（Proto.Actor 保证），管道执行在 actor 处理循环内同步推进。

**预留并行扩展**：当某个能力域需要高吞吐时，该域可声明 `concurrency: parallel`，由管理层为该域 spawn 多个 worker 共享一个 context 或每个 worker 独立 context 快照——但这是**显式开启**的优化，不是默认行为。

### 决策 2：管道配置热更新 — 原子替换（swap），保留 actor/context

**采用"原子替换"**：配置变更 → 管理层基于当前 context 构建**新管道实例**（复用 context 中的服务/状态，不改动 context 本身）→ 原子切换引用 → 旧管道在途请求排空后销毁。

- 保留能力域 actor（长命锚点不变）
- 保留 context（状态不丢）
- 只替换管道（插件链）本身

## 理由（Rationale）

### 并发选串行

1. **正确性优先**：串行下 context 状态无竞争，无需锁/事务，设计期这是最大的简化——"状态放 context"（`03-context.md` §3）在串行下才真正免费。
2. **吞吐不是当前瓶颈**：agent harness 类系统的瓶颈是 LLM 调用（秒级），不是管道处理（微秒级）。串行吞吐完全够用。
3. **并行是渐进增强**：先串行跑对，再对具体热点域开并行，避免一开始就背着同步复杂度。

### 管道热更新选原子替换

1. **与插件热重载语义一致**：插件热重载 = 摘旧挂新（`02-plugin-model.md` §7），管道热更新 = 同一机制在管道层复用——配置变 = 重建管道链 = 换节点集合，机制统一。
2. **保留 context 是硬约束**：状态在 context（`03-context.md` §3），重建 context = 丢状态 = 违背状态外置原则。
3. **原子切换避免中间态**：管道替换瞬间在途请求要么走旧管道要么走新管道，不会出现"一半节点是新的、一半是旧的"。

## 权衡 / 风险（Trade-offs / Risks）

| 风险 | 说明 | 缓解 |
|------|------|------|
| 串行吞吐上限 | 单域串行处理，高并发场景受限 | 显式 `concurrency: parallel` 域扩展 |
| 管道切换在途请求 | 替换瞬间有请求正走旧管道 | 排空（drain）后销毁旧管道；新请求走新管道 |
| 配置校验 | 坏配置导致管道构建失败 | 配置层 schema 校验（启动期 fail-fast）+ 构建失败回滚旧管道 |

## 备选方案（Alternatives）

| 方案 | 描述 | 结论 |
|------|------|------|
| 并行 + 锁 | 管道并行执行，context 加锁 | 不采纳（默认）；复杂度前置，收益未验证 |
| 配置变重建 context | 管道配置变更 = 重建 actor + context | 不采纳；违背状态外置，丢状态 |
| 原地修改管道 | 在现管道上增删节点 | 不采纳；有中间态，难以回滚 |

## 影响（Consequences）

- `docs/architecture/03-context.md` §7 生命周期明确：context 长命、管道可换、插件短命。
- `docs/architecture/04-pipeline.md` 增加"管道原子替换"机制说明。
- 配置层 schema 需包含 `concurrency` 字段（串行/并行）与管道版本号（触发热更新）。

## 关联

- `docs/architecture/03-context.md` §3（状态外置）、§7（生命周期）、§8（待定）
- `docs/architecture/04-pipeline.md` §6（多实例）、§8（待定）
- ADR-0001（插件安全边界：同进程可信代码，管道热更新不引入进程边界）
