---
type: architecture-doc
tags: [cordis-csharp, architecture, migration-gap]
created: 2026-08-15
---

# 07 — Cordis 迁移差距分析

> 对照 vendored Cordis 源码（`~/Projects/deepseek-harness/vendor/cordis/src/`）与当前 C# 设计文档（00~06）的差距盘点。
> 本文只做差距分析，不替实现阶段做决策；每项差距给出结论、证据、迁移建议与优先级。

## 1. 分析范围与方法

**源码基线**（vendor/cordis/src/，DeepSeek Harness vendored 版本）：

| 文件 | 行数 | 承担能力 |
|------|------|---------|
| fiber.ts | 754 | 插件生命周期状态机、effect/disposer、quiesce 收敛、依赖门控激活 |
| registry.ts | 337 | 插件注册表、Inject/InjectKey、@Inject 装饰器、Runtime 记录 |
| reflect.ts | 418 | context proxy 拦截、服务 provide/set/get、accessor/mixin/trace |
| events.ts | 352 | 五种 DispatchMode（emit/parallel/serial/bail/waterfall）、Hook 过滤 |
| context.ts | 146 | 根 context、extend/isolate/intercept 作用域派生 |
| logger.ts | 270 | 命名日志、intercept 配置、exporter 导出器 |
| service.ts | 115 | Service 基类、intercept 配置合并、可调用服务 |
| utils.ts | 10236* | DisposableList、symbols、tracing（*字节数） |

**文档基线**（docs/architecture/）：00-tech-stack（技术栈）、01-overview（总览）、02-plugin-model（插件模型）、03-context（作用域链）、04-pipeline（管道/事件双轨）、05-reliability（可靠性）、06-contracts（消息契约）+ ADR-0001~0004。

**判定标准**：
- **已覆盖**：当前文档已给出可与 Cordis 语义等价落地的设计，无需补充。
- **部分覆盖**：核心语义已覆盖，但存在明确缺失的边界/细节，实现前必须补设计。
- **未覆盖**：当前文档无对应设计，语义完全空缺。

**结论速览**：7 项中 0 项已覆盖、7 项部分覆盖、0 项未覆盖——即所有核心概念都有 C# 对应物，但每项都存在需要补全的语义空洞。逐项证据与建议见 §2。

---

## 2. 七个必查项逐项分析

### 2.1 Fiber 异步生命周期 / quiesce 收敛机制

**结论：部分覆盖**（disposer 协议已覆盖；生命周期状态机与异步收敛协议未设计）

**Cordis 源码证据**（fiber.ts）：
- 完整状态机：`FiberState = PENDING → LOADING → ACTIVE → FAILED → UNLOADING → DISPOSED`（fiber.ts:147-154），状态迁移发 `internal/status` 事件（fiber.ts:586）
- 异步回收：`dispose(): Promise<void>`，卸载体 `_unload()` 将全部 effect disposer **逆序**（DisposableList.clear() 返回 reverse，utils.ts:27-31）**并发**执行并全部 await（fiber.ts:675-696）；disposer 可 async，卸载必须等它们收敛
- quiesce 收敛门：`dispose()` 循环 `while (this.inertia) await this.inertia`，直到 in-flight 的 load/unload 全部 settle 才算完成（fiber.ts:293-295）；`inertia` 记录在途转换（fiber.ts:200-201）
- 稳定等待：`await()` 等到无 in-flight 转换并重抛启动错误（fiber.ts:704-710）
- 重启/更新：`restart()` = dispose+reload（fiber.ts:718-723）；`update()` = 校验新配置 → `internal/update` waterfall 可否决 → restart（fiber.ts:736-753）
- 依赖门控激活：PENDING 直到所有 inject 服务可用（`_checkImpl` + `_refresh`，epoch = 依赖 impl fiber uid 拼接，fiber.ts:597-623）；依赖变化 → `_setEpoch` 驱动 `_reload`/`_unload`（fiber.ts:625-639）
- 失败语义：FAILED 态持 `_error`，`await()` 重抛；PENDING 期注册的 effect 在卸载时显式排空（fiber.ts:281-296）

