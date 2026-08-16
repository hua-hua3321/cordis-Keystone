---
type: architecture-doc
tags: [cordis-csharp, architecture, semantics-mapping, intercept, check]
created: 2026-08-15
---

# 12 — Cordis 语义 → C# 对应物对照

> 实现期参考字典：被弃用/未解析的 Cordis 机制，其**能力**在 C# 侧由哪些原生对应物承接。
> 用途：防止"Cordis 有这个我们为什么没有"反复纠结——本文给出每个机制的拆解、C# 对应代码与差异本质。
> 本文是**语义映射参考**：`07-cordis-migration-gap.md` 是分析快照，`11-gap-register.md` 是跟踪状态，本文是"对应物字典"。
> §2-§6 覆盖 G6/G9（intercept/check）；**§7-§9 覆盖框架层通读新增的 H/M/L 系列**（traceable/编程式挂载/访问拦截/effect 等）；**§10 覆盖第二轮官方包源码级复查新增的 F 系列**（表达式方言/条目模型/事务/CRUD/写回管线等）。

## 1. 对应关系总表

| Cordis 机制 | 决策 | C# 对应物 | 弃用的是什么 |
|------------|------|-----------|-------------|
| `ctx.intercept` + `Service.resolveConfig`（G6） | ADR-0010 显式弃用通用机制 | `IOptionsMonitor<T>` 命名选项 + `Configure/PostConfigure` 链 + 配置层分层（08 §4） | 运行期沿原型链动态走查合并的**机制形状** |
| `inject: { 服务名: 配置 }`（G6 附带） | ADR-0010 | Keyed services + factory 委托（`AddKeyedScoped<T>(key, factory)`） | —（能力完整保留） |
| `Service.check` 谓词（G9） | ADR-0010 显式弃用 | `Task Ready` 模式 + `IHealthCheck` readiness 探针 + `IHostedService` 生命周期 | 就绪状态**耦合进加载序**的机制形状 |
| PENDING 等 check 通过（G9 附带） | ADR-0010 | `await service.Ready` + ADR-0007 PENDING（等注册）+ ADR-0005 FAILED/重试（就绪失败） | 运行期状态不再塞进生命周期状态机 |

## 2. G6 intercept → C# 对应物

### 2.1 Cordis 机制（源码证据）

intercept 由三个 API 拼成，语义 = **"每次注入一个服务时，可携带该服务的配置，配置沿祖先链合并"**：

```ts
// context.ts —— 派生一个携带服务配置的子 context（原型链继承，父不被改）
intercept(name: string, config: any) {
  const intercept = Object.create(this[symbols.intercept])  // 原型链叠加
  intercept[name] = config
  return this.extend({ [symbols.intercept]: intercept })
}

// service.ts resolveConfig —— 服务解析配置时沿祖先链收集合并
[symbols.resolveConfig](base?: T, head?: T): T {
  let intercept = this.ctx[Context.intercept]
  const configs: any[] = []
  while (this.name in intercept) {
    if (Object.hasOwn(intercept, this.name)) configs.unshift(intercept[this.name])
    intercept = Object.getPrototypeOf(intercept)   // 沿原型链向上走
  }
  if (base) configs.unshift(base)
  if (head) configs.push(head)
  return this['Config']?.merge ? this['Config'].merge(...configs) : Object.assign({}, ...configs)
}
```

关键语义：
1. 配置是 **(服务名, 消费者) 二元组**维度——同一 `logger` 服务，插件 A 拿 level=debug，插件 B 拿默认值
2. 合并顺序 = 祖先链（root 先应用，越近优先级越高）；可用 `Config.merge` 深合并
3. 解析时机 = **服务访问/构造时**（运行期动态走查）

真实用例（harness）：`ctx.logger('name')` 级别按插件覆盖；客户端类服务（HTTP/LLM client）每个消费者带不同选项。

### 2.2 C# 对应物 A：`IOptionsMonitor<T>` + 命名选项（直接等价）

```csharp
// 等价于 ctx.intercept('logger', { level: 'debug' })
services.AddOptions<LoggerOptions>()
    .Configure("plugin-auth", o => o.Level = LogLevel.Debug)      // 按服务名(插件)命名配置
    .Configure("plugin-fs", o => o.Level = LogLevel.Information);

// 插件侧消费（等价于 resolveConfig 的结果）
class AuthPlugin : IPlugin
{
    public Task InitializeAsync(IPluginContext ctx, IReadOnlyDictionary<string, object?> config)
    {
        var opts = ctx.Get<IOptionsMonitor<LoggerOptions>>()!.Get("plugin-auth");
        // opts.Level == Debug —— 每个插件拿到自己的配置
    }
}
```

- `IOptionsMonitor.Get(name)` = 按名取配置，**且支持运行期变更通知**（`OnChange`）——比 intercept 的一次性解析多一个能力
- 多次 `Configure` 链式执行 = 对应"沿祖先链合并"（root→leaf 顺序）；`PostConfigure` 做最终调整（对应 head）

### 2.3 C# 对应物 B：配置层分层（08 §4 patch/overlay）

intercept 的"**子 context 覆盖、父不被改**"语义，C# 版 = 配置层显式分层：base 组合包 → profile → 用户 patch → overlay。合并是**配置期静态完成**，不是运行期走查。

