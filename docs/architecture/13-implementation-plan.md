---
type: architecture-doc
tags: [cordis-csharp, architecture, implementation-plan, milestones]
created: 2026-08-15
---

# 13 — 分阶段实施计划

> 从底层核起步，每阶段一个可验收目标，里程碑推进，直至整套框架代码落地。
> 过程记录（做了什么/决策了什么）见 14-implementation-log.md——两文档配套：**13 管"下一步做什么"，14 管"已经做了什么"**。

## 1. 执行原则

1. **底层核先行**：依赖关系决定顺序——契约 → 上下文/事件 → 服务/生命周期 → 管道 → 加载 → 配置 → 管理 → 能力域 → 观测 → 持久化 → SDK → AI 组合。上层阶段只依赖已完成阶段的产物
2. **每阶段可验收**：阶段结束 = 验收条件全绿（`dotnet test`）+ 记录闭合（14）+ 决策沉淀（ADR）+ 规则 0 冒烟（`dotnet build -warnaserror`；AOT 发布冒烟可用则必须跑通）+ git commit
3. **里程碑推进**：M0→M13 单调前进，不回头补阶段（缺陷修复属于阶段内工作，不算新阶段）
4. **并行受控**：默认串行；标注"可并行"的阶段在资源允许时并行，**同一时刻至多两个并行阶段**（保证 14 记录与验收不混乱）
5. **实现期待定项在阶段内定稿**：H2/H3/M1/M3/M6 等设计期遗留项已分配阶段（§5），定稿即更新 12/11 状态

## 2. 阶段总览

| 里程碑 | 阶段 | 目标（一句话） | 主要设计输入 | 依赖 |
|--------|------|---------------|-------------|------|
| M0 | P0 工程骨架 | 解决方案/测试/CI/规则 0 验证通道就绪 | AGENTS.md、00 技术栈 | — |
| M1 | P1 核心契约 | 消息契约 + 框架异常码（M6） | 06-contracts、ADR-0004 | P0 |
| M2 | P2 上下文与事件 | Context 门面 + 五分发事件 + 门面拦截（H3）+ Effect（M1） | 03、04 §3、ADR-0006、10 §4 | P1 |
| M3 | P3 服务与生命周期 | 服务注册表 + 依赖门控 + 状态机 | 02 §3、03 §2、ADR-0005/0007 | P2 |
| M4 | P4 管道执行 | 中间件管道 + 动态组合（H2 机制）+ 双轨 | 04、ADR-0006 | P3 |
| M5 | P5 插件加载 | Roslyn 编译 + Collectible ALC + 分组回收 + quiesce | 02、ADR-0005 | P3 |
| M6 | P6 配置层 | 条目模型 + 分层叠加 + 静态插值 + 校验 + 事务/写回 | 08、ADR-0011/0012、F 系列 | P3 |
| M7 | P7 管理层 | 启动/监督/quiesce + hosting API（CRUD/H2 完成） | 09、F5/F9 | P4+P5+P6 |
| M8 | P8 能力域 | Proto.Actor 域 + 多实例隔离（F10）+ 跨域 TaskId | 01、03 §2.2 | P7 |
| M9 | P9 观测与可靠性 | 日志/Activity（H1 落地）/指标/超时熔断 | 05、12 §7.1 | P4（可并行） |
| M10 | P10 事件持久化 | IEventStore + append-only + 重放 | ADR-0009 | P3+P6 |
| M11 | P11 插件 SDK | IPlugin/IMiddleware/manifest/schema/模板/Effect 定稿 | 10、12 §8 | P7 |
| M12 | P12 AI 组合 | MAF/MCP 单向依赖 + O2 不稀释验证 | ADR-0008 | P8 |
| M13 | P13 验收闭环 | 全量回归 + 12 映射核查 + 1.0 发布 | 11、12 | 全部 |

> 顺序依据：配置层（P6）需要服务注册表（P3）做条目 inject/门控接线；管理层（P7）需要管道（P4）、加载（P5）、配置（P6）三者齐备；SDK（P11）在管理层之后定型公开面。
> 可并行：**P9（观测）与 P5-P8**（横切关注点，独立接线）、**P5（加载）与 P4**（独立技术栈，仅依赖 P3）、**P10 与 P9**。