**当前文档证据**：
- 02-plugin-model.md §6：`IPlugin : IAsyncDisposable` + `InitializeAsync` + "dispose = 摘除自己注册的东西"——只有 disposer 原语
- 02-plugin-model.md §7：热重载 = 重编译 → 新 ALC → dispose 旧 → 挂新 → 旧 ALC.Unload()——**没有"等旧插件完全收敛再 Unload"的闸门**；§7 自己点名卸载残留是 HMR 头号失败原因，但解法只写了"disposer 协议强制"
- 05-reliability.md §1：插件卡死 → 超时 → dispose 旧 → 加载新——无 quiesce（在途请求排空）语义；04-pipeline.md §8 对**管道**换有"在途请求排空后销毁"，但对**插件**卸载没有对应描述
- 03-context.md §7：生命周期表只有"插件注册短命，dispose 时按 ID 回收"

**差距说明**：
1. 无生命周期状态机设计：PENDING/LOADING/ACTIVE/FAILED/UNLOADING/DISPOSED 没有对应 C# 模型，"插件当前处于什么状态、启动失败后怎么办"无定义
2. 无 quiesce 收敛协议：dispose 与 ALC.Unload 之间缺"等待在途任务结束 + 等待全部 async disposer settle"的显式闸门；`IAsyncDisposable` 只是原语，没有收敛保证
3. 无稳定等待/错误重抛语义（await() 等价物）；无 restart()/update() 语义（配置热更新在插件粒度没有设计，04-pipeline §8 只覆盖管道粒度）

**迁移建议**（实现前必补）：
- 在 02-plugin-model.md §6 增补 `PluginLifecycleState` 状态机（PENDING/LOADING/ACTIVE/FAILED/UNLOADING/DISPOSED）+ 迁移图
- 定义 quiesce 协议：插件卸载 = ① 拒绝新任务 ② 等在途任务完成（CancellationToken 传播）③ 逆序并发执行全部 disposer 并 await ④ 全 settle 后 ALC.Unload() ⑤ 回收验证（05-reliability.md §6 已有测试门，把收敛断言写进热重载测试）
- 定义 `restart()`/`update()`（配置热更新到插件粒度）与 FAILED 态处理（重试策略联动 05-reliability.md §3）
- 生命周期状态机是新 ADR 候选（见 §5 影响范围）

### 2.2 RegistryService 依赖注入拓扑（InjectKey）vs Keyed Services

**结论：部分覆盖**（静态注册/解析拓扑可等价；availability 等待、依赖变更重载、intercept 配置三段语义缺失）

**Cordis 源码证据**（registry.ts / reflect.ts / fiber.ts）：
- `InjectKey` = Context 上带 intercept 配置的服务名类型键（registry.ts:22-24）；`inject` 声明 = 服务名数组 或 服务名→intercept 配置映射（registry.ts:19）
- 插件在 `Plugin.Base.inject` 声明所需服务，"only loads while all are available"（registry.ts:105-106）；fiber 持解析后的 `inject: Dict<服务名→intercept配置>`（fiber.ts:225）
- **等待语义**：PENDING 直到服务可用；`Impl.check` 可选可用性谓词，依赖方加载前先查（fiber.ts:597-609，reflect.ts:124）
- **变更重载**：服务提供/卸载 → `notify()` 唤醒依赖方重新 `_checkImpl` + `_refresh`（reflect.ts:314-336）→ 依赖方 fiber 自动 reload/unload
- **intercept 配置**：`ctx.intercept(name, config)` 派生携带配置的子 context（context.ts:139-145）；fiber 构造时把 inject 配置合入 `ctx[intercept]`（fiber.ts:240-245）；`Service[resolveConfig]` 沿祖先链合并（service.ts:86-102）

**当前文档证据**：
- 02-plugin-model.md §3：Keyed Services（`AddKeyedScoped<TImpl>("key")` + `GetRequiredKeyedService<T>(key)`）+ 每插件实例独立子容器
- 02-plugin-model.md §3 自身矛盾：注册段写"key = 插件 ID 或能力域实例 ID"（§3 代码注释），解析段写"key = 插件内服务名"——key 语义未定，这是实现前必须锁死的分歧