### 2.4 C# 对应物 C：Keyed services + factory 委托（"每次注入带配置"）

```csharp
services.AddKeyedScoped<ILogger, PluginLogger>("plugin-auth",
    (sp, key) => new PluginLogger(
        sp.GetRequiredService<IOptionsMonitor<LoggerOptions>>().Get((string)key!)));
```

### 2.5 差异本质（为什么方向是对的）

| | Cordis intercept | C# 命名选项 |
|---|---|---|
| 解析时机 | **运行期**，每次访问服务沿原型链动态走查 | **配置期**静态绑定，启动期校验（fail-fast，09 §2） |
| 类型安全 | 弱（`config: any`） | 强（`IOptionsMonitor<T>` 泛型） |
| AOT | 动态走查（反射不友好） | 源生成器友好（规则 0 约束下的正解） |
| 变更 | 无 | `OnChange` 原生支持 |

**弃用的是机制形状（运行期动态走查合并），不是能力**——能力由命名选项 + 配置层分层 + Keyed factory 完整保留，且换成 AOT 安全、启动期可校验的静态形态。05 §5 的 logger 特例（category = `{能力域}/{插件 ID}` + 命名选项覆盖级别）是该形态的第一个落地。

## 3. G9 check 谓词 → C# 对应物

### 3.1 Cordis 机制（源码证据）

```ts
// service.ts —— Service 构造时把可用性谓词随服务一起注册
self.ctx.reflect.provide(name, self, this[symbols.check])   // check = Service.check 静态 symbol

// fiber.ts _checkImpl —— 依赖方解析依赖时，check 不过 = 依赖视为不可用
_checkImpl(name: string) {
  const impl = this.ctx.reflect._getImpl(name, true)
  if (!impl) return delete this._store[name]
  if (impl.check && !impl.check.call(getTraceable(this.ctx, impl.value))) {
    return delete this._store[name]      // check falsy → 依赖不可用 → 依赖方保持 PENDING
  }
  this._store[name] = impl
}
```

关键语义：加载门控 = **服务已注册（inject 解析到）∧ check 谓词通过**。check 把"服务存在"与"服务就绪"分成两个概念——服务对象先注册，但 check falsy（后端未连上）→ 依赖方留在 PENDING，直到 check 变真。

真实用例：数据库服务启动即注册，但连接建立前 check=false；所有依赖它的插件 PENDING 等 DB ready 才 LOADING。

### 3.2 C# 对应物 A：Ready Task 模式（最贴近的惯用法）

```csharp
// 服务提供方：注册即注入；就绪是运行期属性
class DbService : IDataProvider
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Ready => _ready.Task;          // 消费者 await 这个

    public async Task ConnectAsync(CancellationToken ct)
    {
        await _connect(ct);                    // 连接成功
        _ready.TrySetResult();                 // 就绪信号 = check 谓词为真的那一刻
    }
}

// 依赖方：注册即可注入（不用等），需要就绪时显式 await
class ReportPlugin : IPlugin
{
    public async Task InitializeAsync(IPluginContext ctx, IReadOnlyDictionary<string, object?> config)
    {
        var db = ctx.Get<IDataProvider>("db");
        await db.Ready.WaitAsync(ctx.CancellationToken);   // 等价于 Cordis 的"PENDING 等 check"
        // 就绪后的业务
    }
}
```

### 3.3 C# 对应物 B：官方 Readiness 探针（IHealthCheck）

```csharp
builder.Services.AddHealthChecks().AddCheck<DbReadinessCheck>("db");
// GET /health/ready —— .NET 官方就绪概念，运维/K8s 探针直接消费
```

### 3.4 C# 对应物 C：IHostedService 生命周期

`BackgroundService.StartAsync` 完成 = 就绪信号；`WaitForStartAsync` 可做就绪等待——宿主级"等依赖就绪"。

### 3.5 差异本质（为什么方向是对的）

| | Cordis check | C# Ready Task / 健康探针 |
|---|---|---|
| 就绪状态挂在哪 | **加载序**（依赖方生命周期状态机跟提供方 check 走） | **运行期属性**（服务自己的 `Task Ready`，与加载序解耦） |
| 加载序可预测性 | 差——LOADING 与否随运行期 check 变化，启动流程动态化 | 好——注册即加载，序是静态可验证的（09 §2 fail-fast） |
| 等待语义 | 隐式（框架替你等） | 显式（消费者 `await Ready`，看得见） |
| 失败表达 | check 抛错 → 静默 delete store（日志一条） | `Ready` 上抛异常 / FAILED 态 + 监督重试（05 §3） |

**弃用的是"就绪耦合进加载序"这个机制形状**。C# 下注册即注入（ADR-0007 门控 = 服务已注册，静态可验证），就绪作为运行期属性由消费者显式 `await`——比 Cordis 隐式门控**更解耦、更可预测**；加载序一侧由 ADR-0005 的 PENDING/FAILED + 重试覆盖，运行期一侧用 Ready Task 表达。

## 4. 决策要点回顾（ADR-0010）

