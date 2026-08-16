# 更新日志（Changelog）

> English version: [CHANGELOG.en.md](CHANGELOG.en.md)

本项目所有值得注意的变更都记录在本文件中。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，并遵循
[语义化版本](https://semver.org/lang/zh-CN/spec/v2.0.0.html)。

## [1.0.0] — 2026-08-15

**Keystone** 通用地基插件框架（.NET）的首个稳定版本。

### 新增

- **三层架构**：配置层、管理层（`KeystoneHost` / `CompositionRoot` actor）、
  能力域 actor。
- **插件模型**：文件式（.NET 10 文件级应用）与预编译 DLL 插件，经 Roslyn
  内存编译进私有可卸载 `AssemblyLoadContext`（ALC）。
- **生命周期与热重载**：`IPlugin` 契约、依赖门控激活（依赖就绪前保持
  `PENDING`）、真正的原地热更新（仅配置变化，ADR-0017）以及冷重启。
- **中间件管道**：`IMiddleware`，`await next()` 的 waterfall 语义与短路支持。
- **事件系统**：五种分发模式——`emit`、`parallel`、`serial`、`bail`、
  `waterfall`（ADR-0006）。
- **服务模型**：键控服务存储 + realm 隔离（`Provide` / `Get` / `Set`），
  通过编译期接口白名单强类型化（不使用 `Dictionary<string, object>`）。
- **配置层**：YAML 条目树、分层叠加、`!!env` / `!!file` 静态插值、schema
  校验，以及 fail-fast 的 manifest 检查（ADR-0013 / ADR-0014）。
- **AI 能力域组合**：组合微软官方 MAF / MCP（单向依赖，框架核心不依赖 AI
  包），支持 SEP-2640 技能包（ADR-0008）。
- **可观测性**：OpenTelemetry 链路追踪与指标钩子（ADR-0018）。
- **AOT 就绪代码库**：所有宿主代码按 AOT 兼容标准编写（规则 0），仅插件加载
  层为刻意例外。
- **插件 SDK 模板**：`dotnet new keystone-plugin` 脚手架。
- **500+ 单元测试**，覆盖全部里程碑（M0–M13）及等价性/审计轮次。

### 说明

- 本版本依据 ADR-0002 采用 **JIT**（非 NativeAOT）；代码库保持 AOT 兼容，
  未来切换成本低。
- 插件默认作为同进程可信代码执行（ADR-0001）。