**差距说明**：
1. **key 语义**：Cordis 的 key 是**服务名**（稳定的语义标识，消费者声明 `inject: ['fs']` 即可，不感知提供者身份）；若 C# 用插件 ID 做 key，消费者必须知道"哪个插件提供 fs"，依赖关系从"服务契约"退化成"实现耦合"——与 Cordis 不等价。等价形式 = key 用服务名（`AddKeyedScoped<IFsProvider, LocalFsProvider>("fs")`），插件 ID 只做子容器隔离
2. **等待语义**：Cordis 缺服务时插件保持 PENDING 等待；`GetRequiredKeyedService<T>` 缺服务直接抛异常——没有"等待可用"的 C# 设计
3. **变更重载**：服务提供方卸载/替换时，依赖方自动 reload——C# 设计无对应（Keyed Services 只在解析瞬间绑定）
4. **intercept 配置**：Cordis 每次注入可携带该服务的 intercept 配置（如 logger 的 name/level），沿祖先链合并——C# 设计无 intercept 概念

**迁移建议**：
- 锁死 key 语义为**服务名**（类型 + 服务名二元组），插件 ID 仅用于子容器分组；修正 02-plugin-model.md §3 的矛盾表述
- 实现前决策：依赖门控激活（PENDING 等待 + 变更重载）是否纳入第一版——若纳入，需设计"服务可用性事件 + 插件挂起队列"机制（可复用 §2.1 状态机的 PENDING 态）；若不纳入，需显式声明与 Cordis 的差异及影响（插件启动顺序退化为配置序/拓扑序）
- intercept 配置：用每插件 `IOptions<T>` 命名选项 + 配置层合并替代（见 §2.5），或显式声明不做

### 2.3 ReflectService 的 proxy 拦截语义在 C# 静态类型下的能力取舍

**结论：部分覆盖**（类型化替代方向正确，多数动态能力可接受丢弃；isolate 按服务隔离、set 属主校验、check 谓词需显式设计）

**Cordis 源码证据**（reflect.ts / context.ts）：
- context 是 Proxy：get/set/has 陷阱（reflect.ts:135-206）
- get 服务解析：waterfall `internal/get` → **沿 fiber 父链向上走查** `fiber.store[prop]`，isolate 标签不一致即停（reflect.ts:153-167）；strict 模式只取 ACTIVE fiber 的 impl（reflect.ts:237-243）
- set 属主校验：只有提供该服务的 fiber 可以 set，否则抛错（reflect.ts:254-265）
- `provide()`：按 isolate 标签 symbol 为 key 注册（reflect.ts:277-305）；同 scope 重复 provide 抛错（reflect.ts:289-291）；disposer 摘除并唤醒依赖方
- `accessor()` 计算属性、`mixin()` 动态 API 转发（ctx.on → ctx.events.on）（reflect.ts:345-390）、`trace()`/`bind()` 上下文追踪包装（reflect.ts:398-417）
- `isolate(name, label)`：**按服务名**隔离 scope，同 label 共享、不同 label 隔离（context.ts:121-125）

**当前文档证据**：
- 02-plugin-model.md §2（D1）：接口白名单 `ctx.Get<IFsProvider>()`——类型化替代已定
- 03-context.md §2（D3）：类继承骨架 + IFeatureCollection shadow + IServiceScope 父子链
- 00-tech-stack.md §3.3：T5 组合 = "Cordis extend() 的完整语义"

**可接受丢弃的动态能力**（静态类型下的自然替代，需在文档显式确认）：
1. 动态属性访问 `ctx[任意名]` → `ctx.Get<T>()` 编译期类型——**可接受**，这正是 D1 的目标
2. proxy 注入守卫错误（"cannot get property X without inject"）→ 编译期错误——**可接受**，严格更好
3. `accessor()` 运行期计算属性 → 接口成员/方法——**可接受**，前提是 API 面在接口里显式设计
4. `mixin()` 动态转发 → context 接口门面（`ctx.Events.Subscribe<T>` 即此类）——**可接受**，03/04 文档已隐式覆盖
5. `trace()`/`bind()` 追踪包装 → `System.Diagnostics.Activity`/DiagnosticSource——**可接受**，属可观测性细节，05-reliability §5 未设计但非阻塞

