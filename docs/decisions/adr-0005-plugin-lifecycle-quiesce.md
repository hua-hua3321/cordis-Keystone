---
type: adr
tags: [cordis-csharp, decisions, lifecycle, quiesce, hot-reload]
created: 2026-08-15
status: accepted
---

# ADR-0005：插件生命周期状态机 + quiesce 收敛协议

> 决策状态：**accepted**（2026-08-15）
> 关联待定项：`docs/architecture/02-plugin-model.md` §6-§7、`docs/architecture/05-reliability.md` §1/§6
> 来源：`docs/architecture/07-cordis-migration-gap.md` 差距 G1/G2/G3

## 背景（Context）

迁移差距分析（`docs/architecture/07-cordis-migration-gap.md` §2.1）对照 vendored Cordis 源码（`fiber.ts`）发现三项插件生命周期差距：

1. **G1（P0）无生命周期状态机**：Cordis 有完整状态机 `FiberState = PENDING → LOADING → ACTIVE → FAILED → UNLOADING → DISPOSED`（fiber.ts:147-154），状态迁移发 `internal/status` 事件（fiber.ts:586）；当前设计（02-plugin-model.md §6）只有 `IPlugin : IAsyncDisposable` disposer 原语，"插件当前处于什么状态、启动失败后怎么办"无定义。
2. **G2（P0）无 quiesce 收敛协议**：Cordis `dispose()` 循环 `while (this.inertia) await this.inertia` 等待在途 load/unload 全部 settle（fiber.ts:293-295），卸载体将全部 effect disposer **逆序并发**执行并全部 await（fiber.ts:675-696）；当前设计（02-plugin-model.md §7）热重载流程是"dispose 旧 → 挂新 → 旧 ALC.Unload()"，**没有等旧插件完全收敛再 Unload 的闸门**——文档自己点名卸载残留是 HMR 头号失败原因，但解法只写了"disposer 协议强制"。
3. **G3（P1）无插件粒度 restart()/update() 与 FAILED 态处理**：Cordis 有 `restart()` = dispose+reload（fiber.ts:718-723）、`update()` = 校验新配置 → waterfall 可否决 → restart（fiber.ts:736-753）、`await()` 稳定等待 + 错误重抛（fiber.ts:704-710）；当前设计只有管道粒度热更新（04-pipeline.md §8），配置热更新到插件粒度没有对应语义。

热重载是 C# 版相对 JS 版的核心增值（02-plugin-model.md §7 自述）；这三项差距直接决定热重载是否可靠、插件启动失败是否有定义行为，必须在实现前收敛为正式决策。

## 决策（Decision）

### 决策 1：采纳完整生命周期状态机

插件宿主为每个插件实例维护 `PluginLifecycleState` 状态机：

```
PENDING → LOADING → ACTIVE → FAILED → UNLOADING → DISPOSED
                    ↑         │          │
                    └─────────┴──────────┘   （FAILED 可经 restart 回 LOADING；
                                              UNLOADING 为终态前必经）
```

- 状态迁移发布 `internal/status` 事件（对齐 Cordis fiber.ts:586），供监督与可观测性消费。
- `IPlugin : IAsyncDisposable` 保留为 disposer 原语契约，状态机叠加在其上，不改变插件 SDK 接口面。
- 启动失败进入 FAILED 态并持有 `_error`，`await()` 等价物（稳定等待）重抛启动错误（对齐 fiber.ts:704-710）。

### 决策 2：采纳 quiesce 收敛协议（卸载五步闸门）

插件卸载必须按以下顺序收敛，**ALC.Unload() 只允许在第 ⑤ 步之后调用**：

1. 拒绝新任务（状态置 UNLOADING，新请求立即失败或排队到新实例，按能力域语义定）；
2. 等待在途任务完成（CancellationToken 传播，联动 `05-reliability.md` §1 超时策略）；
3. 逆序并发执行全部 disposer 并 await 全部 settle（对齐 Cordis DisposableList.clear() 逆序语义，utils.ts:27-31）；
4. 全部 disposer 收敛后，摘除注册、清 static、释放子容器；
5. 最后 ALC.Unload() + 回收验证（`05-reliability.md` §6 已有测试门，把收敛断言写进热重载测试）。

### 决策 3：插件粒度 restart()/update() 与 FAILED 态处理

- `restart()` = 走完整卸载闸门（决策 2）+ 重新 LOADING/ACTIVE；用于插件崩溃恢复（联动 `05-reliability.md` §3 重试策略）与显式运维操作。
- `update(config)` = 校验新配置 → 配置更新事件（waterfall 可否决）→ 通过后 restart；把配置热更新从"管道粒度"（04-pipeline.md §8）扩展到"插件粒度"。
- FAILED 态处理：重试策略（`05-reliability.md` §3）与状态机联动——重试即 restart，连续失败按监督策略升级（隔离该插件 / 停用该能力域）。