- **G6**：不做"每次注入携带服务配置、沿祖先链合并"的通用机制；`IOptions<T>` 命名选项 + 配置层合并为最终形态
- **G9**：不做"可用但未就绪"谓词；加载序门控 = "服务已注册"（ADR-0007 现状），未就绪由插件运行期自管
- 两项弃用的共同依据：与 D1 静态类型目标一致、AOT 安全（规则 0）、启动流程保持可预测（fail-fast）

## 5. 维护规则

- **新增被弃用机制**（G 类）：对照 §2-§3 格式补一节（机制源码证据 → C# 对应物 → 差异本质 → 弃用的是什么），并同步 11-gap-register 状态
- **新增未解析机制**（H/M/L/F 类）：按编号追加到 §7-§10（机制 → C# 对应物 → 结论），并同步 11-gap-register §3.1/§3.2 状态
- **本文不重复 07 的差距分析**：07 管"差在哪"，本文管"用什么替代"
- **与实现期的关系**：实现期遇到"为什么不做 X"的质疑，先查本文；对应物已在 SDK（10-plugin-sdk）落地的标注到对应节

## 6. 与相邻文档的关系

| 文档 | 关系 |
|------|------|
| 07-cordis-migration-gap.md | 差距分析快照（G6/G9 的定义来源） |
| 11-gap-register.md | 跟踪状态（G6/G9 行备注指向本文） |
| ADR-0010 | 决策记录（弃用决策 + 本映射的决策依据） |
| 05-reliability.md §5 | logger 特例 = intercept 替代形态的第一个落地 |
| 08-configuration-layer.md §4 | 配置层分层 = intercept 祖先链合并的静态形态 |
| 02-plugin-model.md §3 / ADR-0007 | 依赖门控 = "注册即用"（check 谓词不纳入） |

---

## 7. H 系列：框架层通读新增（高价值）

> 来源：Cordis core 源码全文通读（utils/reflect/registry/fiber/events）后，对照 C# 文档（00-12 + ADR）确认的未解析机制。C# 对应物经实现侧确认，均可在现有技术栈（T1-T11）内落地。

## 7.1 H1 traceable / 上下文跟随 → Activity.Current + CallerInfo + 解析隔离

**Cordis 语义**（utils.ts:117-218 createTraceable、reflect.ts get 路径）：服务方法被插件调用时，服务内部读到的 `ctx` 是**调用者的 ctx**（trace 绑定）；`noShadow` 服务（如 logger）例外，绑定提供者。服务能感知"谁在调用我"。

**C# 对应物（三层，无需运行期 proxy 重绑定）**：

| Cordis traceable | C# 对应物 | 说明 |
|------------------|----------|------|
| 调用者 ctx 重绑定 | `System.Diagnostics.Activity.Current`（AsyncLocal 承载的环境上下文） | 服务内读 `Activity.Current` 即得调用方的追踪/任务上下文；05 §5 已采纳 Activity 做链路追踪，同一机制承载"调用方感知" |
| "在函数上加参数即得调用链" | `[CallerInfo] CallerInfo`（.NET 9+，编译期自动注入调用者成员名/文件/行/列） | 用户指正：C# 原生即可，无需特殊处理；`[CallerMemberName]`/`[CallerFilePath]`/`[CallerLineNumber]` 属性族（[文档](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/attributes/caller-information)） |
| 服务感知调用方 scope | **解析侧隔离**：服务实例按调用方 scope 解析（03 §2.2）——实例 A 拿到 fs-A 实例、实例 B 拿到 fs-B 实例 | C# 的"谁在调用我"主要由实例级解析天然承担（服务实例已绑定实例），Activity 承担跨调用追踪侧 |

**差异本质**：Cordis 靠运行期 proxy 把 `this.ctx` 动态换绑；C# 靠环境上下文（Activity/AsyncLocal）+ 解析隔离 + 编译期 CallerInfo，静态、AOT 安全。**结论：H1 P9 已落地（`TraceContext`：Activity.Current 跨 async 贯穿 + TaskId tag 读取——服务内读 Activity.Current 即得调用方上下文，测试验证）。**

## 7.2 H2 编程式挂载（ctx.plugin / ctx.inject）→ 动态管道组合 + 生命周期托管

**Cordis 语义**（registry.ts:300-336）：任何 context 可运行期挂载插件（插件可挂子插件）；`ctx.inject(deps, cb)` = 依赖就绪后运行回调。

**C# 对应物（用户指正：动态传 List\<next\> 函数实现管道组合，再执行）**：

"挂载一个可执行插件"这个动作，C# 侧就是**动态管道组合**——插件 = 中间件节点，运行期向管道 List 插入节点，反向包装成单条链，再执行（ASP.NET Core 同款组合方式，T8）：

```csharp
// 动态管道组合（宿主内部实现；形状 B 闭包 = Func<ctx, next, Task>）
List<Func<IPluginContext, Func<Task>, Task>> nodes = new() { nodeA, nodeB, nodeC };

// 组合：从终节点反向包装成一条链
Func<Task> next = () => terminal(ctx);
foreach (var node in nodes.AsEnumerable().Reverse())
{
    var captured = node;
    var inner = next;
    next = () => captured(ctx, inner);
}
await next();   // 按序执行整条链
```

