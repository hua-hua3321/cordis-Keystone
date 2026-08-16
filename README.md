# Keystone 地基插件框架（cordis-csharp）

> 基于 .NET 的**通用地基插件框架**：任何 C# 应用可嵌入（配置驱动、多实例隔离、热重载、中间件管道式执行模型）。
> **命名与定位声明**：Keystone 是独立命名的地基框架，插件理念受 DeepSeek Harness vendored Cordis 启发（作为参照基线），**非 Cordis 官方再实现、不占用 Cordis 名义**；Cordis 一词在本仓库仅作参照上游引用。

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dot.net/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![CI](https://github.com/hua-hua3321/cordis-Keystone/actions/workflows/ci.yml/badge.svg)](https://github.com/hua-hua3321/cordis-Keystone/actions/workflows/ci.yml)
[![Docs](https://img.shields.io/badge/docs-architecture-blue)](docs/architecture/)

**Keystone** is a universal, configuration-driven plugin framework for .NET — a
plugin substrate any C# application can embed, with multi-instance isolation,
hot reload, and a middleware-pipeline execution model.

## 📚 文档 / Documentation

| 资源 | 链接 |
|------|------|
| 快速上手（中文） | [docs/tutorials/getting-started.md](docs/tutorials/getting-started.md) |
| Getting Started (English) | [docs/tutorials/getting-started.en.md](docs/tutorials/getting-started.en.md) |
| 架构文档（20 篇） | [docs/architecture/](docs/architecture/) |
| 设计决策 ADR-0001~0018 | [docs/decisions/](docs/decisions/) |
| 英文 README | [README.en.md](README.en.md) |
| 贡献指南（中 / 英） | [CONTRIBUTING.md](CONTRIBUTING.md) · [CONTRIBUTING.en.md](CONTRIBUTING.en.md) |
| 行为准则（中 / 英） | [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) · [CODE_OF_CONDUCT.en.md](CODE_OF_CONDUCT.en.md) |
| 安全政策（中 / 英） | [SECURITY.md](SECURITY.md) · [SECURITY.en.md](SECURITY.en.md) |
| 支持与问答（中 / 英） | [SUPPORT.md](SUPPORT.md) · [SUPPORT.en.md](SUPPORT.en.md) |
| 更新日志（中 / 英） | [CHANGELOG.md](CHANGELOG.md) · [CHANGELOG.en.md](CHANGELOG.en.md) |

## 当前状态

**实现期完成（M0-M13 全部通过，500 测试全绿）**：Keystone 框架 1.0 可运行——核心（契约/上下文/事件/生命周期/管道/加载/配置/管理层/能力域/观测/持久化/SDK/AI 组合）十三阶段全部落地，全工程 AOT 零 IL 警告（Proto.Actor/MAF 例外按 ADR-0015 记录）。本仓库是方案与实现进度的唯一真源（Single Source of Truth）。

## 项目定位

- **通用地基**：任何 C# 应用（web/服务/桌面/嵌入式宿主）可把 Keystone 作为插件底层嵌入；不绑定任何具体业务领域
- **不重造**：DI（IServiceProvider）、中间件管道（ASP.NET Core 形状）、配置（IOptions + M.E.C 提供者抽象）、日志（ILogger）、后台服务（IHostedService）、**AI 底层（LLM 适配/技能包/MCP/agent 编排——组合微软官方 MAF/MCP，ADR-0008）**
- **只实现框架独有的部分**：ALC 插件加载层、按插件 ID 分组的注册回收、管道配置 schema、插件 SDK
- **配置解绑**：配置来源不锁死——提供者抽象（ADR-0013），默认本地 YAML（开发阶段，ADR-0014），AgileConfig 配置中心为预留可选源，用户可自实现任意来源；禁止硬编码（框架可调值一律走配置）

## 核心机制

| 机制 | 说明 | 文档 |
|------|------|------|
| 插件热重载 | Roslyn 内存编译 + 私有 Collectible ALC + quiesce 收敛闸门 | [02-plugin-model.md](docs/architecture/02-plugin-model.md)、ADR-0005 |
| 依赖门控激活 | manifest `inject` 服务级依赖，PENDING 等待依赖就绪 | ADR-0007 |
| 事件分发 | emit / parallel / serial / bail / waterfall 五种模式 | ADR-0006 |
| 多实例隔离 | 独立 context + 子容器 + 服务级 isolate | [03-context.md](docs/architecture/03-context.md) |
| 配置提供者抽象 | M.E.C `IConfigurationSource` 契约 + 默认本地 YAML（AOT 安全 YamlStream 解析）；AgileConfig 配置中心预留可选源 | ADR-0013/0014、[08-configuration-layer.md](docs/architecture/08-configuration-layer.md) |
| AI 能力域组合 | 组合微软官方 MAF/MCP（SEP-2640 技能包、MCP 双端、Workflows 编排），单向依赖，核心不依赖 | ADR-0008 |

## 文档索引

- 架构：`docs/architecture/`（00 技术栈 ~ 19 第二轮等价性复核，见 [AGENTS.md](AGENTS.md) 索引）
- 决策：`docs/decisions/README.md`（ADR-0001 ~ 0018，设计期已收敛；实现期新决策走 14 §4 通道）
- 差距分析：[07-cordis-migration-gap.md](docs/architecture/07-cordis-migration-gap.md)（对照 vendored Cordis 源码的 7 必查项 + 差距清单）
- 实施推进：[13-implementation-plan.md](docs/architecture/13-implementation-plan.md)（分阶段计划 + 里程碑）+ [14-implementation-log.md](docs/architecture/14-implementation-log.md)（过程记录 + 回溯索引）

## 构建与测试

```bash
dotnet build cordis-csharp.slnx          # 警告即错误（TreatWarningsAsErrors + 分析器）
dotnet test cordis-csharp.slnx           # 500 个单测（M0-M13 全阶段 + P14-P72 审计批）
```

规则 0 AOT 冒烟（每阶段验收，13 §4）：

```bash
dotnet publish src/Keystone.Core -c Release -r osx-arm64 --self-contained /p:PublishAot=true
dotnet publish src/Keystone.Config -c Release -r osx-arm64 --self-contained /p:PublishAot=true
```

文档校验（R10）：

```bash
cd ~/Projects/central-governance && python3 scripts/validate_frontmatter.py
```

## 治理

- 中央治理库：`~/Projects/central-governance`（规则 D01-D08 + R00-R20）
- 规则 0：AOT 就绪编码标准（最高优先级，见 [AGENTS.md](AGENTS.md)）——当前 JIT（ADR-0002），代码必须按 AOT 兼容标准编写
- 新决策落地前写 ADR；方案文档改动走文档治理规则（R10）

## 参考项目

- cognitive-tree-csharp（同构看板，slug cognitivetree-c / cognitive-tree-csharp）
- DeepSeek Harness（vendored Cordis 源码基线：`~/Projects/deepseek-harness/vendor/cordis/src/`——仅作理念参照）

---

## 🤝 社区与贡献 / Community

欢迎参与！请先阅读[贡献指南](CONTRIBUTING.md)与[行为准则](CODE_OF_CONDUCT.md)。
提交 Bug 或功能建议请使用 GitHub Issue 模板；安全问题请见[安全政策](SECURITY.md)
**不要**公开提 issue。获取帮助见[支持文档](SUPPORT.md)。

## 📄 许可证 / License

本项目以 [MIT 许可证](LICENSE) 开源。© 2026 Keystone contributors.
