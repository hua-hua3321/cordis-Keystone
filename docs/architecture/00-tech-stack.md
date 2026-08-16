---
type: architecture-doc
tags: [cordis-csharp, architecture, tech-stack]
created: 2026-08-15
---

# 00 — 当前系统技术体系

> 方案的技术基线与已确认技术栈清单。本文是架构文档的"技术栈总表"；NuGet 版本以 `Directory.Packages.props`（CPM）为唯一真源，本文版本列为回填快照（2026-08-16）。

## 1. 技术基线

| 项 | 版本 | 说明 |
|----|------|------|
| .NET | .NET Core 10（net10.0，JIT 运行时） | 宿主运行在完整 .NET 运行时；不采用 NativeAOT（ADR-0002 方案 A） |
| C# | C# 14 | 随 .NET 10 SDK 提供；项目代码用 C# 14 语法（规则 D04 现代语法偏好） |
| 部署形态 | JIT 自包含发布 + ReadyToRun（R2R）可选 | ADR-0002：放弃 NativeAOT 单二进制，保留自包含发布 |

**AOT 就绪约束（AGENTS.md 规则 0）**：虽然当前采用 JIT，所有代码必须按 AOT 兼容标准编写——禁止运行时反射/动态生成/反射序列化于宿主路径，唯一例外是插件加载层（Roslyn 编译 + ALC）。技术栈中的 Source Generator、显式序列化契约均为此服务。

## 2. 技术栈清单（已确认）

| # | 技术项 | 版本/形态 | 架构用途 | 关联文档/ADR |
|---|--------|----------|----------|-------------|
| T1 | Proto.Actor | 1.8.0（Directory.Packages.props） | actor 模型：**每能力域一个 actor**（串行消息循环保证 context 无竞争；supervision 监督重启）。管理层为宿主组合根 `KeystoneHost`（**非 actor**，见 §3.1 实现备注）（能力域 actor 串行/监督；库自身 AOT 警告例外 ADR-0015）| 01-overview §2-§3、05-reliability §2、ADR-0003 |
| T2 | Roslyn（Microsoft.CodeAnalysis.CSharp） | 5.6.0（2026-08-15 锁定） | 插件内存编译：单文件 `.cs` → `CSharpCompilation.Create` + `Emit(MemoryStream)` → PE | 02-plugin-model §4、ADR-0002 |
| T3 | Collectible AssemblyLoadContext | .NET BCL 内置（net10.0） | 插件加载：私有 ALC（`isCollectible: true`），依赖 fallback 到 Default；热重载摘旧挂新后 dispose 卸载 | 02-plugin-model §4-§7、ADR-0001 |
| T4 | 键控服务存储（自建 `KeyedServiceStore`） | 自研（~200 行纯内存簿记，P57） | 服务注册：键 = `(服务名, realm)`，进程级单一共享 store；**设计期原案 MS.DI Keyed Services 未采用**（per-scope 容器模型与 Cordis 全局 store 语义不符，ID-50/02 §3 实现备注） | 02-plugin-model §3、03-context §2.2 |
| T5 | context 作用域链（`ContextFacade` 父链 + isolate map） | 自研（P21/P57） | context 作用域链：`ContextFacade` 父子链（Parent/Root）+ isolate map 沿链推导 realm + 进程级 `KeyedServiceStore` 查键；**设计期三层混合（类继承骨架/IFeatureCollection/IServiceScope）未采用**（见 §3.3 实现备注） | 03-context §2、01-overview D3 |
| T6 | MessagePack（MessagePack-CSharp） | 3.1.8（2026-08-15 锁定，配合源生成器） | 跨域序列化：actor 间消息的显式序列化边界，默认 MessagePack、JSON 可配置 | 06-contracts、ADR-0004 |
| T7 | Source Generator | C# 编译器内置（Roslyn） | AOT 就绪：序列化契约（MessagePack/System.Text.Json 源生成器）、编译期已知类型替代运行时反射 | AGENTS.md 规则 0、ADR-0002 |
| T8 | ASP.NET Core 中间件形状 | 设计模式（不引包） | 管道：中间件链 + waterfall 语义，插件 = 中间件；不重造中间件框架 | 04-pipeline、01-overview §1 |
| T9 | 中央治理库 | ~/Projects/central-governance（D01-D08 + R00-R20） | 项目规则引用：C# 代码风格/测试/文档治理等 | AGENTS.md 治理节 |
| T10 | MAF 包族（Microsoft.Agents.AI）+ MCP 协议 SDK（ModelContextProtocol.Core） | MAF 1.17.0 / MCP 2.2.0（跟随官方 NuGet，单向依赖） | AI 能力域底层组合：llm/agents/skills/mcp/workflow 适配层，**框架核心不依赖**（ADR-0008）；MCP 协议层用官方稳定 SDK（ID-12） | ADR-0008、10-plugin-sdk |
| T11 | .NET 10 文件式应用（File-based apps） | net10.0 内置 | 插件脚本形态：单文件 .cs + 顶层语句，`#:package` 依赖声明 ↔ manifest `dependencies` 白名单；复杂插件走 DLL 轨（现有 ALC 管线） | 02-plugin-model §4、ADR-0008 |
| T12 | YamlDotNet | 18.1.0 | 配置解析：YamlStream 节点树手动映射（规则 0：无反射反序列化器；ADR-0014/0016 YAML-only） | 08-configuration-layer、ADR-0012 |
| T13 | AgileConfig.Client | 1.9.1（**预留可选源**，未默认接线） | 配置中心源：ADR-0013 提供者抽象下的预留实现（ADR-0014 开发阶段收敛 YAML-only） | ADR-0013/0014 |
| T14 | OpenTelemetry（Exporter.Console/OTLP/Extensions.Hosting） | 1.12.0（仅 Keystone.Hosting 引用） | 观测导出层（ADR-0018 L3）：L1 探针纯 BCL / L2 事实复用 EventStore / L3 组合导出仅宿主 | ADR-0018、05-reliability §5 |

