---
type: adr
tags: [cordis-csharp, adr, actors, proto-actor, aot]
created: 2026-08-15
status: accepted
id: adr-0015
---

# ADR-0015：Proto.Actor 保留 + 库自身 AOT 警告例外

## 背景

P8 能力域 actor 落地时探测（Proto.Actor 1.8.0，`dotnet publish -r osx-arm64 -p:PublishAot=true`）：
**发布成功，但 Proto.Actor 程序集自身产生 AOT 分析警告**（IL2104 trim warnings / IL3053 AOT warnings）。这些警告来自**第三方库内部**（非本项目代码），无法通过我们的编码消除，与规则 0"裁剪告警视为构建错误"直接冲突。

## 决策

1. **保留 Proto.Actor 1.8.0**（T1 技术栈不变，用户拍板）
2. **规则 0 例外扩展（第二处）**：引用 Proto.Actor 的工程（Keystone.Runtime）`NoWarn IL2104;IL3053`——例外范围仅限 **Proto.Actor 库自身的 AOT 警告**；本项目代码仍零告警（宿主自身代码的裁剪告警依旧视为错误）
3. 例外理由与范围记录在 00-tech-stack T1 行 + AGENTS 规则 0 例外声明

## 理由

1. **T1 已定案**：Proto.Actor 是 00-tech-stack 已确认技术栈（能力域 actor 串行循环/监督）
2. **能力价值**：actor 模型/监督/路由/集群是能力域编排的成熟底座；自实现仅覆盖串行+监督（1% 能力），未来路由/集群需求仍需 Proto.Actor
3. **警告无害**：IL3053/IL2104 是"可能破坏功能"的静态分析提示，Proto.Actor 在 JIT 运行时（本项目现状，ADR-0002）无实际影响；AOT 切换时再评估（届时可能需专用 actor 运行时）

## 权衡与风险

- **风险**：AOT 全量切换（ADR-0002 未来场景）时 Proto.Actor 可能有运行时问题——缓解：AOT 切换专项评估（届时重测 + 必要时独立程序集/替换）
- **例外扩散风险**：规则 0 例外从"仅加载层"扩到"加载层 + Proto.Actor 警告"——缓解：例外明确限定"库自身警告"，本项目代码零告警不变

## 备选方案

1. 自实现 actor 形状（Channel 串行循环）——用户否决（放弃 Proto.Actor 全能力不值）
2. 独立程序集隔离（Keystone.Actors 非 AOT 严格区）——用户否决（架构多一层，当前无需）

## 影响

- 00-tech-stack T1 行补注（AOT 警告例外，ADR-0015）
- AGENTS 规则 0 例外声明补第二处例外
- P8 实现：Keystone.Runtime.Actors（CapabilityActor/CapabilityDomain）基于 Proto.Actor

## 关联

- 00-tech-stack T1、AGENTS 规则 0、ADR-0002（JIT 现状）
- 13-implementation-plan P8、14-implementation-log W8-*
