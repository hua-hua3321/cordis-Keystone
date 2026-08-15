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

| 特征 | 走管道（请求-响应） | 走事件（广播） |
|------|-------------------|---------------|
| 需要结果 | 是（调用方等结果） | 否（fire-and-forget） |
| 干预执行 | 是（可以短路/修改） | 否（只监听） |
| 顺序敏感 | 是（管道顺序执行） | 否（并发/无序） |
| 持久需要 | 按事实事件持久 | 可选 |

**判断口诀**：要结果、要干预、要顺序 → 管道；否则 → 事件。

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

## 6. 待定

- Payload 强类型 vs 动态（MessagePack/JSON）——跨进程边界时需要序列化
- 多能力域编排：一个任务跨多个域时，TaskId 传递与子任务模型
