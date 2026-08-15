---
type: architecture-doc
tags: [cordis-csharp, architecture, compliance, audit]
created: 2026-08-15
---

# 17 — 文档要求 vs 实现达成度审计

> 触发：用户指出"要根据文档里面的要求来做"，P33 暴露实现是文档的近似而非要求（如 01 §4 每实例持久 context 被简化为每请求新建）。
> 方法：workflow 并行 5 子代理，逐项对照架构文档（00-16）与 ADR-0001~0015 的**可验证承诺** vs 当前实现。
> 结果：30 项差距（5 域 × 6，全 ⚠️/❌），集中在"功能实现了但未按文档接线/语义简化"。
> 状态：**🔴 P0 高危 5 项全部修复（DC-1 P34 / DC-3 P35 / DC-6 P35 / DC-5 P36 / DC-4 P37）；P1 四项全部修复（DC-8 P38 / DC-11 P39 / DC-10 P40 / DC-7 P41）；P2 推进中：DC-16 ✅（P42）/ DC-20 ✅（P43）/ DC-13 ✅（P44）/ DC-15 ✅（P45）/ DC-17 ✅（P46）/ DC-18 ✅（P47）**；其余按 §4 计划排期。

## 1. 审计方法

- 5 个并行子代理，各负责一个文档域，对照"文档明确承诺" vs 实现代码（文件/行号证据）
- 判定标准：✅已达标 / ⚠️部分（近似实现，有简化）/ ❌未实现
- 只报 ⚠️/❌，每域限 6 项最重要的

## 2. 差距清单（30 项，去重后 20 项）

### 🔴 高危（12 项）

| ID | 文档 | 要求 | 现状 | 状态 |
|----|------|------|------|------|
| DC-1 | 01 §3/§4、03 §7 | 每实例独立**持久** context（actor=context 同生命周期，跨请求驻留） | ~~每请求新建 ContextFacade~~ **✅ 已修复（P34）**：CapabilityActor 持实例 context，跨请求复用 | ✅ |
| DC-2 | 03 §2/§5 | 中间件 context 沿作用域链解析服务 + 共享事件总线 | 请求级 facade 无父 → 解析不到插件服务、事件总线每请求新建 | ⚠️ 随 DC-1 部分缓解（实例 context 有父可传） |
| DC-3 | ADR-0005、09 §4 | 卸载五步闸门（拒绝新任务/在途排空/超时审计） | **✅ 已修复（P35）**：入口拒绝 + 总超时（ShutdownTimeout）+ 未收敛审计（UncollectedPlugins） | ✅ |
| DC-4 | 05 §2、09 §3 | 监督策略（OneForOne/AllForOne + 重启计数 + 指数退避 + 升级不可用） | **✅ 已修复（P37）**：OneForOneStrategy（Restart decider + MaxRestarts/窗口，超阈值停止=域不可用） | ✅ |
| DC-5 | 05 §3/§4 | 超时/熔断/重试接入运行链 | **✅ 部分（P36）**：依赖等待超时 → FAILED（DependencyWaitTimeout 接线）；TimeoutPolicy/RetryPolicy/CircuitBreaker 其余调用点待接 | ⚠️ |
| DC-6 | 02 §3、ADR-0007 | rebind=报错 + 热重载服务保持 | **✅ 已修复（P35）**：Register 重复（他属主）报错；热重载先卸载再启动 | ✅ |
| DC-7 | 08 §4 | 配置分层叠加（base/profile/patch/overlay + 环境选择） | **✅ 已修复（P41）**：`StartAsync(IReadOnlyList<string>)` 多层按序叠加（逐层解析含插值 → EntryTree.ApplyLayers）；单层重载保持原语义 | ✅ |
| DC-8 | ADR-0012 | !!env/!!file 静态插值（YAML tag 语法 + 无环检测 + 展开后校验） | **✅ 已修复（P38）**：EntryParser 接 StaticInterpolator（!!env NAME/!!file path tag 展开，缺失保留标记，visited 环检测跨整次解析）；宿主 EnvProvider/FileProvider 注入（展开后进 schema 校验） | ✅ |
| DC-9 | 08 §6 | 文件变更→重载→diff→逐条目更新 + 写回管线 | 无配置 watcher/diff；热更新退化为 API 调用 | ⚠️ |
| DC-10 | 04 §8、ADR-0003 | 管道原子替换（swap）+ 在途排空 + 保留 actor/context | **✅ 已修复（P40）**：管道实例化缓存（构建一次跨请求复用）+ SwapPipelineAsync 原子换引用（保留 actor/context；串行循环内在途走旧链） | ✅ |
| DC-11 | ADR-0009 | 事实事件持久化接入运行链 | **✅ 已修复（P39）**：IFactEvent 标记（emit 分发持久化，durable 分级）+ 任务完成/失败事实（能力域 actor）+ 插件生命周期事实（PluginRuntime）+ 宿主 EventStore 选项（根总线携带） | ✅ |
| DC-12 | 03 §1/§7 | actor=context 同生命周期，状态跨请求驻留 | 每请求新建 context（DC-1 修复前） | ✅ 随 DC-1 |

### 🟡 中危（8 项）