| Cordis | C# 对应物 | 说明 |
|--------|----------|------|
| `ctx.plugin(plugin)` 挂载可执行插件 | **动态管道组合**：运行期向管道 List 插入节点 → 反向包装 → 执行 | 04-pipeline "管道节点可换（热重载）"即同一机制；挂载 = 插入节点 |
| 挂载后的生命周期 | **生命周期托管**：节点随挂载器回收——quiesce（ADR-0005）+ 依赖门控（ADR-0007） | 挂载不只是"插入"，还绑定卸载收敛与依赖等待 |
| `ctx.inject(deps, cb)` | 依赖就绪后执行组合：PENDING（ADR-0007）→ 节点激活 → 组合执行 | 门控语义已覆盖 |
| 插件挂子插件 | **子管道嵌套**（节点内再组合子 List\<next\>）+ Proto.Actor 子 actor（能力域骨架） | 两层：管道组合管"节点编排"，actor 管"串行循环/监督"（T1） |

**差异本质**：Cordis 挂载 = fiber 入树 + 注册表；C# 挂载 = **管道节点动态插入（List\<next\> 组合）+ 生命周期托管（ADR-0005/0007）**。Proto.Actor 仍承担能力域 actor 的串行循环与监督，但"挂载一个可执行插件"的动作本身 = 管道组合，**不需要为每个插件 spawn actor**。

**结论**：H2 落点 = **04-pipeline 内部组合机制**（宿主侧 `List<Func<ctx, next, Task>>` 反向包装）+ **P7 已落地 `KeystoneHost.MountAsync`**（编程式挂载 = PluginLoader 全管线：编译→ALC→门控→运行→卸载）+ ADR-0005/0007 生命周期托管。形状 A/B 的关系澄清：**形状 A（IMiddleware）是插件 SDK 接口面；宿主内部组合用形状 B 闭包（List\<next\>），两者不冲突**（04 §2 定案补充）。

## 7.3 H3 服务访问拦截（internal/get / internal/set）→ AOP（AOT 安全路径）

**Cordis 语义**（events.ts:344-347）：服务读取/写入可被瀑布拦截（代理/监控/装饰）；`internal/service`（reflect.ts:331-334）通知服务变更。

**C# 对应物（用户指正：AOP 处理读写拦截，外部调用时拦截器优先生效）**：

| 路径 | 说明 | AOT（规则 0） |
|------|------|--------------|
| **显式装饰器/门面拦截**（context 门面中间件，04 管道形状） | 服务注册经装饰器包装，外部调用先过拦截器 | ✅ **P2 已落地**：`IContextInterceptor`（`OnServiceReadAsync`/`OnServiceWriteAsync`）+ `ContextFacade` 门面组合，无运行时代理 |
| 源生成器拦截器（C# `InterceptsLocation` 实验特性） | 编译期拦截，稳定后可用 | ✅ 可选 |
| Castle.DynamicProxy / `DispatchProxy` | 运行期代理 + IL 生成 | ❌ **排除**（违反规则 0） |

**`internal/service` 变更通知** = ADR-0007 服务可用性事件（已覆盖）。**结论：H3 机制选定 AOT 安全路径（装饰器/门面拦截），运行时 AOP 库显式排除；P2 已在 ContextFacade 落地。**

---

## 8. M 系列：框架层通读新增（中价值）

| # | Cordis 机制 | C# 对应物 | 结论 |
|---|------------|----------|------|
| M1 | 通用 `ctx.effect()` + EffectMeta 诊断树（fiber.ts:415-561, getEffects()） | **P2 已落地**：`IEffectRegistry` + `IContext.Effect(Func<Task>, label, [CallerMemberName])`（net10 BCL 无 CallerInfo 类型，用 CallerMemberName 等价，ID-07）+ 嵌套诊断树 + 逆序收敛 | ✅ 已实现 |
| M2 | Callable service（`ctx.logger()` 可调用 + 带方法，utils.ts:226-233） | **P2 已落地**：`IContext.GetLogger(name)` + `IPluginContext.Logger`（category = 上下文名/插件 ID）；callable 形态用方法替代 | ✅ 已实现 |
| M3 | `internal/config` 配置解析拦截（fiber.ts:641-644，注入激活后、schema 校验前） | **P6 已落地**：`ConfigResolver`（IConfigFilter next 链可否决）+ `ConfigSchema`（必填/未知字段 fail-fast + 默认值补齐） | ✅ 已实现 |
| M4 | `@Inject` 方法级延迟调用（registry.ts:45-59，方法调用等到服务可用） | `Lazy<Task<T>>`（首次访问才解析） | ✅ Lazy 即对应物 |
| M5 | `Plugin.Transform`（用户配置→运行配置转换，registry.ts:113-118） | `IOptions` 配置绑定 + 转换步骤（08 §5） | ✅ 已覆盖 |
| M6 | `CordisError` 错误码体系（fiber.ts:157-174） | `TaskResult.ErrorCode`（06 §1）+ 框架级异常码表 | ✅ **P1/P2 已定稿**：`Keystone.Core.Errors.ErrorCode`（格式 `KS:{CATEGORY}:{NAME}`，CORE/LIFECYCLE/GATING/CONFIG/PIPELINE 五类 20 码；`KeystoneException.Code` 共用；P2 补 ServiceAlreadyRegistered） |
| M7 | 监听器 `prepend` 顺序 + `ctx.once`（events.ts:97-117） | **P2 已落地**：`EventSubscriptionOptions{Prepend, Once}` + 事件过滤 `{Scope, Global}`（G15） | ✅ 已实现 |
| M8 | `update(config, noSave)` 持久化钩子（fiber.ts:736-753） | 配置层"内存更新 vs 写回"语义（08 §6） | ✅ 已有多种实现方式 |

