---
type: architecture-doc
tags: [cordis-csharp, architecture, overview]
created: 2026-08-15
---

# 01 — 方案总览

> Cordis C# 版的三层架构。本文是架构总纲，细节见各专题文档。

## 1. 核心思想

Cordis（JS）的核心是可组合性：一切皆插件，插件贡献服务、类型化事件、可逆 effects 到共享 context。
C# 版保留这个组合纪律，但用 .NET 原生能力替代 JS 动态特性，并引入 JS 版本没有的生命周期管理（监督树、热重载）。

**不重造**：DI（IServiceProvider）、中间件管道（ASP.NET Core 形状）、配置（IOptions）、日志（ILogger）、后台服务（IHostedService）。
**只实现**：ALC 插件加载层、按插件 ID 分组的注册回收、管道配置 schema、插件 SDK。

## 2. 三层架构

```
┌─────────────────────────────────────────────────────┐
│ 配置层（Configuration Layer）                         │
│   插件清单（plugin-id + 单文件 .cs + 依赖白名单 + 版本）│
│   能力域定义（capability domain → actor 映射）        │
│   管道组成（中间件顺序、scope 父子关系）                │
├─────────────────────────────────────────────────────┤
│ 管理层（Management Layer / CompositionRoot actor）   │
│   读配置 → 为每个能力域 spawn 能力域 actor            │
│   插件编译（Roslyn 内存编译）                         │
│   插件加载（私有 ALC，依赖 fallback 到 Default）       │
│   热重载（FileSystemWatcher → 重编译 → 摘旧挂新）     │
│   监督（能力域 actor 崩溃 → 重启策略）                 │
├─────────────────────────────────────────────────────┤
│ 能力域 actor（Capability Domain Actor）              │
│   持 context + 管道（中间件链）                       │
│   管道（waterfall）：主请求链，插件=中间件             │
│   事件（parallel/emit）：观察者插件，监听不干预        │
│   context：管道+事件共享的状态容器，插件无状态          │
└─────────────────────────────────────────────────────┘
```

## 3. 生命周期模型

| 实体 | 生命周期 | 说明 |
|------|---------|------|
| 能力域 actor | 长命 | 管理层 spawn，崩溃由监督重启 |
| context | 与 actor 同生命周期 | 一个域 = 一个 actor = 一个 context |
| 插件 | 短命 | 挂载在 context 上，可热重载替换 |
| 管道 | 与 actor 同生命周期 | 节点（插件）可换，管道本身不换 |

**核心分层**：context 长命，插件短命。热重载 = 摘旧插件换新插件，actor 和 context 不动。

## 4. 多实例模型

同一能力域可创建多个实例，各自独立执行不同任务：

```
配置层：capability-domain 定义 + instance 数量 + scope 父子关系
  → 管理层读配置 → 同一能力域 spawn N 个 actor
  → 每个 actor 独立 context（独立子容器/独立作用域链）
  → 事件在各自 context 链上路由，互不冲突
```

隔离机制（详见 03-context.md）：
- 每实例独立 context（组合而非继承，注册表互不写回）
- 服务隔离：每实例独立子 IServiceProvider
- 事件隔离：context filter + scope 父子链

## 5. 关键设计决策（摘要，详情见各文档）

| # | 决策 | 理由 | 文档 |
|---|------|------|------|
| D1 | 接口白名单而非 Dictionary<string, object> | 保住编译期类型安全 | 02-plugin-model.md |
| D2 | 键控服务 + 子容器组合 | 类型安全 + 多实例隔离 + 热重载回收 | 02-plugin-model.md |
| D3 | context 作用域链 = 类继承骨架 + IFeatureCollection shadow + IServiceScope 父子链 | 各取所长，不造轮子 | 03-context.md |
| D4 | 管道（waterfall）+ 事件（parallel/emit）双轨 | 请求链走管道，观察者走事件 | 04-pipeline.md |
| D5 | 热重载 = Roslyn 内存编译 + 私有 ALC + disposer 协议 | C# 社区标准姿势 | 02-plugin-model.md |
| D6 | 状态外置：插件无状态，状态在 context | 热重载不丢状态 | 03-context.md |

## 6. 明确不做（克制边界）

- 不重造 DI 容器 / 中间件框架 / 配置系统 / 日志系统
- 不做全 actor 化（高频紧密调用留在域内直接调用，不走消息）
- 不强制"一切皆插件"到 UI 层
- 不引入 JS 生态兼容层

## 7. 待定决策

- 安全边界：插件可信代码（同进程）vs 隔离进程
- 插件来源：本地文件 vs 远程分发（版本/签名）
- AOT vs JIT：Roslyn 动态编译与 NativeAOT 互斥
- context 并发模型：串行处理（吞吐受限）vs 并行 + 同步