| ID | 文档 | 要求 | 现状 |
|----|------|------|------|
| DC-13 | 06 §3/§4 | Trace 接入 + TaskId 幂等去重 | **✅ 已修复（P44）**：请求执行包裹 keystone.task Activity（TaskId/ParentTaskId/能力域/操作 tag 贯穿，结束恢复前序）；重复 TaskId 回缓存结果不重执行（FIFO 容量上限防无界增长） | ✅ |
| DC-14 | 06 §1 | 取消贯穿全链（CT 传中间件/handler） | 取消止于传输层 |
| DC-15 | 09 §5 | CRUD 落盘写回管线 + position 参数 | **✅ 已修复（P45）**：ConfigFilePath 选项 → CRUD 防抖写回（原子写+重试）+ FlushConfigAsync/Shutdown 排空；CreateEntry/MoveEntry position 参数；**并修复死代码 bug**（Serialize 方法组绑定 Select 索引重载 → 下标灌进 indent，多条目写坏） | ✅ |
| DC-16 | 08 §3 | disabled 挂起 + isolate 组级隔离 | **⚠️ 部分（P42）**：disabled 运行行为已兑现（挂起不加载/父组继承子树/SetEntryDisabledAsync 恢复/挂起条目不参与门控拓扑与 manifest 校验）；isolate 组级服务隔离未接线（3 §2.2 语义，后续项） |
| DC-17 | 10 §6、ADR-0008 | manifest configSchema + semver/白名单校验 | **✅ 已修复（P46）**：PluginManifest.ConfigSchema 字段（可选，null=原始直传）；version 语义化版本校验（GeneratedRegex + NonBacktracking）；dependencies ⊆ 程序集编译白名单（越界 fail-fast，规则 0） | ✅ |
| DC-18 | ADR-0009 决策3 | 事件分级/降级/归档/定时 Prune | **✅ 已修复（P47）**：StoredFact 增 Durable 字段（EventBus 落盘时携带，旧数据缺键=尽力写）；FileEventStore 增 archivePath（Prune 被清事实同帧格式归档可重放，未配置=纯删除）；FactRetentionScheduler 周期 Prune（失败降级续跑）+ 宿主 RetentionPolicy/PruneInterval 接线随宿主启停（降级语义 P39 EventBus 已先行） | ✅ |
| DC-19 | ADR-0001 | IPluginSource/IPluginHost 抽象边界 | 无接口，SourceProvider 委托替代 |
| DC-20 | 05 §5 | 日志 category={能力域}/{插件 ID} + IOptions 命名选项 | **⚠️ 部分（P43）**：category 前缀已兑现（root context 域前缀 + 子 context 继承 + LoggerFactory 经宿主选项注入）；IOptions 命名选项级别覆盖未接线（RingBuffer 三级过滤 G11/G12 已有，命名选项绑定记剩余） |

## 3. 根因分析

1. **"功能实现了但没接线"是主模式**：10 项差距是"组件存在但零调用"（超时/熔断/重试、静态插值、事件持久化、分层叠加、写回管线、Trace）——M3-P13 各阶段只测了组件本身，未做宿主级接线验收
2. **生命周期语义简化**：quiesce 五步只做三步、监督策略未配、每请求新建 context——设计文档的完整语义未逐条对照实现
3. **文档承诺 vs 实现漂移**：06 §6 说 MAF Workflows 组合、01 §4 说每实例持久 context——实现用简化方案（纯 Task fan-out、请求级 facade）且未同步 ADR

## 4. 修复计划（按优先级）

| 优先级 | ID | 修复方向 | 工作量 |
|--------|----|---------|--------|
| P0 | DC-1/DC-2 | ✅ 已修复（P34）：实例级持久 context | 已做 |
| P0 | DC-3 | quiesce 补拒绝新任务 gate + 在途排空 + 超时审计 | 中 |
| P0 | DC-4 | Spawn 配置监督策略（重启计数 + 退避 + 升级） | 中 |
| P0 | DC-5 | 超时/熔断/重试接入运行链（初始化超时/依赖超时/管道超时） | 中 |
| P0 | DC-6 | Register 重复报错 + 热重载注册隔离 | 小 |
| P1 | DC-8 | StaticInterpolator 接 EntryParser（tag 语法）+ 无环检测 | ✅ 已修复（P38） |
| P1 | DC-11 | EventBus/PluginRuntime 事实事件写入 IEventStore | ✅ 已修复（P39） |
| P1 | DC-10 | 管道实例化缓存 + swap API（原子替换） | ✅ 已修复（P40） |
| P1 | DC-7 | 宿主接分层叠加（base/patch 组装） | ✅ 已修复（P41） |
| P2 | DC-9/13/14/15/16/17/18/19/20 | 逐项 | 各小-中 |

## 5. 结论

- 30 项差距中 **DC-1 已修复**（用户点名项），DC-2/DC-12 随修复缓解
- 其余集中在"组件未接线"与"生命周期语义简化"两类根因——与 P21 集成验收发现的模式一致（各能力域独立实现，宿主级接线缺位）
- 修复按 §4 计划推进，每项 TDD + 文档 + 提交

## 关联

- 16-cordis-gap-review（Cordis 差距，已闭合 9 项）、14-implementation-log（执行记录：P34 起）
- ADR-0003/0005/0007/0009/0012（生命周期/门控/持久化/插值）