**不可静默丢弃、必须显式设计的语义**：
1. **按服务隔离（isolate）**：Cordis 可对**单个服务名**建独立 scope（`isolate('fs', label)`）；`IServiceScope` 是整 scope 隔离，粒度不同。当前文档只设计了"每实例独立 scope 根"（03-context §5），**服务级隔离**无对应——多实例模型下"实例 A 用 fs-A、实例 B 用 fs-B"如果靠整 scope 隔离，会连带隔离所有其他服务，语义过粗
2. **set 属主校验**：Cordis 只允许提供者改自己的服务值；`IFeatureCollection.Set` 无属主概念、任意覆盖。若 C# 用 IFeatureCollection 做 shadow，需在 context 门面上加"服务属主"校验设计，否则热重载时新旧插件可互相改服务值
3. **check 谓词**：`Impl.check` 控制依赖方能否加载（如"服务可用但暂不就绪"）——C# 无对应，若 §2.2 的依赖门控激活纳入第一版则必须一并设计

**迁移建议**：
- 在 03-context.md §2 增补"服务级隔离"设计：`IServiceScope` 每服务一个命名 scope（或子容器按服务名分组），显式声明与整 scope 隔离的取舍
- 在 context 门面设计 context 属主：服务注册记录提供插件 ID，set 时校验属主（可挂在 IFeatureCollection 之上做薄封装）
- 显式记录"已接受丢弃"清单（上面 5 项），防止实现期反复纠结

### 2.4 五种 DispatchMode vs 当前仅 waterfall + parallel

**结论：部分覆盖**（emit/parallel/waterfall 已覆盖；**serial/bail 明确缺失**）

**Cordis 源码证据**（events.ts）：
- `DispatchMode = 'emit' | 'parallel' | 'serial' | 'bail' | 'waterfall'`（events.ts:32）
- emit：同步 fire-and-forget，忽略返回值（events.ts:194-196）
- parallel：并发跑全部监听者、await 全部、聚合错误（events.ts:183-187）
- serial：**按序 await，遇到第一个 bail 值（非 null/false/undefined）即停并返回**（events.ts:204-209，isBailed events.ts:13-15）
- bail：**同步按序，第一个非空返回值即停**（events.ts:217-222）
- waterfall：监听者包裹 next 链，不调 next 即否决（events.ts:234-243）
- 框架内部事件按模式选型：`internal/update`/`internal/config`/`internal/get`/`internal/set` = waterfall（可否决/可拦截），`internal/listener` = **bail**（监听注册可被替换），`internal/dispatch` = emit（诊断）

**当前文档证据**：
- 04-pipeline.md §3（D4）：双轨 = 管道（waterfall）+ 观察者事件（parallel/emit）
- 03-context.md §4：事件分层 = 拦截事件（waterfall）/ 策略事件（parallel/emit）/ 事实事件（持久）
- 06-contracts.md §2：判断口诀"要结果/要干预/要顺序 → 管道；否则 → 事件"

**差距说明**：
1. **serial 缺失**：异步按序 + 首个有效结果短路。06-contracts 的口诀把"要结果/要干预/要顺序"全导向管道，但管道是 waterfall（包裹式），无法表达"监听链上第一个返回决策者生效"——这是策略型事件（如权限检查链：第一个拒绝者决定结果）的典型语义
2. **bail 缺失**：同步首个非空生效。框架级"handler 替换/选择"语义（Cordis 的 internal/listener）无对应
3. 当前文档把"事件=parallel/emit"固化成双轨，等于默认丢弃 serial/bail 且**没有显式决策记录**——这是遗漏不是设计选择

**迁移建议**：
- 实现前必须显式决策，二选一：
  - **方案 A（建议）**：事件轨补 serial/bail 两种模式（实现成本低：`EventsService` 的形状就是委托链，serial/bail 各加一个聚合函数），并修订 06-contracts.md §2 判断口诀为"要顺序+首个决策 → serial/bail；要包裹/否决 → waterfall；只观察 → parallel/emit"
  - **方案 B**：声明弃用 serial/bail，给出替代路径（如策略事件必须走管道），并写入文档防止回归
- 框架内部事件（若 C# 版有等价物：服务解析拦截、配置更新否决）按 Cordis 模式选型表选模式，不要让所有 internal 事件默认 waterfall

