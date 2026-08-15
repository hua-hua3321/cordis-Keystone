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

## 约定

- ADR 编号顺序递增（adr-000N）
- 结构：背景 / 决策 / 理由 / 权衡风险 / 备选方案 / 影响 / 关联
- 决策被推翻时新建 ADR，不修改已 accepted 的旧 ADR（保留决策历史）