---

## 9. L 系列：框架层通读新增（低价值）

| # | Cordis 机制 | 结论 |
|---|------------|------|
| L1 | logger 环形缓冲 1000（logger.ts:195-220） | 实现期：ILoggerProvider 内存环形缓冲（诊断用） |
| L2 | `Service.extend` 派生（service.ts:33-40） | 无需对应（装饰器/继承） |
| L3 | `ctx.get(name, strict)` strict 语义（reflect.ts:233-243） | 无需对应（scoped 生命周期天然覆盖） |
| L4 | logger 对 Error/AggregateError 自动展开（logger.ts:141-150） | 实现期：ILogger 异常参数结构化 |
| L5 | composeError 长栈拼接（utils.ts:240-287） | **显式无需对应**（.NET 原生 async 栈更好） |
| L6 | `ctx.root` / `baseUrl` / `Context.is` | 实现期随手处理 |
| L7 | printf 风格格式化 + `defaultFormatters`/`Formatter`（logger.ts:50-61） | 不同形等价：ILogger 消息模板（`{Placeholder}` + `LogValuesFormatter`）；05 §5 结构化记录模型已覆盖字段侧，格式化器用 ILogger 模板 |
| L8 | `c16`/`c256` ANSI 名称配色（logger.ts:165-173） | 无需对应（控制台配色细节） |
| L9 | 事件监听器 `this` = 分发 context（events.ts:165-175 dispatch 绑定） | 已覆盖：G15 事件过滤形状（hook 记录 ctx）；C# 侧监听器用闭包捕获 context，等价 |

---

## 10. F 系列：官方包源码级复查新增（第二轮）

> 来源：vendored 官方包源码全文通读（@deepseek-ai/cordis-plugin-include + cordis-plugin-loader 全 7 文件）+ 核心深层语义。此前官方包仅按 README/教程覆盖，本轮源码级复查发现 14 项，其中 6 项中价值已补设计，1 项走 ADR-0011 显式弃用。

| # | Cordis 机制 | 源码证据 | C# 落点 | 状态 |
|---|------------|---------|---------|------|
| F1 | `!!js` 配置表达式方言（YAML tag + with-scope eval + internal/config 插值 + disabled 求值） | include/src/index.ts:9-23、loader config/utils.ts:5-27、entry.ts:104-108 | **ADR-0011 弃用求值**（规则 0 第 4 条）+ **ADR-0012 保留解析**（YamlDotNet 自定义 tag 静态插值：!!env/!!file/anchors；引用环检测见 08 §5） | ✅ 已决策 |
| F2 | 条目级 `inject` 字段（与 manifest inject 合并） | loader config/entry.ts:9-22、index.ts:122 | 08 §3 条目模型 `Inject` 字段 | ✅ 已补设计 |
| F3 | diff 分级重启（name/inject/group 变 → 冷重启；仅 config → 热更新；disabled → 卸载；每步回滚） | loader config/entry.ts:142-246 | 08 §6.1 变更分级 | ✅ 已补设计 |
| F4 | 组级事务（并行应用 + 失败聚合 + 逆序回滚 + 重 id 检测） | loader config/group.ts:59-106 | 08 §6.2 组级事务 | ✅ 已补设计 |
| F5 | 条目 CRUD API（create 含 position/remove/跨组移动回滚/`:` 嵌套 id 解析/持久化钩子） | loader config/tree.ts:66-142 | 09 §5 CreateEntry/RemoveEntry/MoveEntry/ResolveEntry | ✅ 已补设计 |
| F6 | 配置写回管线（原子写 tmp+rename、占用重试退避、写防抖、写队列、readonly、事务刷新保旧树、initial 引导、apply 队列防竞态） | include/src/index.ts:27-368 | 08 §6.3 配置写回管线（`File.Move` 原子替换 + IOException HRESULT 重试映射） | ✅ 已补设计 |
| F7 | disabled 继承（父组挂起 → 子树全挂；组自身永不挂） | loader config/entry.ts:83-98 | 08 §3 disabled 字段说明 | ✅ 已补 |
| F8 | `cordis:` 内建模块前缀（Loader.builtins） | loader config/tree.ts:144-148 | 08 §3 name 字段（内建前缀约定） | ✅ 已补 |
| F9 | loader 事件面（exit/config-update/entry-init/partial-dispose/patch-context waterfall） | loader src/index.ts:23-30 | 09 §5 管理面事件表 | ✅ 已补设计 |
| F10 | isolate 变更：服务实现跨 realm 转移 + realm GC + 自定义过滤通知（patch-context 7 步） | loader config/isolate.ts:71-173 | 03 §2.2 isolate 变更语义；转移优化 = 实现期 | ✅ 已补（优化实现期） |
| F11 | Loader.Intercept.await 门控（依赖 loader 的插件等条目加载完） | loader src/index.ts:54-57, 166-170 | G9 弃用机制的单点用例；C# 由 09 §2 启动序（配置层先于能力域）天然覆盖 | ✅ 无需对应 |
| F12 | envData（CORDIS_SHARED 环境变量跨进程共享启动时间） | loader src/index.ts:68-70 | 无需对应（环境变量/静态成员） | ✅ 无需对应 |
| F13 | unwrapExports（ESM/CJS/default 互操作归一） | loader src/index.ts:191-199 | 无需对应（Roslyn 编译产物无互操作问题） | ✅ 无需对应 |
| F14 | internal/config 的 tree-carrier 豁免（Group/Include 的 config 是"别的行的配置列表"，不参与插值） | loader src/index.ts:92-101 | **复查降级**：F1 弃用表达式后插值不存在 → 豁免必要性消失，归并为 F4/08 §6.2（组条目 config = 子条目列表的结构语义，不套用条目级转换） | ✅ 已覆盖（复查修正） |

