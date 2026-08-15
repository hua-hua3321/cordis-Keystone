---
type: architecture-doc
tags: [cordis-csharp, architecture, pipeline]
created: 2026-08-15
---

# 04 — 管道设计

> 中间件管道（waterfall 语义）+ 事件双轨。决策 D4。

## 1. 核心模型

所有插件在能力域内挂载在**管道**上，类比 ASP.NET Core 中间件管道：

```
Task/Message → [插件A] → [插件B] → [插件C] → 内置执行器
                  ↑           ↑           ↑
                  └────── 共享 context（= HttpContext）──────┘
```

- **插件 = middleware**：可 before（处理前拦截）、after（处理后收尾）、
  short-circuit（短路直接返回不往下走）
- **context = HttpContext**：管道执行期间的状态容器
- **状态放 context** = 中间件往 HttpContext.Items/Features 塞东西（成熟模式）

## 2. 与 Cordis waterfall 的对应

Cordis `DispatchMode = 'waterfall'` 语义："每个 listener 包裹链的其余部分，
调用 next() 放行，不调用就否决"。这就是 middleware pipeline 的另一种叫法。

C# 实现形状（两种可选，早期定）：

```csharp
// 形状 A：ASP.NET Core 风格（清晰）
public interface IMiddleware
{
    Task InvokeAsync(IPluginContext ctx, RequestDelegate next);
}

// 形状 B：闭包风格（灵活）
// Func<IPluginContext, Func<Task>, Task>
```

建议形状 A——清晰，且与 .NET 生态习惯一致。

## 3. 双轨模型（决策 D4）

不是所有插件交互都是管道式的。两类插件区分：

| 类型 | 走哪 | 语义 | 例子 |
|------|------|------|------|
| 管道插件 | 管道（waterfall） | 请求链上的 before/after/短路 | 校验、权限、限流、日志 |
| 观察者插件 | 事件（parallel/emit） | 监听不干预 | 遥测、审计、指标、事件记录 |

**强制观察者插件进管道 = 白白增加每个请求的延迟和耦合。**
双轨是 Cordis 验证过的设计（按插件性质选分发模式）。

## 4. 中间件接口设计

```csharp
public interface IMiddleware
{
    string Id { get; }                       // 插件 ID
    int Order { get; }                       // 管道顺序
    Task InvokeAsync(IPluginContext ctx, RequestDelegate next);
}
```

管道配置声明顺序（配置层）：

```yaml
pipeline:
  - id: plugin-auth        # 权限校验
  - id: plugin-rate-limit  # 限流
  - id: plugin-logging     # 请求日志
  - id: builtin-executor   # 内置执行器（终点）
```

## 5. 短路与错误

- 插件短路：不调用 next()，直接设置结果返回
- 异常：抛异常 → 管道错误处理（见 05-reliability.md）
- after 语义：await next() 之后的代码即 after（ASP.NET Core 同款）

## 6. 多实例与管道

同一能力域 N 个实例 = N 条平行管道，互不干扰：

```
实例1: TaskA → [插件A] → [插件B] → 执行器
实例2: TaskB → [插件A] → [插件B] → 执行器
        （各自独立 context，管道节点相同但注册互不可见）
```

管道节点（插件）可换（热重载），管道本身和 context 不换。

## 7. 观察者事件（parallel/emit）

```csharp
// 观察者插件：订阅事件，不干预管道
ctx.Events.Subscribe<TaskStarted>(static e => telemetry.RecordStart(e.TaskId, e.Timestamp));
ctx.Events.Subscribe<TaskCompleted>(static e => telemetry.RecordEnd(e.TaskId, e.Result));
```

事件模型（详见 03-context.md 事件分层）：
- 事实事件：持久（任务完成/失败）
- 拦截事件：waterfall（管道即此类）
- 策略事件：parallel/emit（观察者）

## 8. 待定

- 管道配置热更新：配置变 → 重建管道？保留 actor/context？（与插件热重载不同维度）
- 管道节点粒度：一个插件一个节点，还是一个插件可贡献多个节点？