### 2.5 LoggerService 命名日志 vs ILogger

**结论：部分覆盖**（能力等价可实现；命名规则、级别覆盖、exporter 接线未设计）

**Cordis 源码证据**（logger.ts）：
- `ctx.logger(name?)` 返回命名 Logger 门面；名字缺省 = `config.name ?? hyphenate(fiber.name)`——**自动按插件 fiber 名命名**（logger.ts:251-261）
- 每插件可经 intercept 配置覆盖 logger 的 name/level（logger.ts:176-181, 239-249）
- 四级：error/info/warn/debug（LoggerLevel，logger.ts:22-27）；printf 风格格式化 + 可插拔 formatter（logger.ts:50-61, 99-131）
- exporter 导出器：插拔 sink，按 logger 名设级别阈值（logger.ts:41-47, 154-159）；环形缓冲 1000 条（logger.ts:195）
- Message 结构：sn/ts/name/type/level/args/fiber（logger.ts:30-38）

**当前文档证据**：
- 05-reliability.md §5：只写了"ILogger 注入 context；插件必须通过 ctx 日志（不直接 console）；日志格式 {taskId} {pluginId} {phase} {elapsed}"
- 01-overview.md §1：不重造日志系统（ILogger）——选型正确

**差距说明**：
1. **命名规则未定**：ILogger 的 `ILoggerFactory.CreateLogger(category)` 等价于 Cordis 命名日志，但"category = 插件 ID/fiber 名"这条规则没有写入文档——没有它，插件日志无法按插件过滤，可观测性打折
2. **级别覆盖未定**：Cordis 经 intercept 按插件覆盖 name/level；C# 没有 intercept 概念，需要每插件 `IOptions<T>` 命名选项或配置层的等价设计（与 §2.2 的 intercept 差距同源）
3. **exporter 接线未定**：ILoggerProvider 即 exporter 等价物，但"哪些 provider 内置、控制台/文件/结构化"未设计
4. Message 结构化记录未定：06-contracts 定义了跨域消息契约，但日志记录本身的字段契约（对应 Cordis Message）未设计——05-reliability §5 只给了格式字符串，没有记录模型

**迁移建议**：
- 在 05-reliability.md §5 补：category 命名规则（插件 ID + 能力域）、每插件级别覆盖（IOptions<T>）、ILoggerProvider 接线清单、结构化日志记录模型（与 ADR-0004 显式序列化契约对齐）
- 这是低优先级、实现期可补的差距，不阻塞设计收敛

### 2.6 插件间依赖声明（cordis.yml 的 inject）vs 当前 manifest

**结论：部分覆盖**（manifest 声明了程序集级依赖与 provides；**服务级依赖（inject）与就绪等待未声明**）

**Cordis 源码/使用证据**：
- 插件在代码层声明 `inject`（registry.ts:105-106）；harness 实际形态：`export const inject = ['agents', 'sessions', 'sessionPersistence']`（docs/postmortem/0001），`{ inject: ['messageFeedback'] }`、`{ inject: ['workspaceRegistry'] }`（packages/ 多处）
- loader 读取插件导出（name/inject/Config/apply），**依赖驱动加载序**："插件声明所需的服务后，会等待这些服务就绪才启动；加载顺序通过服务依赖表达，而非手动编排启动序列"（docs/cordis-primer.zh.md）；loader 在声明的注入激活后才插值插件 config
- `provide`：插件声明提供的服务名（registry.ts:108），loader 据此建依赖图
- postmortem 0001 实证：inject 丢失（export default 把命名导出扔掉）→ 插件在 fiber 树里找不到服务 → 运行时崩溃——inject 是加载正确性的硬依赖

**当前文档证据**：
- 02-plugin-model.md §1 manifest：`{"id", "version", "main", "dependencies": ["cordis-runtime", "cordis-contracts"], "provides": ["IFsProvider"]}`
- 02-plugin-model.md §5：六条工作清单全是**程序集级**引用问题（Roslyn 引用集、Resolving fallback、版本冲突、传递依赖、编译/运行引用一致、插件间类型共享走宿主接口）