---

## 11. 关系说明（H/M/L/F 系列）

- H/M/L 的 C# 对应物已按"现有技术栈（T1-T11）内可落地"验证；**需要新 ADR 的只有真正引入新机制/新技术的项**（H/M/L 均不需要；F 系列仅 F1 因显式弃用走 ADR-0011）
- F 系列第二轮复查后已全部落设计（08/09/03 补充 + ADR-0011），落点见 11-gap-register §3.2

---

## 11.1 CA 系列语义差异注记（第二轮代码级审计"接受差异"项，P63 回写）

> 来源 18 §3 决策矩阵；2026-08-16 落注。三项均为"刻意设计差异"——非缺口，记录等价面与理由防复查误判。

| # | Cordis 机制 | Keystone 现状（等价面） | 差异理由 |
|---|------------|------------------------|---------|
| CA-14 await 抛启动错误 | `fiber.await()` 重抛 startup error | CreateEntryAsync 收敛不抛；失败进 FAILED（GetPluginState 可查 + TaskFailedFact 已记录） | 隔离语义（09 §2 刻意设计）：单插件失败不阻断管理面调用方；诊断走事实事件而非异常通道 |
| CA-16 internal/listener·dispatch | 监听器注册/分发本身作为总线事件暴露 | 无对应；等价面 = EventSubscriptionOptions + 五模式分发（emit/parallel/serial/bail/waterfall） | .NET 事件模型下订阅行为不作为总线事件暴露；元事件需求出现时经 EventBus 显式建模 |
| CA-18 Service 抽象基族 | init/invoke/extend/check/tracker 五符号 | 服务 = 任意 T + Provide（init→InitializeAsync；invoke→GetLogger 形态；extend/check 无） | POCO 服务 + 扩展方法是 C# 惯例；check 为 G9 显式弃用（ADR-0011 同族理由） |

---

## 12. 排查覆盖凭证（穷举审计）

> 审计方法：对 vendored Cordis 全部 8 个源文件执行 `grep '^export'` 穷举导出符号，逐一对照覆盖状态；**第二轮**通读官方包源码（plugin-include + plugin-loader 全 7 文件，F 系列）；**第三轮**扫 bin.js CLI 入口 + cosmokit 工具库。
> 审计基线：`~/Projects/deepseek-harness/vendor/cordis/src/`（Cordis 4.0.1，2026-08-15）。

| 文件 | 导出符号数 | 覆盖状态 | 未映射残留 |
|------|-----------|---------|-----------|
| context.ts | 2（interface + class，含 extend/isolate/intercept/root 等成员） | ✅ 全映射 | 0 |
| events.ts | 9（DispatchMode/EventOptions/Hook/EventsService/Events/isBailed/类型工具） | ✅ 全映射 | 0 |
| fiber.ts | 9（ValidationError/resolveConfig/Disposable/Effect/EffectMeta/FiberState/CordisError/CordisError 命名空间/Fiber） | ✅ 全映射 | 0 |
| logger.ts | 16（LoggerType/LoggerMethod/Formatter/LoggerLevel/Message/Exporter/defaultFormatters/LoggerOptions/Logger×2/Logger class/c16/c256/LoggerService 命名空间+接口+类） | ✅ 全映射 | 审计发现 L7/L8 已补（printf 格式化/ANSI 配色） |
| reflect.ts | 4（Property/Property 命名空间/Impl/ReflectService） | ✅ 全映射 | 0 |
| registry.ts | 7（Inject/InjectKey/Inject 装饰器/Inject 命名空间/Plugin/Plugin 命名空间/RegistryService） | ✅ 全映射 | 0 |
| service.ts | 1（Service 抽象类，含 init/check/config/invoke/extend/tracker/resolveConfig 七符号） | ✅ 全映射 | 0 |
| utils.ts | 12（DisposableList/Tracker/symbols/isConstructor/joinPrototype/isObject/getPropertyDescriptor/getTraceable/withProps/createCallable/composeError/buildOuterStack） | ✅ 全映射 | 0 |

### 12.1 导出符号明细对照（60 项）

状态值域：✅ 已映射（有 C# 对应物/已在文档覆盖）｜⚠️ 实现期（对应物已定，API 形态实现期落）｜显式弃用（ADR 记录的不做项）｜无需对应（类型工具/内部实现/显式放弃）。

