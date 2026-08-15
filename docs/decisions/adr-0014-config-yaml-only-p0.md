---
type: adr
tags: [cordis-csharp, adr, configuration, providers]
created: 2026-08-15
status: accepted
id: adr-0014
---

# ADR-0014：开发阶段配置源收敛为 YAML，配置中心延后启用

## 背景

ADR-0013 原定默认组合为"本地 YAML + AgileConfig 配置中心双源"。P0 落地后评审：**开发阶段引入配置中心属于过度设计**——增加网络依赖、部署与运维复杂性，而当前阶段配置形态尚未稳定（条目模型/分层叠加在 P6 才定型）。

## 决策

1. **默认组合仅本地 YAML**：`KeystoneConfigBuilder.CreateDefault()` = 可选 `keystone.yml`（`cordis.yml` 生态的条目模型不变）
2. **优先级**：YAML > 代码内文档化默认值（`KeystoneSettings`）
3. **AgileConfig 配置中心降为预留可选源**：提供者代码**保留**（`AddAgileConfig` 显式追加可用），不进入默认组合；后续阶段（如部署环境需要集中管理）按需启用，届时优先级沿用 M.E.C"后添加者优先"（配置中心 > YAML > 默认值）
4. **提供者抽象不受影响**：ADR-0013 的 `IConfigurationSource` 契约与用户自实现路径保持——来源多样性能力就绪，只是默认组合收敛

## 理由

1. **开发阶段简单性**：YAML-only 无网络/部署依赖，离线可跑、可测、可审计
2. **抽象已就绪**：配置中心启用是"Add 一行"级变更（`AddAgileConfig`），无架构改动——延后不损失灵活性
3. **配置形态未稳**：条目模型/分层叠加（P6）定型前，配置中心里的配置结构还会变，过早接入中心徒增迁移成本

## 权衡与风险

- **延后代价**：暂无集中配置能力——开发阶段不需要；若提前需要，`AddAgileConfig` 立即可用
- **测试保留**：AgileConfig 提供者测试（`AgileConfigConfigurationProviderTests` + 覆盖语义测试）保留，防预留代码腐化

## 备选方案

1. **维持双源默认**——被否决：开发阶段复杂性收益为负（用户拍板收敛）
2. **移除 AgileConfig 代码**——被否决：抽象能力与测试已就绪，删除属浪费（保留为可选源）

## 影响

- 08-configuration-layer §2：默认源改为 YAML，配置中心标注预留
- README/AGENTS：定位与核心机制表"默认 YAML/AgileConfig 双源"表述同步
- 测试：`CreateDefault` 语义测试更新（仅 YAML）
- 14-implementation-log：W0-07 记录本次收敛

## 关联

- ADR-0013（被收敛的对象，不修改其正文）
- 08-configuration-layer §2、13-implementation-plan P0/P6、14-implementation-log W0-07
