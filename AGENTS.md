---
type: project-index
tags: [cordis-csharp, architecture, dotnet, plugin-system]
created: 2026-08-15
---

# Cordis C# 方案（cordis-csharp）

> 将 DeepSeek Harness 的 vendored Cordis 插件框架理念，用 C# / .NET 重新实现的标准方案。
> 本目录是方案文档的唯一真源（Single Source of Truth），其余文档只放指针。

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

> 例外声明：ADR-0002 明确插件运行时由 Roslyn 内存编译进独立 ALC——**插件加载层（Roslyn/ALC）是刻意排除在 AOT 标准之外的唯一区域**，宿主其余部分一律遵守本条。

## 项目定位

C# 版 Cordis 插件框架：配置驱动、多实例隔离、热重载、中间件管道式的插件执行模型。
不重造 DI/中间件/配置等 .NET 已提供的能力，只实现 Cordis 独有的部分（ALC 插件加载、按插件 ID 分组回收、管道配置 schema、插件 SDK）。

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
| [decisions/](decisions/README.md) | 决策记录（ADR-0001 ~ 0004，设计期已收敛） | accepted |

## 治理

- 项目接入中央治理库（`~/Projects/central-governance`），规则引用 D01-D08 + R00-R20
- 新决策落地前写 ADR 到 `decisions/`
- 方案文档改动走文档治理规则（R10）

## 加工件说明（看板流水线使用）

当前阶段：**设计期**（只有 docs/，无代码）。

- 构建：`dotnet build cordis-csharp.slnx`（尚无 slnx，代码落地后创建）
- 测试：`dotnet test cordis-csharp.slnx`（尚无测试项目，代码落地后创建）
- 文档校验：`cd ~/Projects/central-governance && python3 scripts/validate_frontmatter.py`
- 设计文档改动必须同步：AGENTS.md 索引、docs/architecture/ 对应文档、decisions/ ADR
- 参考项目：cognitive-tree-csharp（同构看板，slug cognitivetree-c / cognitive-tree-csharp）