**差距说明**：
1. **依赖维度错位**：manifest 的 `dependencies`（cordis-runtime/cordis-contracts）是 **Roslyn 编译引用白名单**——解决"插件代码能引用哪些程序集"；Cordis 的 `inject` 是**服务级运行时依赖**——解决"插件要等哪个服务提供方就绪"。两者正交，当前 manifest 只覆盖了前者
2. **无服务级依赖声明**：插件消费 `IFsProvider` 时，编译引用是 cordis-contracts（程序集），但**运行期需要一个实际提供 IFsProvider 的插件**——这个依赖关系 manifest 没有字段可表达
3. **无就绪等待**：即使能声明，C# 设计也没有"等依赖可用再激活"的加载序（§2.2 同源差距）；没有它，插件启动顺序只能靠配置序，等于放弃 Cordis 的核心卖点

**迁移建议**：
- manifest 增补服务级依赖字段（如 `"inject": ["fs", "llm"]`），与 `provides` 配对成依赖图；保留 `dependencies` 程序集白名单，文档明确两者维度不同
- 若 §2.2 的依赖门控激活纳入第一版，加载序 = 依赖图拓扑序 + PENDING 等待；manifest 校验器（AGENTS.md 提到 verify-cordis-config 同类工具）应校验 inject 声明的服务在依赖图内可达
- 优先级 P0：这是"插件框架之所以是插件框架"的核心机制，harness 全仓都依赖它

### 2.7 scope 父子链 rebind 语义 vs IFeatureCollection / IServiceScope

**结论：部分覆盖**（父子链查找与 shadow 覆盖已覆盖；**rebind 语义分歧、按服务隔离、set 属主未处理**）

**Cordis 源码证据**（context.ts / reflect.ts）：
- `extend(meta)`：原型继承 + meta 自有属性 shadow 父级，父不 mutated（context.ts:99-107）
- `isolate(name, label)`：子 context 为**指定服务**开新 scope；同 label 共享、不同 label 隔离（context.ts:121-125）
- `intercept(name, config)`：子 context 携带服务 intercept 配置（context.ts:139-145）
- **rebind 语义**：同 scope 内重复 provide 同一服务 = **抛错**（"service X has been registered at <fiber>"，reflect.ts:289-291）；"覆盖父级"只能通过**新建子 scope**（isolate/extend）提供新 impl 实现——父不被改
- 服务解析走 fiber 父链 + isolate 标签一致性检查（reflect.ts:153-167）；事件分发按 hook 所属 context 过滤，`global:true` 才跳过（events.ts:171-174，service.ts:61-63）

**当前文档证据**：
- 03-context.md §2（D3）：类继承骨架 + IFeatureCollection shadow（后注册覆盖先注册）+ IServiceScope 父子链（先自己再父）
- 03-context.md §5：每实例独立 scope 根；scope 父子关系配置层显式声明（共享事件挂公共父 scope）
- 00-tech-stack.md §3.3：三者组合 = "Cordis extend() 的完整语义"

**差距说明**：
1. **rebind 语义分歧（关键）**：Cordis 同 scope rebind = **报错**，覆盖父级 = **新 scope**；`Microsoft.Extensions.DependencyInjection` 同 scope 重复注册 = **静默 last-wins**，`IFeatureCollection.Set` = 无属主任意覆盖。C# 设计文档没有处理这个分歧——直接用 MS.DI 语义，热重载"摘旧挂新"时可能静默替换而非报错/隔离
2. **按服务隔离粒度**：Cordis `isolate` 按服务名；IServiceScope 整 scope——服务级隔离缺失（同 §2.3 第 1 条）
3. **事件 context filter**：文档 03-context §5 高层面覆盖（"事件路由按 context/scope 过滤"），但过滤实现（hook 记录 ctx、分发检查、global 旁路）未落到设计——实现期要防"事件跨实例泄漏"

**迁移建议**：
- 在 03-context.md §2 显式决策 rebind 语义：同 scope 重复注册 = 报错（对齐 Cordis，可防热重载静默污染）还是 last-wins（对齐 MS.DI，简单但热重载有风险）——建议前者，与 02-plugin-model §3 的"key 必须全局唯一，禁止同名覆盖"一致
- 服务级隔离（isolate）与 set 属主校验见 §2.3 迁移建议
- 事件过滤落到设计：hook 记录注册 context + 分发时按 scope 链过滤 + 全局事件旁路，03-context §5 补实现形状