| 文件 | 符号 | 类型 | 状态 | 落点 | 源码位置 |
|------|------|------|------|------|---------|
| context | `Context` (interface) | 接口 | ✅ | 03 §2 作用域链；root/baseUrl→L6；events/logger/reflect/registry 内置服务→00 T1-T8 | context.ts:16-33 |
| context | `Context` (class) | 类 | ✅ | extend→03 §2；isolate→G7/03 §2.2；intercept→G6/ADR-0010；Context.is→L6 | context.ts:42-146 |
| events | `isBailed` | 函数 | ✅ | ADR-0006 serial/bail 短路语义（bail 值判定） | events.ts:13-15 |
| events | `Parameters` / `ReturnType` / `ThisType` | 类型 | 无需对应 | 类型工具，C# 泛型天然等价 | events.ts:18-22 |
| events | `DispatchMode` | 类型 | ✅ | ADR-0006 五种分发模式 | events.ts:32 |
| events | `EventOptions` | 接口 | ✅ | global→G15；prepend→M7 | events.ts:112-117 |
| events | `Hook` | 接口 | ✅ | G15 事件过滤（hook 记录 ctx） | events.ts:120-123 |
| events | `EventsService` | 类 | ✅ | parallel/emit/serial/bail/waterfall→ADR-0006；on/once→M7；dispatch+filter→G15 | events.ts:131-319 |
| events | `Events` (内部事件集) | 接口 | ✅ | internal/plugin+status→ADR-0005；internal/config→M3；internal/service→H3；internal/update→ADR-0005；internal/get+set→H3；internal/listener→ADR-0006；internal/dispatch→ADR-0006 | events.ts:329-351 |
| fiber | `ValidationError` | 类 | ✅ | N1/08 §5 插件配置校验错误 | fiber.ts:19-36 |
| fiber | `resolveConfig` | 函数 | ✅ | N1（schema 校验 → 默认值补齐） | fiber.ts:50-62 |
| fiber | `Disposable` | 类型 | ✅ | 02 §6 disposer 协议 | fiber.ts:74 |
| fiber | `Effect` | 类型 | ⚠️ | M1 通用 effect（含迭代器形态，实现期补 IPluginContext.Effect） | fiber.ts:83-93 |
| fiber | `EffectMeta` | 接口 | ⚠️ | M1 诊断树（[CallerInfo] 审计清单） | fiber.ts:96-101 |
| fiber | `FiberState` | 枚举 | ✅ | ADR-0005 六态状态机 | fiber.ts:147-154 |
| fiber | `CordisError` | 类 | ✅ | M6 框架异常码（TaskResult.ErrorCode 之外的框架级；P1 定稿 ErrorCode.cs） | fiber.ts:157-165 |
| fiber | `CordisError.Code` | 命名空间 | ✅ | M6 码表（P1 定稿：`KS:{CATEGORY}:{NAME}` 五类 20 码） | fiber.ts:168-174 |
| fiber | `Fiber` | 类 | ✅ | uid/ctx/config/state→ADR-0005；dispose/store/inertia→G2 quiesce；effect/getEffects→M1；_checkImpl→G9；_refresh/_setEpoch→G5；_reload/_unload/await/restart/update→ADR-0005+M8 | fiber.ts:184-753 |
| logger | `LoggerType` | 类型 | ✅ | G11 四级（error/info/warn/debug） | logger.ts:13 |
| logger | `LoggerMethod` | 类型 | ✅ | G11 | logger.ts:16 |
| logger | `Formatter` | 类型 | ✅ | L7 printf 格式化 → ILogger 消息模板（{Placeholder}） | logger.ts:19 |
| logger | `LoggerLevel` | 枚举 | ✅ | G12 级别覆盖 | logger.ts:22-27 |
| logger | `Message` | 接口 | ✅ | 05 §5 结构化日志记录模型（sn/ts/name/type/level/args/fiber） | logger.ts:30-38 |
| logger | `Exporter` | 接口 | ✅ | G12 ILoggerProvider 接线（levels 阈值/formatters） | logger.ts:41-47 |
| logger | `defaultFormatters` | 常量 | ✅ | L7（printf 占位符格式化器 → ILogger 模板） | logger.ts:50-61 |
| logger | `LoggerOptions` | 接口 | ✅ | G11/G12（name/meta/level） | logger.ts:64-71 |
| logger | `Logger` (interface ×2) | 接口 | ✅ | G11 命名日志门面 | logger.ts:74-76 |
| logger | `Logger` (class) | 类 | ✅ | G11 门面实现（printf 格式化/Error 展开→L4） | logger.ts:83-161 |
| logger | `c16` / `c256` | 常量 | 无需对应 | L8 ANSI 名称配色（控制台细节） | logger.ts:165-173 |
| logger | `LoggerService` (namespace) | 命名空间 | ✅ | G12 Intercept 形态（ADR-0010 后仅命名选项） | logger.ts:176-181 |
| logger | `LoggerService` (interface) | 接口 | ✅ | M2 callable 形态（GetLogger(name) 替代） | logger.ts:184-186 |
| logger | `LoggerService` (class) | 类 | ✅ | G11/G12/M2/L1（环形缓冲 1000、exporter、invoke） | logger.ts:194-269 |
| reflect | `Property` / `Property` (namespace) | 类型 | 显式弃用 | G16 动态能力丢弃清单（accessor 计算属性） | reflect.ts:94-113 |
| reflect | `Impl` | 接口 | ✅ | G9 check 谓词（显式弃用）+ H1 value/fiber | reflect.ts:116-125 |
| reflect | `ReflectService` | 类 | ✅ | get(strict)→L3；set→G8 属主；provide→G7 隔离；notify→G5/ADR-0007；accessor/mixin/trace/bind→G16+H1；handler→H1/H3 | reflect.ts:133-417 |
| registry | `Inject` (type) | 类型 | ✅ | ADR-0007 服务级依赖声明（数组/映射两形态） | registry.ts:19 |
| registry | `InjectKey` | 类型 | ✅ | G4 key = 服务名（类型+名称二元组） | registry.ts:22-24 |
| registry | `Inject` (decorator) | 函数 | ✅ | 类级→G4；方法级延迟→M4（Lazy\<Task\<T\>\>） | registry.ts:37-60 |
| registry | `Inject` (namespace resolve) | 命名空间 | ✅ | G4 依赖归一化（数组/对象/继承） | registry.ts:63-89 |
| registry | `Plugin` | 类型 | ✅ | 02 §1 插件三形态（Function/Constructor/Object） | registry.ts:92-95 |
| registry | `Plugin` (namespace) | 命名空间 | ✅ | Base（name/Config/inject/provide/intercept→G6）；Transform→M5；Runtime（fibers 多实例）→01 §4 | registry.ts:98-146 |
| registry | `RegistryService` | 类 | ✅ | plugin/inject→H2 编程式挂载；map 枚举（keys/values/entries）→09 §5 状态查询；resolve→插件形态 | registry.ts:195-336 |
| service | `Service` (abstract) | 类 | ✅ | resolveConfig→G6；check→G9；invoke→M2；extend→L2；tracker/filter→H1/G15 | service.ts:22-115 |
| utils | `DisposableList` | 类 | ✅ | G2 quiesce 逆序并发 disposer（clear 返回 reverse） | utils.ts:5-40 |
| utils | `Tracker` | 接口 | ✅ | H1 traceable 元数据 | utils.ts:43-47 |
| utils | `symbols` | 常量 | ✅ | 内部实现载体（H1 shadow/tracker、M2 invoke、G6 resolveConfig 等） | utils.ts:50-73 |
| utils | `isConstructor` | 函数 | ✅ | 02 §1 插件形态判定（class vs function） | utils.ts:79-89 |
| utils | `joinPrototype` | 函数 | ✅ | M2 callable 内部实现（原型合并） | utils.ts:92-99 |
| utils | `isObject` / `getPropertyDescriptor` | 函数 | 无需对应 | 内部工具 | utils.ts:102-114 |
| utils | `getTraceable` | 函数 | ✅ | H1 上下文跟随（→Activity.Current + CallerInfo） | utils.ts:117-125 |
| utils | `withProps` | 函数 | ✅ | H1 内部（receiver 叠加） | utils.ts:128-140 |
| utils | `createCallable` | 函数 | ✅ | M2 callable 服务创建（→GetLogger(name) 方法形态） | utils.ts:226-233 |
| utils | `composeError` / `buildOuterStack` | 函数 | 无需对应 | L5 长栈拼接（.NET 原生 async 栈更好，显式无需对应） | utils.ts:240-287 |