## 3. 各项在架构中的用途与相互关系

### 3.1 运行骨架：Proto.Actor（T1）

三层架构的"活体"由 actor 承担（01-overview §2）：

```
管理层（KeystoneHost，宿主组合根——非 actor，P7 落地形态）
  ├─ 读配置 → 创建能力域（CapabilityDomain）→ spawn 能力域 actor（监督子）
  ├─ 插件编译（Roslyn，T2）→ 加载（ALC，T3）→ 注册（KeyedServiceStore，T4）
  ├─ 热重载（FileSystemWatcher → 重编译/原地更新 → 摘旧挂新）
  └─ 监督（能力域 actor 崩溃 → 重启策略，经 CapabilityDomain）
能力域 actor（Proto.Actor）
  ├─ 持 context + 管道（中间件链，T8）
  ├─ 串行消息循环 → context 状态天然无竞争（ADR-0003）
  └─ 跨域消息经 MessagePack 序列化（T6）
```

> **实现备注（2026-08-16，按代码核对）**：设计期原案为"管理层 = CompositionRoot actor"（01 §2 原图）；落地为 `KeystoneHost` 普通类（进程内组合根，09 §1）——管理面（配置状态机/CRUD/watcher/写回）无消息循环需求，actor 化无收益；**actor 只用于能力域**（串行保证 + 监督，ADR-0003/0015）。01/05/09 已同步注记。

- **串行保证**：一个能力域 context 一次处理一个任务，由 Proto.Actor 消息循环保证（ADR-0003），这是 context 无锁的根基。
- **监督**：能力域 actor 崩溃由 Proto.Actor supervision 重启（05-reliability §2），与可靠性策略联动。

### 3.2 插件加载链路：Roslyn → ALC → KeyedServiceStore（T2 → T3 → T4）

插件生命周期是三者串成的管线（02-plugin-model §3-§7）：

```
单文件 .cs（+ manifest）
  → T2 Roslyn 内存编译（CSharpCompilation.Create + Emit(MemoryStream)，带 embedded PDB）
  → T3 私有 Collectible ALC 加载（LoadFromStream，依赖 fallback 到 Default）
  → 反射实例化（仅插件加载层，规则 0 例外）
  → T4 KeyedServiceStore 注册（Provide 写键 = (服务名, realm)，属主 = 插件 context）
  → 挂载到能力域 context 的管道上
```

