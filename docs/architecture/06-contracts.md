---
type: architecture-doc
tags: [cordis-csharp, architecture, contracts]
created: 2026-08-15
---

# 06 — 消息契约

> 管道里传什么、怎么追踪、请求与事件的分界。

## 1. 请求模型

进入管道的任务/消息类型（第一版定义，接口层）：

```csharp
public sealed record TaskRequest(
    Guid TaskId,           // 全局唯一（幂等键）
    Guid? ParentTaskId,    // 父任务 ID（跨域编排树，ADR-0004 决策 2）
    string Capability,     // 能力域名
    string Operation,      // 操作名
    object? Payload,       // 业务载荷（强类型，按能力域约束）
    CancellationToken CancellationToken  // 取消传播贯穿全链
);

public sealed record TaskResult(
    Guid TaskId,
    bool Succeeded,
    TaskResultType Type,   // Completed/Failed/Cancelled
    object? Data,          // 成功载荷
    string? ErrorCode,     // 失败码
    string? ErrorDetail    // 失败详情
);
```

## 2. 请求-响应 vs 事件 的分界（判断标准）

| 特征 | 走管道（waterfall） | 走事件（serial/bail） | 走事件（parallel/emit） |
|------|-------------------|----------------------|------------------------|
| 需要结果 | 是（调用方等结果） | 是（首个决策者生效） | 否（fire-and-forget） |
| 干预执行 | 是（包裹/否决/短路） | 是（首个决策者决定） | 否（只监听） |
| 顺序敏感 | 是（管道顺序执行） | 是（注册序，首个短路） | 否（并发/无序） |
| 语义形态 | 中间件链（await next） | 决策链（首个返回值生效） | 观察者（监听不干预） |
| 持久需要 | 按事实事件持久 | 可选 | 可选 |

**判断口诀**（ADR-0006 修订）：
- 要**包裹/否决**（before/after/短路整条链）→ **管道（waterfall）**
- 要**顺序 + 首个决策**（第一个返回决策者生效，如权限检查链）→ **事件（serial/bail）**
- 只**观察**（遥测/审计，不干预）→ **事件（parallel/emit）**

## 3. 请求 ID / 链路追踪

- 每个任务一个 `TaskId`（Guid），贯穿管道全链
- 观察者事件也携带 TaskId（遥测/审计按任务关联）
- 日志格式强制：`{taskId} {pluginId} {phase} {elapsed}`
- 多实例并发时，TaskId 是区分不同请求的唯一线索

## 4. 幂等契约

- TaskId 即幂等键
- 副作用操作（写库/发消息）以 TaskId 去重
- 重试不得重复执行副作用（见 05-reliability.md）

## 5. 能力域契约

每个能力域定义自己的 Operation + Payload 类型：

```csharp
// 示例：fs 能力域
public sealed record FsReadRequest(Guid TaskId, string Path, CancellationToken CT)
    : TaskRequest(TaskId, "fs", "read", new FsReadPayload(Path), CT);
```

- 能力域接口白名单（宿主定义）约束 Payload 类型
- 插件只处理本能力域的 Operation，未知 Operation → 错误

## 6. 已决决策（ADR-0004/0006）

- **Payload**：域内强类型 record（编译期类型安全）；跨域边界显式序列化契约（MessagePack 默认 / JSON 可配置），在契约接口上声明（ADR-0004）
- **跨域编排**：TaskId 贯穿（子任务携带 ParentTaskId）+ 子任务全等聚合（fan-out/fan-in，全部成功才成功，任一失败父任务失败，取消级联传播）（ADR-0004）
- **事件分发模式全集**：emit/parallel/serial/bail/waterfall 五种模式全部纳入；策略型事件（首个决策生效）走 serial/bail，包裹/否决走 waterfall（管道），观察走 parallel/emit（ADR-0006）
- **跨域编排实现层（ADR-0008）**：ADR-0004 决策不变（TaskId 贯穿 + 全等聚合），**实现层组合 MAF Workflows**（`Microsoft.Agents.AI.Workflows`，fan-out/fan-in + checkpoint + 取消级联现成），不自研编排器；TaskId/ParentTaskId 语义不得被组合实现稀释
