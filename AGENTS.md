---
type: project-index
tags: [cordis-csharp, architecture, dotnet, plugin-system]
created: 2026-08-15
---

# Cordis C# 方案（cordis-csharp）

> 将 DeepSeek Harness 的 vendored Cordis 插件框架理念，用 C# / .NET 重新实现的标准方案。
> 本目录是方案文档的唯一真源（Single Source of Truth），其余文档只放指针。

## 项目定位

C# 版 Cordis 插件框架：配置驱动、多实例隔离、热重载、中间件管道式的插件执行模型。
不重造 DI/中间件/配置等 .NET 已提供的能力，只实现 Cordis 独有的部分（ALC 插件加载、按插件 ID 分组回收、管道配置 schema、插件 SDK）。

## 文档索引

| 文档 | 内容 | 状态 |
|------|------|------|
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