**结论**：8 文件 **60 个导出符号全部有覆盖状态**（✅ 已映射 / ⚠️ 实现期 / 显式弃用 / 无需对应），无"完全未看过"的导出。审计过程新发现的 2 项细节（L7/L8）已补入 §9。

**第三轮补充审计（bin.js + cosmokit，0 项需设计）**：

| 目标 | 内容 | 结论 |
|------|------|------|
| bin.js（16 行 CLI 引导） | `new Context()` + baseUrl + `ctx.plugin(Loader)` + `loader.create(include)` | ✅ 已覆盖：09 §2 启动 + 08 §2 配置加载的组合用例；仅使用已设计机制（H2 编程式挂载、F5 CRUD） |
| cosmokit（~50 导出，6 文件） | 数组/字符串/Binary/Time/对象工具 + 类型守卫 + 类型级类型 | ✅ 纯工具库 0 框架语义：BCL/LINQ 全覆盖，无需移植；deepEqual/clone 已是 F3/F6 设计的机制成分 |

**残留风险（非"漏"，是持续维护项）**：

1. **深度语义 vs 导出面**：已随 P2（Effect/拦截器）、P7（H2）、P12（O2）实现展开闭合（§7.1/§8 对应"已落地"标注）
2. **版本漂移**：基线 = vendored 4.0.1；上游 Cordis 迭代会产生新导出/新语义——按 11-gap-register §5 纪律，实现期发现新差距追加登记，必要时重跑本审计
3. **官方包体系**（@cordisjs/plugin-*、create-cordis、utils）：vendored 的 plugin-include/plugin-loader 已源码级穷举（F 系列 + 本轮 bin.js/cosmokit）；**上游生态其余包**（README 列表中的 group/hmr/timer/logger-console 等未随 vendor 分发的）仍按 README/教程覆盖（N1-N6 + 08/10），引入时按 11-gap-register §5 纪律补登记
