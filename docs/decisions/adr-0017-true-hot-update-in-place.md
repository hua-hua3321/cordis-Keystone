---
id: ADR-0017
title: 真热更新——config-only 原地重启（同 ALC 新实例）
status: accepted
date: 2026-08-15
deciders: [tovis, keystone-agent]
tags: [hot-update, lifecycle, alc, d-1]
---

# ADR-0017：真热更新——config-only 原地重启（同 ALC 新实例）

## 背景

19 号审计 D-1（LD-6）发现：修复前的"热路径"（`UpdatePluginAsync` / `UpdateEntryAsync` 热分支）
内部仍调用 `ReloadPluginAsync`——**重编译源码 + 新 ALC + 旧 ALC 卸载**。即"仅 config 变更"
也要付出冷重启全价：

- **源依赖**：源码损坏/获取端故障时，config 热更新必然失败（重编译必读源）；
- **重量级**：Roslyn 编译 + ALC 分配 + 旧 ALC 卸载等待，config 微调代价不成比例；
- **语义偏离 Cordis**：fiber.ts `update(config)` = `internal/update` waterfall（可否决）→ 默认
  `restart()`——**同代码重新执行**（不重新 import/求值模块），config 变更从不触碰源码。

## 决策

config-only 变更走**原地通道**（`PluginLoader.UpdateConfigAsync`）：

1. quiesce 旧 runtime（effect 收敛 → 插件 dispose → 摘 provides 注册——与冷重启同收敛语义）；
2. **同 ALC 内** `Activator.CreateInstance` 新插件实例（缓存于加载时的 `_pluginType`）；
3. 新 `PluginRuntime`（新 config）启动。

**不重编译、不换 ALC、不触碰源码**——源坏时热更不受影响（对齐 Cordis"同代码 restart"）。

分级不变（08 §6.1）：

| 变更 | 通道 | 动作 |
|------|------|------|
| 仅 config | 原地（本 ADR） | 同 ALC 新实例 |
| 结构变（name/inject/isolate/parent/形状） | 冷重启 | 重编译 + 新 ALC + 旧卸载 |
| 源码变（CA-2 watcher） | 冷重启 | 同上 |

接线点：`UpdatePluginAsync` 热分支、`UpdateEntryAsync` 热分支、失败复原（loader 仍在时——
冷路径失败 loader 已拆卸则回退冷重启）。`PatchContext` 瀑布可否决语义保持（apply 前拦截）。

## 理由

- **正确性**：插件实例假设"每实例状态外置于 context"（03），dispose→重建不丢框架侧状态；
  静态状态在同一程序集上累积（与 Cordis 模块级缓存一致——fiber restart 不重置模块态）。
- **可用性**：热更的可用性与源码可用性解耦——运维窗口内源损坏不阻塞配置调优。
- **成本**：热路径 O(实例化) vs 冷路径 O(编译 + ALC)；量级差三个数量级。

## 后果

- 正面：D-1 闭合；热更失败面收窄（仅运行时启动失败）；19 号审计 D 系列全清。
- 负面/接受：同 ALC 累积的静态状态不重置（插件若误用静态存请求态，热更不清理）——与
  Cordis 语义一致，插件责任（SDK 文档明示）；`_pluginType` 反射实例化在加载层（ADR-0002
  例外域，IL2077/IL2074 使用点豁免）。
- 备选否决：① config 变更也走冷重启（修复前现状——源依赖 + 重量级，被本 ADR 取代）；
  ② 不重启仅通知 config 变更（Cordis fiber 无此路径——update 即 restart；且插件闭包无法
  观察新 config，语义不等价）。

## 验证

`TrueHotUpdateTests`（4 例）：源坏热更成功 / 同程序集原地重启（静态累积 +1）/ CA-4 热分支
同通道 / 结构变仍冷重启。全量 469 测试绿 + AOT 零警告。