## 3. 阶段详表

### P0 工程骨架（M0）

- **目标**：解决方案与治理通道就绪，后续阶段有统一的构建/测试/验证底座
- **交付物**：`cordis-csharp.slnx`；工程划分（Keystone.Core / Keystone.Config / Keystone.Runtime / Keystone.Management / Keystone.Sdk / Keystone.Hosting / tests）；`Directory.Build.props`（规则 0 编译约束：warnaserror、可空、禁止反射告警）；CI（build + test + frontmatter 校验）；测试工程骨架（xunit）
- **验收条件**：
  1. `dotnet build cordis-csharp.slnx -warnaserror` 通过
  2. 规则 0 冒烟命令跑通（AOT publish 可用则通过，不可用则记录例外）
  3. 空测试工程可运行（`dotnet test` 绿）
  4. CI 管道接入文档校验（validate_frontmatter.py）

### P1 核心契约（M1）

- **目标**：消息契约与错误码落地，后续所有阶段的数据形状在此定型
- **交付物**：`TaskId`（生成/解析/层级/比较）、`TaskRequest`（含 `Guid? ParentTaskId`）、`Payload`（MessagePack 源生成契约）、`TaskResult` + `ErrorCode` 码表（M6 定稿，含框架级码：生命周期/门控/配置/管道）；`CordisException` 层次
- **验收条件**：
  1. 单测：TaskId 生成唯一性/解析/父子层级；Payload 序列化往返（MessagePack 源生成，无反射）
  2. 错误码表完整且与 12 §8 M6 一致（实现期码表定稿并回写 12）
  3. 契约 DTO 全部 `[MessagePackObject]`（规则 0 第 3 条核查通过）

### P2 上下文与事件（M2）

- **目标**：Context 门面与事件总线落地，服务注册表的前置
- **交付物**：`IPluginContext` 门面（内置事件/日志/注册表访问；属主校验 G8；**门面拦截器形状定稿（H3）**）；`EventsService`（emit/parallel/serial/bail/waterfall，ADR-0006；过滤 G15；prepend/once M7）；**`IPluginContext.Effect(Func<Task>, [CallerInfo] CallerInfo?)`（M1 定稿）**；日志门面 `GetLogger(name)`（M2 形态）+ 基础 ILogger 接线；`ctx.Root`/`BaseUrl`（L6）
- **验收条件**：
  1. 事件测试：五分发模式语义（parallel 聚合、serial 顺序、bail 短路、waterfall 否决/放行）；prepend 顺序；once 只触发一次
  2. 属主校验：非属主 set 抛错（G8）
  3. Effect 测试：disposer 执行、嵌套 EffectMeta 树、CallerInfo 记录调用者
  4. 门面拦截器形状验收：AOT 安全（无 Castle/DispatchProxy，规则 0 核查）

### P3 服务与生命周期（M3）

- **目标**：服务注册表与依赖门控，插件运行时状态机
- **交付物**：`ServiceRegistry`（键控服务 T4：provide/set/get、isolate realm 键）；manifest 解析（`inject`/`provides`，02 §1）；**依赖门控引擎**（PENDING→ACTIVE 激活/失效重载，ADR-0007）；**`PluginRuntime` 状态机**（PENDING/LOADING/ACTIVE/FAILED/UNLOADING/DISPOSED，ADR-0005）+ effect 收敛（M1 落地）
- **验收条件**：
  1. 门控测试：依赖缺失 → PENDING；依赖出现 → 自动 ACTIVE；依赖消失 → 依赖方重载
  2. 状态机全转移测试（含 FAILED 与重试）
  3. 服务可用性事件（internal/service 对应物）驱动依赖方（ADR-0007）
  4. manifest 校验：非法 inject 引用 fail-fast

### P4 管道执行（M4）