---

## 3. 差距清单（汇总）

| # | 差距 | 证据位置 | 结论 | 优先级 |
|---|------|---------|------|--------|
| G1 | 插件生命周期状态机缺失（PENDING/LOADING/ACTIVE/FAILED/UNLOADING/DISPOSED） | fiber.ts:147-154 vs 02-plugin-model §6-7 | 部分覆盖 | P0 |
| G2 | quiesce 收敛协议缺失（卸载 = 排空在途 + 逆序并发 disposer + await 全 settle + 再 Unload） | fiber.ts:675-696, 293-295 vs 02-plugin-model §7 | 部分覆盖 | P0 |
| G3 | 插件粒度 restart()/update()（配置热更新）与 FAILED 态处理缺失 | fiber.ts:718-753 vs 04-pipeline §8（只有管道粒度） | 部分覆盖 | P1 |
| G4 | Keyed Services 的 key 语义未锁死（服务名 vs 插件 ID 矛盾） | 02-plugin-model §3 内部矛盾 | 部分覆盖 | P0 |
| G5 | 依赖门控激活（PENDING 等待 + 服务变更重载）无 C# 设计 | fiber.ts:597-639, reflect.ts:314-336 vs 02-plugin-model §3 | 部分覆盖 | P0 |
| G6 | intercept 配置（每注入携带服务配置、祖先链合并）无对应 | registry.ts:19, context.ts:139-145, service.ts:86-102 vs 全文档 | 部分覆盖 | P1 |
| G7 | 按服务隔离（isolate）粒度缺失（IServiceScope 是整 scope） | context.ts:121-125 vs 03-context §2/§5 | 部分覆盖 | P1 |
| G8 | 服务 set 属主校验缺失（IFeatureCollection 无属主） | reflect.ts:254-265 vs 03-context §2 | 部分覆盖 | P1 |
| G9 | Impl.check 可用性谓词无对应 | reflect.ts:124, fiber.ts:597-609 | 部分覆盖 | P2 |
| G10 | serial/bail 分发模式缺失（无显式决策记录） | events.ts:204-222 vs 04-pipeline §3 | 部分覆盖 | P0 |
| G11 | 日志命名规则（category=插件 ID）未写入设计 | logger.ts:251-261 vs 05-reliability §5 | 部分覆盖 | P2 |
| G12 | 日志级别覆盖（intercept/IOptions）与 provider 接线未设计 | logger.ts:239-249 vs 05-reliability §5 | 部分覆盖 | P2 |
| G13 | manifest 无服务级依赖声明（dependencies 是程序集白名单，与 inject 正交） | 02-plugin-model §1/§5 vs registry.ts:105-106 | 部分覆盖 | P0 |
| G14 | 同 scope rebind 语义分歧未决策（Cordis 报错 vs MS.DI last-wins） | reflect.ts:289-291 vs 03-context §2 | 部分覆盖 | P1 |
| G15 | 事件 context filter 实现形状未落到设计 | events.ts:171-174 vs 03-context §5 | 部分覆盖 | P2 |
| G16 | 动态能力丢弃清单未显式记录（accessor/mixin/trace 等） | reflect.ts:345-417 vs 02-plugin-model §2 | 部分覆盖 | P2 |

## 4. 迁移优先级

### P0（实现前必须收敛，否则核心语义错误或热重载不可靠）

| 项 | 理由 |
|----|------|
| G1/G2（生命周期状态机 + quiesce） | 热重载是 C# 版相对 JS 版的核心增值；无状态机与收敛闸门，ALC 卸载残留/在途任务撕裂是必然事故（02-plugin-model §7 自己点名） |
| G4/G5/G13（key 语义 + 依赖门控 + manifest inject） | "等依赖就绪再启动"是 Cordis 框架之所以是框架的机制（harness 全仓依赖，cordis-primer 核心概念第 3 条）；key 语义不锁死，实现期 Keyed Services 必返工 |
| G10（serial/bail） | 事件契约面在 06-contracts 冻结前必须定；漏掉 serial/bail 会让策略型事件被迫塞进管道，语义错位 |