- **热重载** = 重新走这条链路：新编译产物进新 ALC，dispose 旧 ALC 触发卸载（T3 的 collectible 特性是回收的关键）。
- **KeyedServiceStore 解决"强类型接口 + 运行期实例区分"**：同一 `IFsProvider` 可按 `(服务名, realm)` 键注册多个实现，插件解析时算 realm + 查共享 store，编译期类型安全（02-plugin-model §3 实现备注）。
- **隔离**：默认共享域（realm=""）+ 条目 isolate 声明私有/命名域（03 §2.2），多实例隔离靠 realm 键天然达成（P54/P57）。

### 3.3 context 作用域链：ContextFacade 父链 + isolate map（T5）

Cordis `extend()` 的原型继承 + 属性 shadow 语义，C# 落地形态（03-context §2 实现备注，P21/P57）：

| 机制 | 承担语义 |
|------|---------|
| `ContextFacade` 父子链（Parent/Root） | scope 派生：子 context 复用父的事件总线/服务 store/logger 工厂，自身独立 Effect/请求 CT 槽 |
| isolate map（名 → realm，沿链推导） | "子覆盖父、父不被改"的 shadow 语义：首个含该服务名的 facade 给出 realm，均无 → "" 默认共享 |
| 进程级 `KeyedServiceStore`（键 = (服务名, realm)） | 服务解析/注册：算 realm + 查共享 store（Cordis `reflect.store` 对应物） |

> **实现备注（2026-08-16，按代码核对）**：设计期原案为"类继承骨架（RequestContext : TurnContext : SessionContext）+ IFeatureCollection shadow + IServiceScope 父子链"三层混合（D3 原案）；**落地未采用**——固定层级骨架无实际需求（context 谱系由条目树/isolate 声明驱动），IFeatureCollection/IServiceScope 的 per-scope 容器模型与 Cordis 全局 store + realm 语义不符（ID-50）。03-context §2 已按落地形态改写。

### 3.4 跨域边界：MessagePack（T6）

- 域内（管道内插件调用）：直接调用，**不序列化**（01-overview §6 克制边界）。
- 跨域（actor 间消息）：必然经过序列化，MessagePack 默认 / JSON 可配置（ADR-0004）。
- 序列化是**显式边界行为**：每个跨域消息类型声明 `[MessagePackObject]`（或契约标记），在契约接口上声明，不在代码里隐式序列化——避免"哪一层在序列化"的隐式歧义。
- 配合 T7：MessagePack.Generator 源生成器生成序列化代码，宿主侧零运行时反射。

### 3.5 管道：ASP.NET Core 中间件形状（T8）

- 管道不引 ASP.NET Core 包，只借其**形状**：中间件委托链 + `await next(ctx)` 前后即 before/after（waterfall 语义，04-pipeline §2）。
- 插件 = 中间件，挂载在能力域 context 上；管道本身与 actor 同生命周期，节点（插件）可热重载替换。
- 事件轨（parallel/emit）与请求链（waterfall）双轨：请求链走管道，观察者走事件（01-overview D4）。

> **实现备注（2026-08-16，按代码核对）**：管道组成当前**经代码传入**——`CapabilityDomain.Spawn(name, handler, middlewares, ...)` 由嵌入方在 spawn 时给定，运行期热换走 `SwapPipelineAsync` 原子替换（DC-10/P40）。**配置层条目尚无 pipeline 声明字段**（`EntryOptions` 无此字段，01 §2 原案"管道组成（中间件顺序）"为预留面）；如需配置驱动管道组成，走 ADR 扩展条目 schema。

### 3.6 AOT 就绪：Source Generator（T7）

规则 0 的执行支柱：

- 序列化：MessagePack / System.Text.Json 源生成器生成契约代码，不依赖运行时反射序列化。
- 编译期已知类型：业务代码禁止运行时反射，用源生成器或编译期类型替代。
- **刻意例外**：插件加载层（T2 Roslyn + T3 ALC）是唯一排除在 AOT 标准之外的区域（ADR-0002 例外声明）。

