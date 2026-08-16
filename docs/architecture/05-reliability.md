---
type: architecture-doc
tags: [cordis-csharp, architecture, reliability]
created: 2026-08-15
---

# 05 — 可靠性

> 错误处理、监督策略、超时熔断、可观测性。运行期可靠性层。

## 1. 错误处理

### 管道异常策略

| 场景 | 策略 |
|------|------|
| 节点抛异常 | 短路返回错误（或回滚已执行节点，按插件性质定） |
| 观察者插件异常 | 不影响主链路，日志记录 |
| 内置执行器异常 | 返回失败结果，触发重试/熔断 |

错误处理中间件位置（ASP.NET Core 同款）：管道首部放 exception-handling，
尾部放 result-mapping。

### 插件崩溃隔离

| 故障 | 恢复粒度 | 说明 |
|------|---------|------|
| 插件死循环/卡死 | 重启插件 | 超时检测 → dispose 旧插件 → 加载新插件 |
| 插件抛异常 | 短路 + 记录 | 不重启（瞬态错误） |
| 能力域 actor 崩溃 | 监督重启 actor | Proto.Actor supervision |
| 进程级故障 | 管理层决策 | 整个域重启 or 进程退出 |

## 2. 监督策略（Proto.Actor supervision）

- OneForOne（默认）：子 actor 崩溃只重启该 actor
- AllForOne：需要一致性时，一损俱损

管理层（CompositionRoot）监督能力域 actor：
- 崩溃 → 按策略重启（保留 context 状态 or 重建）
- 重启计数 → 连续失败 N 次 → 标记不可用，告警

## 3. 超时与熔断

| 环节 | 超时 | 行为 |
|------|------|------|
| 管道节点 | 可配置（默认 Ns） | 超时 → 短路 + 错误 |
| LLM 调用 | 秒级（模型相关） | 超时 → 重试（幂等） |
| 插件初始化 | 启动超时 | 失败 → 禁用该插件 |
| 熔断 | 连续失败阈值 | 打开 → 快速失败 → 半开试探 |

CancellationToken 贯穿全链（接口第一版就带 token，后期加是破坏性变更）。

## 4. 重试与幂等

- 重试策略：指数退避 + 抖动
- **幂等性**：多实例跑同一任务，重试不得重复执行副作用
  - 副作用操作（写库/发消息）必须幂等（任务 ID 去重）
  - 这是"多实例跑不同任务"模型的正确性前提

## 5. 可观测性

### 链路追踪

- 每个请求/任务一个 RequestId（Guid），贯穿管道全链（06-contracts §3）
- 用 `System.Diagnostics.Activity` + DiagnosticSource 承载（对齐 Cordis trace/bind 的替代物，07 §2.3 第 5 项），Activity 携带 TaskId/ParentTaskId/能力域
- 日志格式：`{taskId} {pluginId} {phase} {elapsed}`
- 管道执行链路日志：每个节点 before/after + 耗时

### 指标

- 插件级：调用次数、失败率、延迟（p50/p95）
- 管道级：总耗时、节点分布
- 热重载审计：谁在什么时候重载了哪个插件，为什么（事件记录，含 quiesce 收敛结果）

### 日志

- **命名规则**：`ILoggerFactory.CreateLogger(category)`，category = `{能力域}/{插件 ID}`（对齐 Cordis 按 fiber 名自动命名，07 G11）——按插件过滤日志的前提
- **级别覆盖**：每插件经 `IOptions<T>` 命名选项覆盖 category 的 name/level（对齐 Cordis intercept 配置，07 G12）
- **provider 接线清单**（P2-18 修正，对齐 Cordis 核心：核心默认仅内存缓冲，console 属生态包 opt-in）：
  默认零 provider（`ILoggerFactory` 空转缓冲，测试/嵌入方安静）；Console / File（滚动）/ 结构化 exporter
  均为可选 `ILoggerProvider`，由嵌入方经 `ServiceOptions` 显式接线
- **结构化日志记录模型**（对齐 Cordis Message 结构 / ADR-0004 显式序列化契约）：

  | 字段 | 类型 | 说明 |
  |------|------|------|
  | Timestamp | DateTimeOffset | 记录时间 |
  | TaskId / ParentTaskId | Guid? | 任务关联（链路追踪键） |
  | Category | string | 能力域/插件 ID |
  | Level | LogLevel | 级别 |
  | Phase | string | 管道阶段（before/after/error） |
  | Elapsed | TimeSpan? | 节点耗时 |
  | Message | string | 格式化消息 |

- ILogger 注入 context；插件必须通过 `ctx.Logger`（10-plugin-sdk §4），不直接 console

## 6. 测试策略

| 层 | 测什么 | 方式 |
|----|--------|------|
| 插件单测 | 插件逻辑 | 不启动整个域 |
| 管道集成 | 挂假插件跑管道 | in-memory 宿主（TestServer 模式） |
| 热重载 | 重载后旧 ALC 被回收 | 专门测试：加载→注册→dispose→断言回收 |
| 并发 | 多实例并行任务 | 竞争测试 |
| 端到端 | 真实插件 + 真实执行 | 真实宿主 |

**热重载回收测试是硬门**——测不好，热重载就是纸面能力。

## 7. 待定项收敛（2026-08-15）

原待定两项已收敛为决策（实现期不再悬空）：

- **回滚语义（已决：默认不回滚）**：管道节点失败 → 错误中间件短路返回（§1），**不做自动回滚**；事务边界由插件显式声明——插件需要补偿时实现宿主提供的补偿接口（如 `ITransactional`，在管道入口声明参与事务），宿主按声明顺序执行补偿。理由：自动回滚对"只读/幂等副作用"是纯开销，事务场景是少数显式声明更清晰。
- **重试幂等键（已决：TaskId 默认，插件级扩展可选）**：默认 TaskId 即幂等键（06-contracts §4）；插件级幂等键（业务自然键）作为显式扩展——插件可实现 `IIdempotencyKeyProvider` 提供业务键，宿主在重试去重时优先使用。第一版仅实现 TaskId 去重，插件级键留接口不进默认实现。