### P1（第一版内补设计，可排在 P0 之后）

| 项 | 理由 |
|----|------|
| G3（插件粒度 restart/update） | 配置热更新承诺了"管道可换"，插件级配置更新是同一承诺的插件侧；可后补但别欠债 |
| G6/G7/G8/G14（intercept + 服务级隔离 + set 属主 + rebind 决策） | 多实例隔离与热重载正确性的边界条件；不决策则实现期在 MS.DI 语义上踩坑（静默替换、跨实例泄漏） |

### P2（实现期按需补，不阻塞设计收敛）

| 项 | 理由 |
|----|------|
| G9/G11/G12/G15/G16（check 谓词、日志细节、事件过滤形状、动态能力清单） | 可观测性与健壮性细节；在插件 SDK 设计（01-overview §7 遗留待定）时一并处理 |

## 5. 影响范围

**文档改动**（实现 P0/P1 时同步更新，遵循 R10：改文档必须同步 AGENTS.md 索引 + 关联 ADR）：
- 02-plugin-model.md：§3（key 语义、依赖门控）、§6（状态机 + quiesce）、§1/§5（manifest 增 inject 字段）
- 03-context.md：§2（rebind 决策、服务级隔离、set 属主）、§5（事件过滤形状）
- 04-pipeline.md：§3（serial/bail 决策落点）
- 05-reliability.md：§1（插件级 quiesce）、§5（日志命名/provider/记录模型）
- 06-contracts.md：§2（判断口诀修订，若采纳方案 A）

**ADR 候选**（新决策落地前写 ADR，见 decisions/README.md）：
- ADR-0005：插件生命周期状态机 + quiesce 收敛协议（G1/G2/G3）
- ADR-0006：事件分发模式全集（serial/bail 纳入 or 弃用声明）（G10）
- ADR-0007（可能）：依赖门控激活与 manifest 服务级依赖（G5/G13）——若 G5 纳入第一版

**实现任务分解影响**（看板流水线，仅影响后续拆解，本文不创建子任务）：
- P0 项应各自成为独立实现子任务（I→V→R 验证链），每项含对应文档更新
- 热重载回收测试（05-reliability §6 硬门）需扩展：断言 quiesce 收敛（在途任务排空 + disposer 全 settle + ALC 回收）

**不产生影响**：
- 技术栈基线（00-tech-stack T1-T9）不受本文影响——所有差距都在现有技术选型内可解（Keyed Services、IServiceScope、ILogger、中间件形状均可用），不需要新增技术项

## 6. 参考索引

**Cordis 源码**（`~/Projects/deepseek-harness/vendor/cordis/src/`）：
- fiber.ts — 状态机、effect/disposer、quiesce、依赖门控、restart/update
- registry.ts — Inject/InjectKey、@Inject、Plugin.Base、Runtime
- reflect.ts — proxy 陷阱、provide/set/get、isolate、accessor/mixin/trace
- events.ts — DispatchMode 五种、Hook 过滤、internal 事件
- context.ts — extend/isolate/intercept
- logger.ts — 命名日志、intercept、exporter
- service.ts — Service 基类、resolveConfig
- utils.ts — DisposableList（clear 逆序）

**harness 佐证**（`~/Projects/deepseek-harness/`）：
- docs/cordis-primer.zh.md — 核心概念 + 分发模式表 + loader 配置
- docs/postmortem/0001-acp-default-export-drops-inject.md — inject 丢失实证、fiber 树走查、ctx.get 可选服务读取
- examples/acp-agent/cordis.yml、python/sdk-runtime/.../cordis.yml — loader 配置形态

**当前文档**（docs/architecture/）：
- 00-tech-stack.md §3.3 — T5 组合声明
- 01-overview.md §1/§6 — 不重造清单
- 02-plugin-model.md §1-§7 — 插件定义/键控服务/加载/依赖共享/回收/热重载
- 03-context.md §2/§4/§5 — 作用域链/事件分层/事件隔离
- 04-pipeline.md §3/§8 — 双轨/管道热更新
- 05-reliability.md §1/§5/§6 — 错误处理/可观测性/测试门
- 06-contracts.md §2 — 请求-响应 vs 事件分界
