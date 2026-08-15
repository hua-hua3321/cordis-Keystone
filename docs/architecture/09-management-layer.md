---
type: architecture-doc
tags: [cordis-csharp, architecture, management, lifecycle]
created: 2026-08-15
---

# 09 — 管理层设计

> 三层架构的第二层：CompositionRoot actor 的启动流程、监督接线、进程级优雅关闭。
> 本文补齐 01-overview §2"管理层"的专题设计（来源：补充排查"管理层无独立专题文档"）。

## 1. 职责与定位

管理层 = CompositionRoot actor（Proto.Actor，T1），是整个运行时的**编排者**，不参与业务执行：

```
CompositionRoot actor
  ├─ 启动：读配置 → 校验 → 构建依赖图 → spawn 能力域 actor
  ├─ 插件管线：编译（Roslyn）→ 加载（ALC）→ 注册（Keyed Services）→ 挂载
  ├─ 热更新：文件监听 → 配置/插件变更 → 原子替换/重载
  ├─ 监督：能力域 actor 崩溃 → 重启策略
  └─ 关闭：进程级优雅关闭（全局 quiesce）
```

管理层是**唯一**持有插件管线与配置状态机的实体；能力域 actor 只处理本域任务，不感知全局编排。

## 2. 启动流程（bootstrap）

```
进程启动
  → 1. 加载配置层（08-configuration-layer：分层叠加 → 条目模型）
  → 2. schema 校验（fail-fast：坏配置直接退出，绝不带病启动）
  → 3. manifest 校验（依赖图可达性 + 无环 + 白名单，ADR-0007）
  → 4. 构建服务依赖图（provides/inject 拓扑）
  → 5. spawn 能力域 actor（每域一个，含独立 scope 根）
  → 6. 插件加载序 = 拓扑序 + PENDING 等待（ADR-0007 决策 3）
  → 7. 全部 ACTIVE 或 FAILED（FAILED → 启动告警，按 05-reliability §3 策略）
  → 8. 就绪信号（宿主可开始接收任务）
```

启动失败语义：配置/schema 校验失败 = 进程退出（fail-fast）；插件加载失败 = 该插件 FAILED，其余继续（隔离，不整域回滚）。

## 3. 监督接线

能力域 actor 由管理层 spawn 为监督子（Proto.Actor supervision）：

| 策略 | 适用 | 说明 |
|------|------|------|
| OneForOne（默认） | 单域崩溃 | 只重启该 actor（保留 context 状态 or 重建，按配置） |
| AllForOne | 需要一致性 | 一损俱损（共享状态域） |

监督与插件生命周期的联动（ADR-0005 决策 3）：

```
能力域 actor 崩溃
  → 重启计数 +1
  → 重试策略：指数退避（05-reliability §3）
  → 连续失败 N 次 → 标记该域不可用 + 告警（升级：隔离插件 / 停用能力域）
```

插件级崩溃（死循环/卡死）不走 actor 重启，走插件粒度 restart()（超时检测 → quiesce 卸载 → 重载），见 ADR-0005。

## 4. 进程级优雅关闭（全局 quiesce）

ADR-0005 定义了**插件粒度**卸载闸门；进程级关闭是同一语义在**全局**的展开：

```
关闭信号（SIGTERM / 宿主调用 ShutdownAsync）
  → 1. 停止接收新任务（入口拒绝 + 记录）
  → 2. 各能力域 actor 排空在途任务（CancellationToken 传播 + 超时逃生）
  → 3. 逐域执行插件 quiesce（ADR-0005 五步闸门：拒绝新任务 → 排空 → 逆序并发 disposer → 摘除注册 → ALC.Unload）
  → 4. 停监督树（不再重启，防止关闭期"复活"）
  → 5. 释放根容器 / 事件持久化 flush
  → 6. 进程退出
```

- 关闭超时：总关闭超时（默认 Ns）→ 强制退出 + 记录未收敛插件（可观测性审计）
- 幂等：ShutdownAsync 可重复调用；已在关闭中 → 直接等待完成
- 与 ADR-0005 一致：**ALC.Unload 只在插件收敛后调用**，进程级不绕过该闸门

## 5. 管理面（hosting API）

管理层对外暴露的管理操作（宿主嵌入形态，10-plugin-sdk §1）：

| 操作 | 说明 |
|------|------|
| `StartAsync` | 启动流程（§2） |
| `ShutdownAsync` | 全局 quiesce（§4） |
| `CreateEntry(options, parent?, position?)` | 创建条目（含插入位置；id 冲突自动生成；对齐 Cordis EntryTree.create，F5） |
| `RemoveEntry(id)` | 停止并删除条目（对齐 EntryTree.remove） |
| `MoveEntry(id, parent?, position?)` | 跨组移动/排序，失败回滚（对齐 EntryTree.update 移动路径） |
| `ReloadPlugin(id)` / `UpdatePlugin(id, config)` | 插件粒度重载/配置更新（ADR-0005；变更分级见 08 §6.1） |
| `ResolveEntry(id)` | 嵌套 id 解析（`:` 分隔跨子树，对齐 EntryTree.resolve） |
| `DumpConfig` | 当前生效配置树（对齐 harness `--dump-config`） |
| 状态查询 | 插件状态机（PENDING/ACTIVE/FAILED...）+ 指标（05-reliability §5） |

条目 CRUD 落盘经 08 §6.3 写回管线（原子写 + 防抖 + 事务刷新）。**CRUD 返回前 await 子树收敛**（对齐 Cordis `EntryTree.await`：等待本树 import/lifecycle 任务与失败聚合后再返回——编程式挂载（H2）路径的依赖方靠此获得"已就绪"保证，即 Cordis `loader.await` 门控的对应物）。

**编程式挂载**（H2，实现期定 API 形态）：除配置驱动外，任何 context 可运行期挂载插件——执行机制 = 动态管道组合（04 §2 形状 B 内部组合）+ 生命周期托管（ADR-0005/0007），底层 actor 骨架不按插件粒度 spawn（12 §7.2）。

**管理面事件**（实现期事件清单，F9，对齐 Cordis loader/* 事件面）：

| 事件 | 模式 | 语义 |
|------|------|------|
| `EntryInit` | emit | 条目创建（对齐 loader/entry-init） |
| `EntryDisposing` | emit | 条目部分/完全卸载（对齐 loader/partial-dispose） |
| `PatchContext` | waterfall | 条目上下文补丁可拦截（对齐 loader/patch-context；isolate 变更在此接线，03 §2.2） |
| `ConfigUpdate` | emit | 配置写回前通知（对齐 loader/config-update） |
| `Exit` | serial | 进程重启请求钩子（对齐 exit 信号） |

## 6. 与相邻文档的关系

| 文档 | 关系 |
|------|------|
| 01-overview.md | 本层是三层架构的"管理层"专题 |
| 08-configuration-layer.md | 启动第 1-4 步消费配置层产物 |
| 02-plugin-model.md | 插件管线（编译/加载/回收）的执行者是本层 |
| 05-reliability.md | 监督策略、超时熔断、重试与本文 §3 联动 |
| ADR-0003 | 管道原子替换由本层发起（配置变更检测） |
| ADR-0005 | 插件粒度 quiesce 是本层全局关闭的构件 |
