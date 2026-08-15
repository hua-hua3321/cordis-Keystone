---
type: adr
tags: [cordis-csharp, decisions, events, dispatch-mode]
created: 2026-08-15
status: accepted
---

# ADR-0006：事件分发模式全集 — serial/bail 纳入

> 决策状态：**accepted**（2026-08-15）
> 关联待定项：`docs/architecture/04-pipeline.md` §3、`docs/architecture/06-contracts.md` §2、`docs/architecture/03-context.md` §4
> 来源：`docs/architecture/07-cordis-migration-gap.md` 差距 G10（P0）

## 背景（Context）

迁移差距分析（`07-cordis-migration-gap.md` §2.4）对照 Cordis 源码发现五种分发模式（events.ts:32）：`emit / parallel / serial / bail / waterfall`。当前 C# 设计只覆盖了三种：

- waterfall（管道，`04-pipeline.md` D4）
- parallel / emit（观察者事件，`03-context.md` §4）

**serial 与 bail 明确缺失**，且当前文档把"事件 = parallel/emit"固化成双轨，等于默认丢弃 serial/bail 且**没有显式决策记录**——这是遗漏不是设计选择（07 G10，P0）。

Cordis 语义（events.ts）：

- serial：异步按序 await，遇到第一个 bail 值（非 null/false/undefined）即停并返回（events.ts:204-209）
- bail：同步按序，第一个非空返回值即停（events.ts:217-222）
- 框架内部事件按模式选型：`internal/listener` = bail（监听注册可被替换）、`internal/update`/`internal/get`/`internal/set` = waterfall（可否决/可拦截）、`internal/dispatch` = emit（诊断）

## 决策（Decision）

**采纳完整五种分发模式**，事件轨补 serial/bail：

| 模式 | await | 顺序 | 返回值 | C# 语义 |
|------|-------|------|--------|---------|
| emit | 否 | 注册序 | 无 | fire-and-forget，忽略返回值 |
| parallel | 是 | 并发 | 无 | Task.WhenAll + 错误聚合 |
| serial | 是 | 注册序 | 有 | 按序 await，首个 bail 值短路返回 |
| bail | 否 | 注册序 | 有 | 同步按序，首个非空短路 |
| waterfall | 否 | 注册序 | 有 | 包裹 next 链，不调 next 即否决 |

- `EventsService` 的形状是委托链（handler 注册 + 分发聚合函数），serial/bail 各加一个聚合函数即可，实现成本低；不新增技术项（00-tech-stack.md T1-T9 范围内）
- **框架内部事件按模式选型**：配置更新可否决 → waterfall；监听注册可替换 → bail；诊断 → emit；**不得让所有 internal 事件默认 waterfall**（07 §2.4 第 2 条）

## 理由（Rationale）

1. **策略型事件天然需要 serial**：权限检查链"第一个拒绝者决定结果"是典型语义；塞进管道（waterfall）语义错位——waterfall 是包裹式，无法表达"监听链上第一个返回决策者生效"。
2. **与 Cordis 语义等价是 07 的判定标准**：五种分发模式是事件契约面的一部分（harness `cordis-primer.zh.md` 分发模式表即五种），06-contracts 契约冻结前必须定对。
3. **实现成本低**：委托链聚合函数（按序 await / 同步按序短路），不引入新技术项。
4. **bail 是框架内部机制的基础**：监听注册可替换（internal/listener 等价物）需要 bail，弃 bail 会连带丢框架内部 handler 替换语义。

## 权衡 / 风险（Trade-offs / Risks）

| 风险 | 说明 | 缓解 |
|------|------|------|
| 五种模式增加 API 面 | 事件轨多两种分发聚合 | 与 C# 生态惯例对齐（按序 await / Task.WhenAll 均为原生能力），EventsService 按模式聚合函数分型 |
| serial/bail 与管道语义重叠 | 决策型事件可选管道或 serial | `06-contracts.md` §2 口诀修订（见影响）：要结果+首个决策 → serial/bail；要包裹/否决 → waterfall；只观察 → parallel/emit |
| 模式误用 | 开发者选错分发模式 | 事件声明时显式标注模式（对齐 harness `@mode` 标签），口诀固化进 06-contracts §2 |

## 备选方案（Alternatives）

| 方案 | 描述 | 结论 |
|------|------|------|
| A（采纳） | 五种模式全集，serial/bail 纳入事件轨 | **采纳**：语义等价 Cordis，策略事件有正确落点 |
| B | 声明弃用 serial/bail，策略事件必须走管道 | 不采纳：语义错位，契约冻结前应定对（07 §2.4 方案 B 亦否决） |
| C | 只补 serial，弃 bail | 不采纳：bail 是框架内部 handler 替换的基础，弃掉会连丢内部机制 |

## 影响（Consequences）

- `docs/architecture/06-contracts.md` §2 判断口诀修订：要顺序+首个决策 → serial/bail；要包裹/否决 → waterfall；只观察 → parallel/emit
- `docs/architecture/04-pipeline.md` §3 双轨模型补 serial/bail 两档（策略型事件走事件轨 serial/bail，不再被迫进管道）
- `docs/architecture/03-context.md` §4 事件分层表补"分发模式"列
- EventsService 接口面：按模式分方法（`Emit`/`EmitParallel`/`EmitSerial`/`EmitBail`/`Waterfall`）或 `Subscribe<T>(DispatchMode, handler)`，实现期在插件 SDK 定
- `docs/decisions/README.md` 索引增补 ADR-0006

## 关联

- `docs/architecture/07-cordis-migration-gap.md` §2.4 / G10（来源）
- `docs/architecture/04-pipeline.md` §3（双轨模型）、`docs/architecture/06-contracts.md` §2（判断口诀）、`docs/architecture/03-context.md` §4（事件分层）
- ADR-0004（事件与请求的分界判断，本 ADR 是其分发模式补全）