### 3.7 治理：中央治理库 D01-D08 / R00-R20（T9）

项目接入 `~/Projects/central-governance`，规则按语言分层引用：

- **D 系列（.NET/C# 语言规则）**：D01 C# 代码风格、D02 SmartEnum（禁原生 enum）、D03 命名约定、D04 现代 C# 语法、D05 测试规范、D06 测试陷阱、D07 上帝类禁令、D08 过时标记规范。
- **R 系列（通用规则）**：R00 错误知识库 MCP、R02 安全基线、R03 Git 提交、R04 TDD、R10 文档治理（frontmatter 标准 + 一致性检查）、R11 验证透明度、R16 Worktree 隔离、R17 软件设计原则等。

技术选型与规则的关系：**规则约束写法，技术栈决定能力**——例如 T7 源生成器是 D02/D04 的技术前提，T4 Keyed Services 是 D02 的落地工具，R10 约束本文自身的 frontmatter 格式。

### 3.8 AI 能力域组合：MAF（T10）+ 文件式应用（T11）

- **T10 单向组合**（ADR-0008）：llm/agents/skills/mcp/workflow 能力域适配器引用 `Microsoft.Agents.AI.*` 包族；**框架核心不引用任何 MAF 包**，通用插件运行时独立成立。MAF 基于微软 DI，与 Keystone 服务模型可共存（T4 为自建键控 store，见 §2 表）。**MCP 协议层**：`Microsoft.Agents.AI.Mcp` 无稳定版 → 组合官方稳定协议 SDK `ModelContextProtocol.Core` 2.2.0（`Keystone.AI/Mcp/`，ID-12；agent 集成层待 MAF 稳定）。
- **T11 插件脚本形态**：插件默认 = .NET 10 文件式应用（单文件 .cs + 顶层语句，`dotnet run app.cs` 心智模型），manifest `dependencies` ↔ `#:package` 引用声明；复杂插件（多文件/资源/私有依赖）走 DLL 轨，复用 T3 ALC 管线。Roslyn 编译管线（T2）对两种形态同一套。
- **组合包 AOT 验收门**（规则 0 扩展，ADR-0008 风险缓解）：T10 组合的每个 MAF/MCP 包在纳入前验证 AOT 兼容（`PublishAot` 冒烟）；提交前跑规则 0 的标准验证命令，组合包不豁免。

## 4. 版本确认方式

- 本文是"已确认技术栈"总表：基线与选型已定（T1-T14），**NuGet 具体版本以 `Directory.Packages.props` 为唯一真源**（中心化包管理 CPM），本文版本列为快照（2026-08-16 回填），升级时同步。
- 新增技术选型必须走 ADR 流程（decisions/README.md），并回填本文。
- 本文变更遵守文档治理规则（R10）：改 AGENTS.md 索引 + 本文 + 关联 ADR 同步。

## 5. 与既有文档的关系

| 文档 | 关系 |
|------|------|
| 01-overview.md | 三层架构总纲（本文是它的技术栈支撑） |
| 02-plugin-model.md | T2/T3/T4 的详细设计 |
| 03-context.md | T5 的详细设计 |
| 04-pipeline.md | T8 的详细设计 |
| 05-reliability.md | T1 supervision 的详细设计 |
| 06-contracts.md | T6/T7 的详细设计 |
| 08-configuration-layer.md | 配置层专题（配置形态/schema 校验；T12/T13） |
| 09-management-layer.md | 管理层专题（启动/关闭/监督接线） |
| 10-plugin-sdk.md | 插件 SDK（接口面/模板工程） |
| 11-gap-register.md | 差距跟踪表（状态矩阵） |
| 12-cordis-semantics-mapping.md | 语义映射参考（被弃用机制的 C# 对应物） |
| decisions/adr-0001 ~ 0018 | 安全边界/来源、AOT vs JIT、并发模型、消息契约、生命周期、分发模式、依赖门控、AI 能力域组合等全部决策依据 |
