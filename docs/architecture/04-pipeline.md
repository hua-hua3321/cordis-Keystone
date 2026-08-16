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

C# 实现形状（**已定案：形状 A**）：

```csharp
// 形状 A：ASP.NET Core 风格（采纳）
public interface IMiddleware
{
    Task InvokeAsync(IPluginContext ctx, RequestDelegate next);
}

// 形状 B：闭包风格（弃用）
// Func<IPluginContext, Func<Task>, Task>
```

**采纳形状 A**：清晰、与 .NET 生态习惯一致、便于框架包装（日志/超时/异常处理可在 InvokeAsync 外包一层）；闭包风格留给宿主内部扩展点，不进插件 SDK。

**形状 A/B 的分工澄清**：形状 A（`IMiddleware`）是**插件 SDK 接口面**；宿主内部组合实现使用形状 B 闭包（`List<Func<IPluginContext, Func<Task>, Task>>` 反向包装成链，ASP.NET Core 同款组合）——**动态管道组合（运行期插入节点 → 组合 → 执行）即编程式挂载插件的执行机制**（对应 Cordis `ctx.plugin()`，见 12-cordis-semantics-mapping.md §7.2）。A 是公开接口，B 是内部实现，两者不冲突。

## 3. 双轨模型（决策 D4）

不是所有插件交互都是管道式的。三类插件区分：

| 类型 | 走哪 | 语义 | 例子 |
|------|------|------|------|
| 管道插件 | 管道（waterfall） | 请求链上的 before/after/短路 | 校验、限流、日志 |
| 决策插件 | 事件（serial/bail） | 注册序执行，首个决策者生效 | 权限检查链、handler 选择 |
| 观察者插件 | 事件（parallel/emit） | 监听不干预 | 遥测、审计、指标、事件记录 |

**强制观察者插件进管道 = 白白增加每个请求的延迟和耦合。**
**强制决策插件进管道 = 语义错位**（waterfall 是包裹式，无法表达"第一个返回决策者生效"）。
三轨是 Cordis 验证过的设计（按插件性质选分发模式，ADR-0006）。

## 4. 中间件接口设计

```csharp
public interface IMiddleware
{
    string Id { get; }                       // 插件 ID
    int Order { get; }                       // 管道顺序
    Task InvokeAsync(IPluginContext ctx, RequestDelegate next);
}
```

管道声明顺序（设计期原案·配置层 YAML；**预留未实现**——`EntryOptions` 尚无 pipeline 字段，落地形态 = `CapabilityDomain.Spawn(middlewares)` 代码传入 + `SwapPipelineAsync` 原子热换，见 00 §3.5 实现备注）：

```yaml
pipeline:
  - id: plugin-auth        # 权限校验
  - id: plugin-rate-limit  # 限流
  - id: plugin-logging     # 请求日志
  - id: builtin-executor   # 内置执行器（终点）
```

## 5. 短路与错误

- 插件短路：不调用 `next(ctx)`，直接设置结果返回
- 异常：抛异常 → 管道错误处理（见 05-reliability.md）
- after 语义：`await next(ctx)` 之后的代码即 after（ASP.NET Core 同款；`RequestDelegate` 携 ctx）

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

## 8. 已决决策（ADR-0003/0004/0006）

- **管道配置热更新**：原子替换（swap）——配置变更 → 基于当前 context 构建新管道实例 → 原子切换引用 → 旧管道在途请求排空后销毁；保留 actor/context，只换管道链（ADR-0003）
- **管道节点粒度**：一个插件 = 一个中间件节点（单节点），复杂能力通过组合多个插件实现
- **中间件形状**：形状 A（`IMiddleware`，ASP.NET Core 风格），闭包形状弃用（§2 定案）
- **三轨分发**：管道插件（waterfall）/ 决策插件（serial/bail）/ 观察者插件（parallel/emit）（ADR-0006）
