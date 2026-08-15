---
type: project-index
tags: [cordis-csharp, architecture, dotnet, plugin-system]
created: 2026-08-15
---

# Keystone 地基插件框架（cordis-csharp）

> 基于 .NET 的通用地基插件框架：任何 C# 应用可嵌入。插件理念受 DeepSeek Harness vendored Cordis 启发（参照基线），**独立命名与定位，非 Cordis 再实现**；"Cordis" 在本仓仅作参照上游引用。
> 本目录是方案文档与实现进度的唯一真源（Single Source of Truth），其余文档只放指针。

## ⚠️ 规则 0（最高优先级，先于一切规则）：AOT 就绪编码标准

**本项目当前不采用 NativeAOT（见 ADR-0002：JIT + Roslyn 动态编译），但所有代码必须按 AOT 兼容标准编写。** 后期如需切换 AOT，零改动直接可用。

强制约束：

1. **禁止依赖运行时 JIT 生成**：不写 `Reflection.Emit`、`Expression.Compile`、运行时生成动态程序集（Roslyn 插件编译是刻意例外，见 ADR-0002——插件走独立 ALC/JIT，宿主本身保持 AOT 兼容）
2. **反射受限**：`Assembly.LoadFrom`/`Activator.CreateInstance` 按类型名动态加载仅在插件加载层使用（该层天然排除在 AOT 外）；业务代码禁止运行时反射，改用源码生成器（Source Generator）或编译期已知类型
3. **序列化显式**：不依赖运行时反射序列化——DTO 使用 `[MessagePackObject]`/`[JsonSerializable]` 等源生成友好契约（与 ADR-0004 一致）
4. **禁止动态代码执行**：不调用 `CSharpScript`、`CodeDom`、`Assembly.Load(byte[])` 于宿主路径
5. **配置绑定静态**：`IOptions` 配置绑定用编译期泛型（`Options.Create<T>` 默认即 AOT 安全），不写反射式绑定
6. **规避 AOT 不兼容 API**：`BinaryFormatter`、`Type.GetType(string)`（运行时解析）、`Marshal.GetFunctionPointerForDelegate` 仅限显式 P/Invoke 场景
7. **诊断产物可裁剪**：代码结构（类型/成员/引用图）保持静态可分析——`rd.xml`/`ILLink` 裁剪告警视为构建错误处理

**验证方式**：每次提交前跑 `dotnet publish -c Release -r <rid> --self-contained /p:PublishAot=true` 冒烟（若当前 JIT 配置下该命令可用则必须跑通；不可用则跑 `dotnet build -warnaserror` 确认无裁剪相关警告）。

> 例外声明（两处）：① ADR-0002——插件运行时由 Roslyn 内存编译进独立 ALC，**插件加载层（Roslyn/ALC）刻意排除在 AOT 标准之外**；② ADR-0015——**Proto.Actor 库自身的 AOT 警告（IL2104/IL3053）例外**（宿主自身代码零告警不变）。其余部分一律遵守本条。

## 项目定位

Keystone 通用地基插件框架：配置驱动、多实例隔离、热重载、中间件管道式的插件执行模型；任何 C# 应用可嵌入，不绑定业务领域。
不重造 DI/中间件/配置等 .NET 已提供的能力，只实现框架独有的部分（ALC 插件加载、按插件 ID 分组回收、管道配置 schema、插件 SDK）。
配置来源解绑（ADR-0013）：提供者抽象 + 默认本地 YAML（开发阶段，ADR-0014；AgileConfig 配置中心为预留可选源），用户可自实现；禁止硬编码，框架可调值一律走配置。
AI 底层（LLM 适配/技能包/MCP/agent 编排）组合微软官方 MAF/MCP，不重造（ADR-0008）。

## 文档索引

