---
id: ADR-0018
title: 观测性框架——OTel 骨架三层架构（探针/事实/导出）
status: accepted
date: 2026-08-15
deciders: [tovis, keystone-agent]
tags: [observability, tracing, logging, metrics, otel, proto-actor]
---

# ADR-0018：观测性框架——OTel 骨架三层架构

## 背景

消息传递模型（Proto.Actor）的调试成本必须用观测面偿还：P68 回归期间中间件异常被吞进
永不完成的 `Proto.Future`，零日志/零 trace/零痕迹，人工二分 29 个测试类才定位。用户裁定：
**日志与追踪系统必须比原始 Cordis 更好**，否则无法排错与审查。

现状缺口：
- `TraceContext` 用 `new Activity(...)`——非 `ActivitySource`：无采样协商，对 OpenTelemetry
  导出器不可见（OTel 只订阅 ActivitySource）；
- 仅故障路径落日志（P68 T11 最小响应），actor 消息进出边界无常规记录；
- 热/冷路径决策、监督动作、慢请求无任何观测面；
- 无指标。

## 决策（用户三选项裁定）

1. **追踪骨架 = OpenTelemetry**：探针层纯 BCL（`ActivitySource`/`Meter`/`ILogger`），
   OTel SDK 仅在组合根（Hosting）作为可选接线；
2. **默认 Console 导出器开启**（开发开箱即见 span/指标），生产经配置切 OTLP endpoint；
3. **指标首批全量**：actor 请求 + 监督 + 热更 + 写回（计数器 + 直方图）。

## 架构：三层

```
┌─ L1 探针层（Keystone.Runtime/Core —— 纯 BCL，零新依赖）──────────┐
│  ActivitySource "Keystone.Runtime" / Meter "Keystone.Runtime"     │
│  ILogger + LoggerMessage 源生成（结构化字段）                     │
└────────────────────────────────────────────────────────────────────┘
     │ 关联键：TaskId / EntryId（span tag ↔ log 字段 ↔ 事实事件）
┌─ L2 事实层（已有 EventStore，保持不动）───────────────────────────┐
│  TaskCompletedFact / TaskFailedFact / … + 新增监督事实            │
└────────────────────────────────────────────────────────────────────┘
     │
┌─ L3 组合/导出层（Keystone.Hosting —— 依赖 OpenTelemetry.*）───────┐
│  KeystoneHostOptions.Observability：Console（默认开）/ OTLP /     │
│  采样率 / 慢请求阈值；未配置 OTLP 时仅 console，探针近零开销      │
└────────────────────────────────────────────────────────────────────┘
```

### L1：span 分类表（ActivitySource "Keystone.Runtime"）

| Span | 触发点 | 关键 tag |
|------|--------|---------|
| `keystone.task` | 跨域任务执行（现有迁移） | task.id / task.parent / capability / operation |
| `keystone.config.apply` | ApplyConfigAsync 批次 | entries / failures / rolled-back |
| `keystone.config.entry` | 逐条目应用 | entry.id / channel=hot\|cold |
| `keystone.hotupdate` | 原地热更新（D-1 通道） | entry.id / old→new keys |
| `keystone.group.transaction` | 组事务 | group / outcome=applied\|rolled-back |

### L1：meter 分类表（Meter "Keystone.Runtime"）

| 指标 | 类型 | 维度 |
|------|------|------|
| `keystone.actor.requests` | counter | capability / instance |
| `keystone.actor.request_duration` | histogram(ms) | capability |
| `keystone.actor.faults` | counter | instance / fault.type |
| `keystone.supervision.restarts` | counter | instance |
| `keystone.hotupdate.operations` | counter | channel=hot\|cold |
| `keystone.writer.failures` | counter | — |
| `keystone.slow_requests` | counter | capability（阈值告警伴随 warn 日志） |

### L1：日志约定

- LoggerMessage 源生成（CA1848 强制，零装箱）；
- 结构化字段命名对齐 tag 表（taskId/instance/entryId/channel/durationMs）——一次查询
  TaskId 同时命中 span、日志、事实；
- actor 消息边界常规记录：请求进（Debug）/ 出（Information，含耗时）。

### L2：新增事实

- `ActorRestartedFact(taskId?, instance, reason)` / `ActorStoppedFact(instance)`——
  监督动作入审计流（05 §2"重启计数→告警"的数据基础）。

### L3：接线（KeystoneHostOptions.Observability）

```
Enabled            = true   （总开关；false = 不建任何 listener）
ConsoleEnabled     = true   （默认 console 导出——开发开箱即见）
OtlpEndpoint       = null   （配置后 OTLP 导出启用）
SampleRatio        = 1.0
SlowRequestThreshold = 5s   （超阈值 warn 日志 + slow_requests 计数）
```

## 关键设计约束

- **Runtime/Core 零第三方依赖**：探针只调 BCL API；OTel 包引用只在 Hosting——
  嵌入方不配置 Observability 时行为与旧版完全一致（无 listener → ActivitySource 近零开销）。
- **AOT**：OTel SDK（1.9+）官方 AOT 兼容；若发布冒烟出现 IL 警告，比照 ADR-0015
  （Proto.Actor 先例）例外处理——宿主自身代码零告警底线不变。
- **TraceContext 迁移**：公开 API 不变（StartTask/GetCurrentTaskId），内部改
  `ActivitySource.StartActivity`——一次性获得采样协商 + OTel 可见性。
- **慢请求阈值**：默认 5s 可配；这正是"无超时调用方永久挂起"形态的第一道运行时防线
  （结合 P68 已修的异常回填，覆盖"慢"与"死"两类）。

## 后果

- 正面：消息模型排错从"人工二分"变"按 TaskId 一查到底"；监督/热更决策可审计；
  指标面为熔断（05 §3）预留数据基础。
- 负面/接受：Hosting 引入 OTel 包依赖（用户裁定接受；Console 默认开在纯生产嵌入
  场景可关）；span 常规记录有量级很小的固定开销（无导出 + 无 listener 时近零）。
- 备选否决：① 纯自研 ActivityListener 落文件（无生态对接，导出格式全自建）；
  ② 默认无导出（调试需显式开启，违背"方便排错"初衷）；③ 最小指标面（监督/热更
  缺数据，后续必补）。

## 实施

P70 批次（TDD 红→绿、全量回归、AOT 冒烟、14 log/11 登记/AGENTS、独立提交）：
T1 TraceContext 迁移 + span 面 / T2 meter 面 / T3 日志约定 + actor 边界常规记录 /
T4 监督事实 + 慢请求告警 / T5 L3 接线 + 配置面 / T6 AOT 验证（OTel 包警告处置）。