- **目标**：中间件管道与动态组合，插件执行模型核心
- **交付物**：`IMiddleware`（形状 A 公开面）+ **宿主内部组合（形状 B：`List<Func<ctx,next,Task>>` 反向包装，04 §2/H2 机制）**；管道运行时（顺序/短路/否决回滚）；双轨（管道插件/决策插件/观察者插件，04 §3）；waterfall 事件与管道共用语义
- **验收条件**：
  1. 管道测试：注册序执行、短路（不调 next）、否决（waterfall 抛错回滚）
  2. 动态组合测试：运行期插入节点 → 组合 → 执行（H2 机制验收）
  3. 双轨分类测试：三类插件的路由正确

### P5 插件加载（M5）

- **目标**：Roslyn 编译 + 独立 ALC + 分组回收 + 热重载
- **交付物**：`RoslynCompiler`（内存编译，ADR-0002 例外区）；`CollectibleAlcManager`（每插件 ID 一组 ALC）；**quiesce 5 步卸载闸门**（ADR-0005：拒绝新任务→收敛→dispose→回收→验证）；插件源变更热重载（02 §7，diff 替换）
- **验收条件**：
  1. 编译-加载-运行-卸载-重载循环测试通过
  2. 卸载后 ALC 可回收（无泄漏：`GC.Collect` 后程序集可卸载，测试断言）
  3. quiesce 收敛：运行中任务排空后才 dispose；超时策略生效
  4. 规则 0 核查：宿主路径无反射依赖（Roslyn/ALC 限加载层）

### P6 配置层（M6）

- **目标**：配置全链路：形态 → 分层 → 插值 → 校验 → 应用（分级/事务/写回）
- **交付物**：YamlDotNet 接线 + 条目模型（id/name/config/disabled/inject/group/isolate，含继承 F7、内建前缀 F8）；分层叠加（profile/patch/overlay，08 §4）；**静态插值（`!!env`/`!!file`/anchors + 引用环检测，ADR-0012）**；schema 校验（源生成器声明式，08 §5）；**配置解析管线（M3 定稿：raw → 过滤器链 → 校验 → 注入）**；**diff 分级重启（F3）**；**组级事务（F4，含卸载主导终止）**；**写回管线（F6：File.Move 原子替换 + HRESULT 重试 + 防抖 + 队列 + 事务刷新 + initial 引导）**；include 文件（F6）
- **验收条件**：
  1. 分层合并测试：base→profile→patch→overlay 叠加序正确；重复 id fail-fast
  2. 插值测试：`!!env`/`!!file` 展开；引用环检测抛错
  3. 校验测试：坏配置精确报错 + 默认值补齐；校验失败不重启（ADR-0003）
  4. diff 分级测试：仅 config 变热更新、name/inject/group 变冷重启、disabled 仅卸载；失败回滚
  5. 组级事务测试：多条目并行应用、失败逆序回滚、树卸载不回滚
  6. 写回测试：原子写（File.Move）、占用重试、防抖合并、事务刷新保旧树、initial 引导
  7. 条目级 inject 与 manifest 并集合并（F2，实现期验证冲突优先级）

### P7 管理层（M7）

- **目标**：启动/监督/关闭全流程 + hosting API 完整面
- **交付物**：8 步启动流程（09 §2）；监督接线（09 §3 + Proto.Actor）；进程级 quiesce（09 §4）；**hosting API 全量**：StartAsync/ShutdownAsync/CreateEntry/RemoveEntry/MoveEntry（含回滚）/ResolveEntry（`:` 嵌套 id）/ReloadPlugin/UpdatePlugin/DumpConfig/状态查询（F5）；管理面事件 5 个（EntryInit/EntryDisposing/PatchContext waterfall/ConfigUpdate/Exit，F9）；**编程式挂载 API 定稿（H2：动态管道组合 + 生命周期托管）**；CRUD 返回前 await 子树收敛（F11 对应物）
- **验收条件**：
  1. 启动-运行-关闭全流程集成测试（含错误注入）
  2. CRUD 测试：创建/删除/跨组移动（失败回滚）/嵌套 id 解析/持久化写回
  3. H2 端到端：程序化挂载插件 → 依赖门控 → 运行 → 卸载
  4. 管理面事件测试：5 事件触发与模式正确（PatchContext waterfall 可否决）

### P8 能力域（M8）

