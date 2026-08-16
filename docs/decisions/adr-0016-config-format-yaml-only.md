---
type: adr
tags: [cordis-csharp, adr, configuration]
created: 2026-08-16
status: accepted
id: adr-0016
---

# ADR-0016：配置格式收敛 YAML-only，弃用 JSON 支持（CA-8）

## 背景

Cordis include 支持三种配置形态：YAML / JSON / 模块（18 §2 CA-8）。Keystone 当前仅 YAML（ADR-0014 开发阶段定位），审计提出"是否补 JSON"待决策。

## 决策

**配置格式收敛 YAML-only，不补 JSON 支持**（2026-08-16 人工裁定，仅弃用 CA-8；CA-11 `cordis:` 内建前缀同轮决策保留为扩展点）。

## 理由

1. **静态插值互斥**：`!!env`/`!!file` 静态插值（DC-8/P38）依赖 YAML tag 机制；JSON 无 tag 概念——支持 JSON 意味着放弃插值或维护双轨解析器
2. **单格式降低矩阵**：解析/写回/校验/watcher/patch 五条管线 × 格式数的测试与维护矩阵翻倍，收益仅"JSON 手写方便"一项
3. **YAML 是 JSON 超集**：YAML 解析器天然接受 JSON 流式语法（flow style）——严格 JSON 文件可直接按 YAML 读，无需独立支持
4. **ADR-0013 已留后门**：未来确需异构格式，走 `IConfigProvider` 抽象自实现（声明不支持 `!!env`），不动核心管线

## 权衡与风险

- **损失**：偏好 JSON 的嵌入方需适应 YAML（或自实现 provider）。接受——框架配置由宿主方而非终端用户编辑
- **边界圈定**：initial 引导（CA-6）与 readonly 降级（CA-7）当前绑定文件后端（ConfigFileWriter），与 ADR-0013 配置源抽象的张力已在 18 §5.1 注记——非本 ADR 范围，若写回改走 IConfigProvider 另立专项

## 备选方案

1. **双格式支持（YAML+JSON）**——被否决：插值互斥 + 矩阵翻倍
2. **JSON 走独立 provider 默认内置**——被否决：违背"默认本地 YAML"的 ADR-0014 定位，留给嵌入方按需自实现

## 影响

- 18-cordis-code-parity-audit CA-8 关闭（弃用）
- EntryParser/EntrySerializer/ConfigFileWatcher/PluginFileWatcher 全部 YAML 单格式
- 11-gap-register §3.3 CA-8 状态回写

## 关联

- ADR-0014（本地 YAML 开发阶段配置源）、ADR-0013（配置来源解绑）
- 18-cordis-code-parity-audit §2 CA-8、11-gap-register §3.3