| 文档 | 内容 | 状态 |
|------|------|------|
| [architecture/00-tech-stack.md](architecture/00-tech-stack.md) | 技术体系：技术基线（.NET 10 + C# 14）+ 已确认技术栈清单（Proto.Actor 1.8 等） | 标准 |
| [architecture/01-overview.md](architecture/01-overview.md) | 方案总览：三层架构（配置层/管理层/能力域 actor） | 标准 |
| [architecture/02-plugin-model.md](architecture/02-plugin-model.md) | 插件模型：接口白名单、键控服务、子容器、热重载 | 标准 |
| [architecture/03-context.md](architecture/03-context.md) | Context 设计：作用域链、状态外置、事件分层 | 标准 |
| [architecture/04-pipeline.md](architecture/04-pipeline.md) | 管道设计：中间件模式、waterfall 语义、双轨事件 | 标准 |
| [architecture/05-reliability.md](architecture/05-reliability.md) | 可靠性：错误处理、监督策略、超时熔断、可观测性 | 标准 |
| [architecture/06-contracts.md](architecture/06-contracts.md) | 消息契约：请求模型、请求 ID、链路追踪 | 标准 |
| [architecture/07-cordis-migration-gap.md](architecture/07-cordis-migration-gap.md) | Cordis 迁移差距：7 必查项结论 + 差距清单/优先级/影响 | 标准 |
| [architecture/08-configuration-layer.md](architecture/08-configuration-layer.md) | 配置层：配置形态、条目模型、分层叠加、schema 校验、热更新触发 | 标准 |
| [architecture/09-management-layer.md](architecture/09-management-layer.md) | 管理层：启动流程、监督接线、进程级优雅关闭 | 标准 |
| [architecture/10-plugin-sdk.md](architecture/10-plugin-sdk.md) | 插件 SDK：接口面、配置注入、计时器、manifest schema、模板工程 | 标准 |
| [architecture/11-gap-register.md](architecture/11-gap-register.md) | 差距跟踪表：G1-G16 + 补充排查项的处理状态矩阵（07 是快照，本文是现状） | 标准 |
| [architecture/12-cordis-semantics-mapping.md](architecture/12-cordis-semantics-mapping.md) | 语义映射参考：被弃用/未解析 Cordis 机制（intercept/check、H/M/L/F 系列）的 C# 对应物字典 + 导出面穷举审计凭证 | 标准 |
| [architecture/13-implementation-plan.md](architecture/13-implementation-plan.md) | 分阶段实施计划：M0-M13 里程碑、每阶段目标/验收条件/DoD、待定项分配 | 标准 |
| [architecture/14-implementation-log.md](architecture/14-implementation-log.md) | 实施记录：工作日志/实现期决策/偏差/验收台账/三向回溯索引（与 13 配套） | 标准 |
| [architecture/15-decoupling-plan.md](architecture/15-decoupling-plan.md) | 解耦工作计划：第三方依赖耦合审计（C1-C8）+ 分阶段隔离计划（D1-D5） | 标准 |
| [architecture/16-cordis-gap-review.md](architecture/16-cordis-gap-review.md) | Cordis 功能差距复核（实现后）：G-C1~C14 差距清单（配置注入/依赖恢复/值注销等）+ 建议计划 | 标准 |
| [decisions/](decisions/README.md) | 决策记录（ADR-0001 ~ 0015，设计期已收敛；实现期新决策走 14 §4 通道） | accepted |

## 治理

- 项目接入中央治理库（`~/Projects/central-governance`），规则引用 D01-D08 + R00-R20
- 新决策落地前写 ADR 到 `decisions/`
- 方案文档改动走文档治理规则（R10）

## 加工件说明（看板流水线使用）

当前阶段：**实现期完成（M0-M13 全部通过，215 测试全绿）+ P14 MCP 协议层落地 + P15-P20 解耦完成 + P21 集成验收 + P22 接入 B3/B4 + P23-P26 Cordis 差距高危项闭合（G-C1 配置注入 / G-C2 依赖 re-arm / G-C3 值注销）**。实现推进按 13-implementation-plan（13 阶段全部落地），过程记录按 14-implementation-log；六工程（Core/Config/Runtime/Hosting/Sdk/AI）+ 全工程 AOT 零 IL 警告。

- 构建：`dotnet build cordis-csharp.slnx`（已存在；警告即错误）
- 测试：`dotnet test cordis-csharp.slnx`（已存在；215 个单测绿，M0-M13 全阶段 + P14-P26）
- **实现纪律（13 §6）**：TDD 测试先行（红→绿→重构）；设计模式 + 契约/错误/实现边界抽象隔离；跨层单向依赖
- 文档校验：`cd ~/Projects/central-governance && python3 scripts/validate_frontmatter.py`
- 设计文档改动必须同步：AGENTS.md 索引、docs/architecture/ 对应文档、decisions/ ADR
- 参考项目：cognitive-tree-csharp（同构看板，slug cognitivetree-c / cognitive-tree-csharp）