- **目标**：Proto.Actor 域与多实例隔离，跨域编排
- **交付物**：能力域 actor（01：串行循环/监督，T1）；多实例模型（01 §4：实例 A/B 独立 scope）；服务级 isolate 运行时（03 §2.2，**F10 转移优化评估实现**）；跨域 TaskId 编排（06 §1）
- **验收条件**：
  1. 串行语义测试：actor 内消息串行执行
  2. 隔离测试：fs-A/fs-B 互不可见；isolate 变更 → 依赖方重载
  3. 跨域测试：TaskId/ParentTaskId 跨域传递一致（O2 前置验证）
  4. F10：转移优化落地或明确"不实现"理由（记录 14 偏差）

### P9 观测与可靠性（M9，可与 P5-P8 并行）

- **目标**：可观测性与可靠性横切能力
- **交付物**：日志（类别命名 G11/级别覆盖 G12/ILoggerProvider 接线/**环形缓冲 L1**/Error 展开 L4）；**Activity 链路（H1 落地：Activity.Current 贯穿 + CallerInfo 日志注入）**；指标（05 §5）；错误处理/超时/熔断/重试（05 §2-§4）；监督策略联动（09 §3）
- **验收条件**：
  1. 链路测试：Activity 跨插件/跨域贯穿，服务内读 Activity.Current 得调用方上下文（H1 验收）
  2. 日志结构化测试：类别/级别覆盖生效；环形缓冲诊断可读
  3. 熔断测试：连续失败触发熔断 → 恢复窗口 → 半开
  4. 规则 0 核查：日志/指标无反射路径

### P10 事件持久化（M10，可与 P9 并行）

- **目标**：事实事件 append-only 持久化与重放
- **交付物**：`IEventStore` 抽象 + 内存/文件实现；append-only 日志（ADR-0009）；重放/保留策略；**事件格式迁移策略（StoredFact.SchemaVersion，ADR-0009 风险表）**
- **验收条件**：
  1. append-only 测试：只追加不改写；崩溃恢复顺序一致
  2. 重放测试：重放产生一致状态
  3. SchemaVersion 迁移测试（新旧格式共存/升级路径）

### P11 插件 SDK（M11）

- **目标**：插件开发者公开面与工程化模板
- **交付物**：`IPlugin.InitializeAsync(ctx, config)`/`IMiddleware` 公开面；`IPluginContext` 完整面（Get/Provide/Subscribe*/Effect[CallerInfo]/GetLogger/Timers，10 §4）；manifest schema（含 `skills: ["skill://..."]` SEP-2640）；**G16 防回归（10 §8 "已接受丢弃"引用表）**；`dotnet new` 模板；示例插件（**注：示例插件库未建——预留登记于 11 §4；验收条件 1 由模板测试覆盖，SDK 用法参考以 tests 内联插件源码为准，10 §7 已同步**）
- **验收条件**：
  1. 模板测试：`dotnet new` 创建示例插件 → 编译 → 挂载 → 运行 → 卸载全链路
  2. SDK 面与 10-plugin-sdk 文档逐条一致（含 Effect API 签名）
  3. manifest 校验：skills 引用、inject 引用合法
  4. G16：5 项已接受丢弃在 SDK 文档显式引用（防回归）

### P12 AI 组合（M12）

- **目标**：MAF/MCP 组合，单向依赖验证
- **交付物**：MAF 适配（Microsoft.Agents.AI.*，T10）；技能包接线（SEP-2640）；MCP 双端；**O2 验证：TaskId/ParentTaskId 语义映射进 MAF Workflows 不稀释**（ADR-0008 不回退项）
- **验收条件**：
  1. 架构测试：核心程序集无 MAF 依赖（单向依赖强制验证，ADR-0008）
  2. O2 验证：Workflows 编排中 TaskId 层级完整传递
  3. 技能包端到端：manifest skills → 加载 → 调用

### P13 验收闭环（M13）

