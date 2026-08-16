---
type: architecture-doc
tags: [cordis-csharp, architecture, context]
created: 2026-08-15
---

# 03 — Context 设计

> context 的作用域链、状态外置、事件分层。决策 D3/D6。

## 1. Context 是什么

context = 管道 + 事件共享的状态容器。类比 ASP.NET Core 的 HttpContext：
- 插件（中间件）往 context 读/写
- 请求/任务结束 context 回收
- 状态放 context，插件无状态

**一个能力域 = 一个 actor = 一个 context**，三者同生命周期。

## 2. 作用域链（决策 D3）

Cordis 的 `extend()` 是原型继承 + 属性 shadow（子 context 覆盖父属性）。
设计期原案用三层混合实现（**落地未采用，见代码块后实现备注**）：

```
固定骨架（类继承）：
  RequestContext : TurnContext : SessionContext   ← 编译期、性能好、虚方法覆盖

动态插件注册（IFeatureCollection 语义）：
  ctx.Set<IFsProvider>(pluginA的)  → 覆盖父层的
  ctx.Get<IFsProvider>()           → 取最近一层   ← shadow 语义官方实现

服务解析链（IServiceScope 语义）：
  scope 内先查自己，再查父 scope                       ← 父子链官方实现
```

- 类继承：适合固定层级（Session/Turn/Request），编译期绑定性能好
- IFeatureCollection：按 Type 键、后注册覆盖先注册——shadow 语义的官方实现
- IServiceScope：scope 查找先自己再父——父子链的官方实现

> **实现备注（2026-08-16，按代码核对）**：落地形态 = `ContextFacade` 父子链（子复用父的事件总线/服务 store/logger 工厂，自身独立 Effect/请求 CT 槽）+ isolate map 沿链推导 realm + 进程级共享 `KeyedServiceStore`——"子覆盖父、父不被改"由 isolate map 影子覆盖承载（P21/P57；02 §3/00 §3.3 已同步）。下文 §2.1-§2.3 的 rebind/属主语义在落地形态上继续成立。

**不需要自己实现"作用域链查找"**——三者组合即 Cordis extend() 的完整语义。

边界用例（同名服务不同 scope）：**默认共享**（realm=""，人人命中同一键）；隔离经条目
isolate 显式声明——`{name: true}` 私有域（#entryId）、`{name: "label"}` 命名共享域（@label）。
（2026-08-16 P54 按 Cordis 源码修正：Cordis 默认共享，非"每实例独立 scope 根"。）

> **实现备注（2026-08-16 P57 更新，原 P21/DEV-02/ID-18）**：服务解析链落地形态——进程级单一 `KeyedServiceStore`（键 = (服务名, realm)），`ContextFacade.Provide` 写**本条目生效 realm** 的键（isolate map 沿谱系解析：组声明 #groupId / 叶自声明 #leafId / @label / 无声明 ""），返回删键 disposer；`Get/TryGet` = 算 realm + 查共享 store（同一 map，门控域 == 解析域）。插件服务跨插件可见性由 realm 决定（"" 默认互见；私有/命名域按声明），属主校验保留（§2.3），值卸载 = disposer 删键（G-C3）。

### 2.1 rebind 语义（G14 决策）

**同 scope 内重复注册同一服务名 = 报错**（对齐 Cordis "service X has been registered"，reflect.ts:289-291），不采用 MS.DI 的静默 last-wins：

- "覆盖父级"只能通过**新建子 scope**（isolate/extend）提供新实现——父不被改
- 与 02-plugin-model §3"同一服务名同 scope 重复注册 = 报错"一致
- 意义：热重载"摘旧挂新"时不会静默替换，污染可被立即发现

### 2.2 服务级隔离（isolate，G7 决策）

`IServiceScope` 是整 scope 隔离，粒度过粗；Cordis 可对**单个服务名**建独立 scope（`isolate('fs', label)`，同 label 共享、不同 label 隔离）。C# 对应：

- 键 = (服务名, realm)，单一共享 store（Cordis `reflect.store` 对应物）；realm ∈ {"" 默认共享, "#entryId" 私有, "@label" 命名共享}
- 多实例模型下"实例 A 用 fs-A、实例 B 用 fs-B"只隔离 fs 服务，**不连带隔离其他服务**
- **默认共享**（realm=""，对齐 Cordis：未 isolate 的名字回落到 root 默认符号）；隔离按需声明（配置层 `isolate` 字段：true=私有 / "label"=命名共享）
- **isolate 变更语义**（F10，P57-T5 已落地）：条目的 isolate 声明变化 → 生效 realm 变（ConfigDiffer 结构键按生效域比对——组级声明变化传播到组内叶子）→ 受影响条目冷重启 + 依赖方按域重评（ADR-0007）；**跨 realm 服务转移优化明确不实现**（ID-11：多实例隔离靠 realm 键天然达成，转移是性能优化非语义必需）；变更通知经 09 §5 PatchContext waterfall 接线