## 理由（Rationale）

1. **热重载正确性依赖收敛闸门**：ALC.Unload() 是尽力而为，只要有残留引用（事件监听器、static、delegate 捕获）卸载静默失败。仅靠"disposer 协议强制"没有执行保证；显式状态机 + quiesce 闸门把"卸载完成"变成可断言、可测试的契约。
2. **与 Cordis 语义等价**：Cordis 的 dispose 是异步收敛协议（逆序并发 + 全 await + quiesce 等待），不是同步摘除；C# 版若只用 `IAsyncDisposable` 原语，语义不等价。
3. **与 ADR-0003 互补不冲突**：ADR-0003 决策 2 已为**管道**定义"在途请求排空后销毁"；本 ADR 把同一可靠性语义补到**插件**粒度，两处保持一致。ADR-0003 的 context 并发模型（actor 串行默认）天然提供"在途任务"边界，quiesce 可精确实现。
4. **实现成本可控**：状态机 + 卸载闸门是纯宿主侧机制，不改变插件 SDK、不改变 ALC 加载管线、不引入新技术项（00-tech-stack.md T1-T9 范围内）。
5. **测试门已有基础**：`05-reliability.md` §6 已要求热重载回收测试，本决策把"quiesce 收敛"写进断言即可，无需新建测试体系。

## 权衡 / 风险（Trade-offs / Risks）

| 风险 | 说明 | 缓解 |
|------|------|------|
| 状态机增加宿主复杂度 | 五个状态 + 迁移事件是新增机制 | 状态机是纯枚举 + 迁移守卫，不引入框架依赖；迁移图写入 02-plugin-model.md §6 |
| quiesce 等待可能挂起 | 在途任务永不结束（死循环/外部阻塞） | 超时逃生：超过 `05-reliability.md` §1 超时阈值后强制 dispose + 记录，不无限等待 |
| 逆序并发 disposer 的时序契约 | 插件依赖的注册/反注册顺序可能隐含耦合 | 文档明确"逆序 = 后注册先摘除"（对齐 Cordis DisposableList），与 DI 容器 dispose 顺序一致 |
| FAILED→restart 循环 | 重试策略可能无限重启 | 连续失败计数 → 升级为隔离/停用（联动 05-reliability §3） |

## 备选方案（Alternatives）

| 方案 | 描述 | 结论 |
|------|------|------|
| A（采纳） | 完整状态机 + quiesce 五步闸门 + 插件粒度 restart/update | 采纳：语义等价 Cordis，热重载可断言 |
| B | 保持 disposer 原语现状，靠超时强制 dispose（05-reliability §1 现状） | 不采纳：无收敛保证，ALC 卸载残留概率高，热重载不可靠 |
| C | 不做插件粒度 restart/update，仅管道粒度热更新 | 不采纳：配置热更新承诺在插件侧失效，插件崩溃恢复只能整域重启，粒度过粗 |
| D | 进程隔离替代（重启进程达到收敛） | 不采纳：ADR-0001 已否决进程隔离（成本 5-10 倍，破坏域内直接调用原则），收敛问题在进程内可解 |

## 影响（Consequences）

- **架构文档**：`02-plugin-model.md` §6 增补 `PluginLifecycleState` 状态机 + 迁移图；§7 热重载流程增补 quiesce 五步闸门。`05-reliability.md` §1 插件崩溃隔离补 quiesce 语义（超时 → 拒绝新任务 → 排空在途 → 收敛 dispose → Unload）；§6 热重载测试补收敛断言。
- **接口面**：宿主新增生命周期状态机（宿主内部机制，不进插件 SDK）；`IPlugin : IAsyncDisposable` 契约不变。
- **实现任务分解**：G1/G2/G3 各自成为独立实现子任务（I→V→R 验证链），热重载回收测试需扩展断言 quiesce 收敛。
- **不回退项**：热重载流程不得回到"无闸门直接 Unload"；插件粒度 update() 不得绕过配置更新 waterfall 否决。

## 关联

- `docs/architecture/02-plugin-model.md` §6（disposer 协议）、§7（热重载流程）
- `docs/architecture/04-pipeline.md` §8（管道粒度热更新，本 ADR 补插件粒度）
- `docs/architecture/05-reliability.md` §1（超时/崩溃隔离）、§3（重试策略）、§6（热重载测试门）
- ADR-0003（context 并发模型 + 管道原子替换，本 ADR 扩展其"在途排空"语义到插件粒度）
- ADR-0001（同进程可信代码，收敛在进程内完成，不引入进程边界）
- 其余差距的候选决策见 `docs/architecture/07-cordis-migration-gap.md` §5（ADR-0006 事件分发模式全集、ADR-0007 依赖门控，另行决策）