- **目标**：整套框架验收与 1.0 发布
- **交付物**：全量回归；12 语义映射核查（G6/G9/H/M/L/F 逐项对照实现）；11 实现期项全部闭合（状态更新）；性能冒烟（加载/卸载/事件吞吐）；发布文档
- **验收条件**：
  1. 全量测试绿（含所有阶段验收用例）
  2. 12/11 实现期项 0 残留（H2/H3/M1/M3 API 形态定稿回写）
  3. 性能冒烟达标（吞吐/内存回收基线记录）
  4. 文档与实际实现一致性核查通过（14 回溯索引可全程追溯）

## 4. 里程碑定义（DoD）

每个里程碑 = 该阶段验收条件**全部 ✅** + 以下四件事闭合：

1. **测试绿**：阶段验收用例在 `dotnet test` 中全绿（验收条件 ↔ 测试用例映射记录在 14 §6 验收台账）
2. **记录闭合**：14-implementation-log 阶段状态更新（日期/结论/工作日志行齐全）
3. **决策沉淀**：阶段内新决策 → ADR（实现期通道，14 §4）；偏差 → 14 §5
4. **规则 0 冒烟**：`dotnet build -warnaserror` 绿；AOT publish 冒烟可用则跑通

## 5. 实现期待定项 → 阶段分配表

| 待定项 | 内容 | 定稿阶段 | 定稿动作 |
|--------|------|---------|---------|
| H2 | 编程式挂载 API 形态 | P4（机制）+ P7（API） | 04 §2 组合已定；P7 hosting API 签名定稿，回写 12 §7.2/09 §5 |
| H3 | 门面拦截器形状 | P2 | 拦截中间件形状定稿，回写 12 §7.3/03 |
| M1 | Effect API + CallerInfo | P2 | `IPluginContext.Effect` 签名定稿，回写 12 §8/10 §4 |
| M3 | 配置解析管线 | P6 | 过滤器链形状定稿，回写 12 §8/08 §5 |
| M6 | 框架异常码表 | P1 | 码表定稿，回写 12 §8/06 §1 |
| M2 | Callable 服务形态（GetLogger） | P2 | API 形态定稿，回写 12 §8/10 §4 |
| M7 | 监听器 prepend/once | P2 | 订阅原语定稿 |
| F10 | isolate 转移优化 | P8 | 落地或明确不实现（14 偏差记录） |
| O2 | TaskId↔MAF Workflows | P12 | 不稀释验证（ADR-0008 不回退项） |
| 事件格式迁移 | StoredFact.SchemaVersion | P10 | 迁移策略落地（ADR-0009 风险表） |
| G16 防回归 | SDK 已接受丢弃引用 | P11 | 10 §8 引用表 |
| L 系列 | L1/L4/L6 等 | P2/P9 | 随手处理（12 §9） |
| 上游包 | 未随 vendor 分发的官方包 | 引入时 | 按 11 §5 纪律补登记（group/hmr/timer/logger-console 等） |

## 6. 执行纪律

- **阶段进入条件**：前置里程碑 DoD 闭合（P5 可 P4 并行，P9/P10 可提前）
- **阶段退出条件**：§4 DoD 四件事全部闭合 → 更新 14 阶段状态 → 进入下一阶段
- **变更纪律**：实现期发现设计缺陷 → 14 §5 偏差记录 + 按需 ADR；**不静默偏离 00-12 文档**
- **规则 0 全程**：每阶段验收含 AOT 兼容核查（P5 加载层除外，ADR-0002 例外）
- **TDD 先行（用户补充）**：每个功能单元**测试先行**——先写失败测试（红）→ 最小实现（绿）→ 重构（保持绿）；验收用例即测试用例（14 §6 台账直接引用测试名）。**禁止"先写实现后补测试"**（边界不清的根因）
- **设计模式与抽象隔离（用户补充）**：实现遵循既有设计模式（Value Object/DTO/静态工厂/门面/策略等）；**契约（Contracts）/错误（Errors）/实现边界显式隔离**——接口面向行为不面向实现；跨层依赖单向（Core ← Config ← ... ← Hosting）；引入新抽象必须能在 00-12 文档找到对应或走 ADR/偏差记录，防止"实现期自己发明边界"
- **看板联动**：本计划对应看板流水线的实现期泳道；AGENTS.md 状态"设计期"→"实现期"在本计划批准并 M0 落地时切换
