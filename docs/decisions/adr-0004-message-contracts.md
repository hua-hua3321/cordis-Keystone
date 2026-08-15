---
type: adr
tags: [cordis-csharp, decisions, contracts, payload, orchestration]
created: 2026-08-15
status: accepted
---

# ADR-0004：消息契约完整化 — Payload 序列化 + 跨域编排

> 决策状态：**accepted**（2026-08-15）
> 关联待定项：`docs/architecture/06-contracts.md` §6

## 背景（Context）

两个未决问题（`docs/architecture/06-contracts.md` §6）：

1. **Payload 序列化**：管道请求的 Payload 用强类型 record（编译期类型安全）还是动态容器（MessagePack/JSON，跨进程可序列化）？
2. **跨域编排**：一个任务跨多个能力域时，TaskId 怎么传递？子任务怎么聚合、结果怎么合并、失败怎么传播？

## 决策（Decision）

### 决策 1：Payload — 强类型 record（域内）+ 序列化契约（跨域边界）

**域内：强类型 record**。每个能力域定义自己的 Operation + Payload 类型（`06-contracts.md` §5），编译期类型安全，插件只处理本域 Operation。

**跨域边界：显式序列化契约**。当任务跨越能力域边界（actor 间消息）时，Payload 序列化为 MessagePack（默认）或 JSON（可配置）——**在契约接口上声明，不在代码里隐式序列化**。

- 每个跨域消息类型声明 `[MessagePackObject]`（或契约标记），序列化是显式边界行为
- 域内保持强类型直接调用（`01-overview.md` §6 的克制边界：不强制全 actor 化）

> **实现备注（2026-08-15，P17，见 14-implementation-log §7.17 / ID-15）**：序列化动作经 `IContractSerializer` 抽象（`Keystone.Core/Serialization/`）——默认 `MessagePackContractSerializer`，可注入 `JsonContractSerializer`（STJ 源生成上下文，调试/审计）。跨域边界当前走 Proto.Actor 同进程引用传递（无实际序列化，`[MessagePackObject]` 保留为契约声明）；抽象的首个消费点是事件持久化（`FileEventStore` 构造器注入，ADR-0009）。AOT 安全：仅源生成实现，禁反射。

### 决策 2：跨域编排 — TaskId 贯穿 + 子任务聚合（父任务等待全部子任务）

**TaskId 贯穿**：父任务创建子任务时，子任务携带 `ParentTaskId`，TaskId 链贯穿整个编排树（`06-contracts.md` §3 的链路追踪扩展）。

**子任务聚合**：父任务等待全部子任务完成（fan-out/fan-in）：
- 全部成功 → 聚合结果（按子任务 ID 索引），父任务完成
- 任一失败 → 父任务失败（默认），失败传播携带失败子任务 ID + 错误码
- 取消 → 子任务级联取消（CancellationToken 传播，`05-reliability.md` §3）

**失败传播路径**：子任务失败 → 父任务标记失败 → 沿 TaskId 链向上传播 → 最外层调用方收到根失败。

## 理由（Rationale）

### Payload 选强类型 + 显式契约

1. **域内强类型保住类型安全**：`C210`（动态插件服务注册）已确立"禁 Dictionary 退化"，Payload 同理——域内强类型 record，编译期检查。
2. **跨域显式序列化边界**：actor 间消息必然经过序列化（Proto.Actor 的消息传递），在契约上声明 = 边界显式化，避免"哪一层在序列化"的隐式歧义。
3. **MessagePack 默认**：比 JSON 紧凑（二进制）、比 JSON 快；JSON 可配置用于调试/日志。

### 编排选 TaskId 贯穿 + 全等聚合

1. **与幂等契约一致**：TaskId 即幂等键（`06-contracts.md` §4），贯穿链保证重试不重复副作用。
2. **全等聚合简单正确**：全部成功才成功（严格），比部分成功（宽松）简单且可预测；部分成功场景后期可按域显式放宽。
3. **级联取消**：子任务随父取消，避免孤儿任务（`05-reliability.md` §5 可观测性需要）。

## 权衡 / 风险（Trade-offs / Risks）

| 风险 | 说明 | 缓解 |
|------|------|------|
| 强类型 Payload 跨域受限 | 域间类型不共享，需定义共享 DTO | 共享契约包（cordis-contracts），`C208` 依赖共享第 6 条（走宿主接口/DTO 中转） |
| 全等聚合过严 | 部分成功场景父任务整体失败 | 默认严格，按域显式放宽为部分成功 |
| 序列化成本 | 跨域消息序列化开销 | 域内不序列化（直接调用），仅边界序列化；MessagePack 二进制紧凑 |

## 备选方案（Alternatives）

| 方案 | 描述 | 结论 |
|------|------|------|
| 动态容器（MessagePack/JSON 全链路） | 所有 Payload 动态化 | 不采纳；域内动态化 = 丢类型安全 |
| 部分成功聚合 | 父任务容忍部分子任务失败 | 不采纳（默认）；显式放宽按域 |
| 无序列化契约（隐式） | 跨域不声明，靠运行时 | 不采纳；边界不显式 = 调试噩梦 |

## 影响（Consequences）

- `docs/architecture/06-contracts.md` §1 请求模型增加 `ParentTaskId` 字段。
- 新增共享契约包（`cordis-contracts`）承载跨域 DTO 与序列化契约。
- 编排树需要子任务聚合器（fan-in）实现，挂在管理层或编排插件上。

## 关联

- `docs/architecture/06-contracts.md` §1（请求模型）、§3（TaskId 链路）、§4（幂等）、§6（待定）
- `docs/architecture/05-reliability.md` §3（超时熔断）、§4（重试幂等）
- ADR-0003（context 并发模型：串行默认，跨域编排在串行域间传递消息）
- C208（依赖共享）、C210（服务注册）
