---
type: index
tags: [cordis-csharp, decisions, adr]
created: 2026-08-15
---

# 决策记录（ADR）索引

> 设计期全部待定决策已收敛为 ADR。新决策落地前先查本索引，避免重复决策。

| ADR | 主题 | 状态 | 日期 |
|-----|------|------|------|
| [adr-0001-plugin-security-and-source.md](adr-0001-plugin-security-and-source.md) | 插件安全边界（同进程可信代码默认）+ 插件来源（本地起步演进） | accepted | 2026-08-15 |
| [adr-0002-aot-vs-jit.md](adr-0002-aot-vs-jit.md) | AOT vs JIT（JIT + Roslyn 动态编译，不采用 NativeAOT） | accepted | 2026-08-15 |
| [adr-0003-context-concurrency-pipeline-hot-reload.md](adr-0003-context-concurrency-pipeline-hot-reload.md) | context 并发模型（串行默认）+ 管道配置热更新（原子替换） | accepted | 2026-08-15 |
| [adr-0004-message-contracts.md](adr-0004-message-contracts.md) | 消息契约（Payload 强类型 + 显式序列化）+ 跨域编排（TaskId 贯穿 + 全等聚合） | accepted | 2026-08-15 |
| [adr-0005-plugin-lifecycle-quiesce.md](adr-0005-plugin-lifecycle-quiesce.md) | 插件生命周期状态机 + quiesce 收敛协议（差距 G1/G2/G3） | accepted | 2026-08-15 |
| [adr-0006-event-dispatch-modes.md](adr-0006-event-dispatch-modes.md) | 事件分发模式全集（serial/bail 纳入，差距 G10） | accepted | 2026-08-15 |
| [adr-0007-dependency-gating.md](adr-0007-dependency-gating.md) | 依赖门控激活 + manifest 服务级依赖 inject（差距 G4/G5/G13） | accepted | 2026-08-15 |
| [adr-0008-ai-capability-composition.md](adr-0008-ai-capability-composition.md) | AI 能力域组合（组合微软官方 MAF/MCP，单向依赖，不重造 AI 底层） | accepted | 2026-08-15 |
| [adr-0009-event-persistence.md](adr-0009-event-persistence.md) | 事件持久化（事实事件 append-only 事件日志 + IEventStore 抽象 + 重放/保留） | accepted | 2026-08-15 |
| [adr-0010-intercept-check-tradeoff.md](adr-0010-intercept-check-tradeoff.md) | G6/G9 取舍（弃用 intercept 通用语义与 check 谓词，显式决策记录） | accepted | 2026-08-15 |
| [adr-0011-config-expression-drop.md](adr-0011-config-expression-drop.md) | 弃用配置内动态表达式（!!js），静态插值/分层叠加替代（复查 F1） | accepted | 2026-08-15 |
| [adr-0012-yaml-static-interpolation.md](adr-0012-yaml-static-interpolation.md) | 保留 YAML 自定义 tag 做静态插值（!!env/!!file，解析≠求值边界澄清，补充 ADR-0011） | accepted | 2026-08-15 |
| [adr-0013-config-provider-abstraction.md](adr-0013-config-provider-abstraction.md) | 配置提供者抽象（Keystone.Config）：M.E.C IConfigurationSource 契约 + 默认 YAML/AgileConfig 双源 + 禁止硬编码 | accepted | 2026-08-15 |
| [adr-0015-proto-actor-aot-exception.md](adr-0015-proto-actor-aot-exception.md) | Proto.Actor 保留 + 库自身 AOT 警告例外（规则 0 第二处例外） | accepted | 2026-08-15 |
| [adr-0016-config-format-yaml-only.md](adr-0016-config-format-yaml-only.md) | 配置格式收敛 YAML-only，弃用 JSON（CA-8；插值互斥 + IConfigProvider 后门） | accepted | 2026-08-16 |
| [adr-0014-config-yaml-only-p0.md](adr-0014-config-yaml-only-p0.md) | 开发阶段配置源收敛为 YAML（配置中心延后启用，AgileConfig 降为预留可选源，收敛 ADR-0013） | accepted | 2026-08-15 |

## 约定

- ADR 编号顺序递增（adr-000N）
- 结构：背景 / 决策 / 理由 / 权衡风险 / 备选方案 / 影响 / 关联
- 决策被推翻时新建 ADR，不修改已 accepted 的旧 ADR（保留决策历史）