### 2.3 set 属主校验（G8 决策）

`IFeatureCollection.Set` 无属主概念、任意覆盖。C# context 门面加**服务属主**薄封装：

- 服务注册记录提供插件 ID；set 时校验属主，非属主修改 = 抛错（对齐 Cordis reflect.ts:254-265）
- 热重载时新旧插件无法互相改服务值，防止静默污染

## 3. 状态外置（决策 D6）

```
能力域 actor（持 context）
  ├─ 插件：无状态，处理时从 ctx 读/写
  └─ context：状态容器（服务 + 数据），长命
```

红利：
- 热重载不丢状态（状态在 context 不在插件）
- 多实例天然隔离（每实例独立 context）
- 状态生命周期与 actor 一致，无孤儿状态

## 4. 事件分层（三类事件）

| 类型 | Cordis 对应 | 分发模式 | 用途 | 持久化 |
|------|------------|---------|------|--------|
| 事实事件（session events） | durable facts | emit | 必须存活（任务完成/失败） | append-only 事件日志（ADR-0009，P10 已实现：IEventStore + 文件/内存实现 + 重放 + 迁移） |
| 拦截事件（agent events） | waterfall | waterfall | 拦截在途工作（pre-step/request） | 不持久 |
| 策略事件（capability events） | serial/bail / parallel/emit | 决策型 serial/bail，观察型 parallel/emit | 附加策略不碰循环（fs/* tools/*） | 可选 |

**扩展点选错领域是最大的架构错误**——事件归属必须在设计文档显式声明。

## 5. 事件隔离（多实例不冲突）

Cordis 的三层隔离（源码验证）：
1. context 原型链继承（extend 子 context 不写回父）
2. isolate(name, label)：独立服务作用域，同 label 共享、不同 label 隔离
3. 事件监听带 context filter（Hook 记录 ctx，分发时检查，global:true 才跳过）

C# 版对应：
- 每实例独立 context（组合而非继承，注册表互不写回）
- 服务注册走 isolate（每实例独立子容器）
- 事件监听走各自 context 链（事件路由按 context/scope 过滤）
- scope 父子关系在配置层显式声明（共享事件挂公共父 scope）

**事件过滤实现形状**（防跨实例泄漏）：hook 注册时记录所属 context；分发时按 scope 链过滤（监听者 context 是分发者 context 的祖先/自身才投递）；`global: true` 的监听者跳过过滤（对齐 Cordis events.ts:171-174）。

## 6. 事件类型化

Cordis 的 `declare module` 声明合并 → C# 用泛型事件 + 强类型 payload：

```csharp
// 事件类型：强类型 payload 类
public sealed record TaskCompleted(Guid TaskId, TaskResult Result);

// 注册/分发：类型安全
ctx.Events.Subscribe<TaskCompleted>(handler);   // 编译期检查 payload 类型
ctx.Events.Publish(new TaskCompleted(id, result));
```

事件名用静态类常量或枚举，不用字符串。

## 7. 生命周期

| 实体 | 生命周期 | 触发 |
|------|---------|------|
| context | 与 actor 同 | 管理层 spawn actor 时创建 |
| scope 链 | 随 context | 每实例独立 scope 根 |
| 插件注册 | 短命 | 插件 dispose 时按 ID 回收 |
| 事件监听 | 随插件 | 插件 dispose 时自动摘除 |

## 8. 已决决策（ADR-0003/0006）

- **context 并发模型**：actor 串行处理（默认）——消息循环天然无竞争，context 状态免费安全；高吞吐域可显式声明 `concurrency: parallel` 由管理层扩展（ADR-0003）
- **共享事件父 scope**：需要全局共享的事件（遥测/日志）挂公共父 scope，在配置层显式声明
- **事件分发模式全集**：emit/parallel/serial/bail/waterfall 五种，策略事件按决策型（serial/bail）/观察型（parallel/emit）选模式（ADR-0006）
- **rebind / 服务级隔离 / set 属主**：同 scope 重复注册报错、按服务名 isolate、set 属主校验（§2.1-§2.3，来源 G14/G7/G8）
