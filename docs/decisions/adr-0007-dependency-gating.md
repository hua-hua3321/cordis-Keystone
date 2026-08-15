---
type: adr
tags: [cordis-csharp, decisions, dependency-gating, manifest, inject, keyed-services]
created: 2026-08-15
status: accepted
---

# ADR-0007：依赖门控激活 + manifest 服务级依赖（inject）

> 决策状态：**accepted**（2026-08-15）
> 关联待定项：`docs/architecture/02-plugin-model.md` §1/§3/§5
> 来源：`docs/architecture/07-cordis-migration-gap.md` 差距 G4/G5/G13（P0）

## 背景（Context）

`07-cordis-migration-gap.md` §2.2/§2.6 发现三项同源 P0 差距：

1. **G13：manifest 无服务级依赖声明**。当前 manifest 的 `dependencies`（cordis-runtime/cordis-contracts）是 **Roslyn 编译引用白名单**——解决"插件代码能引用哪些程序集"；Cordis 的 `inject` 是**服务级运行时依赖**——解决"插件要等哪个服务提供方就绪"。两者正交，当前 manifest 只覆盖了前者。
2. **G5：依赖门控激活无 C# 设计**。Cordis 插件在 `inject` 声明的服务全部可用前保持 PENDING（fiber.ts:597-623）；服务提供方卸载/替换时依赖方自动 reload/unload（reflect.ts:314-336）；`GetRequiredKeyedService<T>` 缺服务直接抛异常，没有"等待可用"语义。
3. **G4：key 语义未锁死**（`02-plugin-model.md` §3 内部矛盾：注册段写"key = 插件 ID 或能力域实例 ID"，解析段写"key = 插件内服务名"）。Cordis 的 key 是**服务名**（稳定的语义标识，消费者声明 `inject: ['fs']` 即可，不感知提供者身份）；若用插件 ID 做 key，依赖关系从"服务契约"退化成"实现耦合"。

实证：harness postmortem 0001——inject 丢失 → 插件在 fiber 树里找不到服务 → 运行时崩溃。inject 是加载正确性的硬依赖；"等依赖就绪再启动"是 Cordis 框架之所以是框架的机制（cordis-primer 核心概念第 3 条，harness 全仓依赖）。

## 决策（Decision）

### 决策 1：key 语义 = 服务名（类型 + 名称二元组）

- 注册/解析 key 一律用**服务名**（语义标识）：`AddKeyedScoped<IFsProvider, LocalFsProvider>("fs")`、`ctx.Get<IFsProvider>("fs")`
- **插件 ID 只用于子容器分组与回收**，不参与服务解析 key
- 修正 `02-plugin-model.md` §3 的矛盾表述（§3 代码注释同步改）

### 决策 2：manifest 增补服务级依赖字段 `inject`

```json
{
  "id": "plugin-fs-local",
  "version": "1.0.0",
  "main": "FsLocalPlugin.cs",
  "dependencies": ["cordis-runtime", "cordis-contracts"],
  "provides": ["fs"],
  "inject": ["llm", "telemetry"]
}
```

- `provides`/`inject` 里的名字是**服务名**（类型在宿主接口白名单声明，`provides: ["fs"]` 表示实现 `IFsProvider` 并注册为服务名 `"fs"`）
- `inject`（服务级运行时依赖）与 `dependencies`（程序集编译白名单）**维度不同、两者并存**，文档显式声明
- `provides` 与 `inject` 配对成服务依赖图；manifest 校验器校验：inject 声明的服务在依赖图内可达、依赖图无环、provides 类型在接口白名单内（启动期 fail-fast）

### 决策 3：依赖门控激活纳入第一版（P0）

- 插件生命周期状态机（ADR-0005）的 PENDING 态承载等待：插件进入 PENDING 直到全部 `inject` 服务可用（对齐 fiber.ts:597-623）
- 宿主维护**服务可用性事件**（服务提供方注册/卸载时发布），PENDING 插件收到事件后重新检查依赖
- 服务提供方卸载/替换 → 依赖方自动 reload/unload（对齐 reflect.ts:314-336，与 ADR-0005 状态机迁移联动）
- 加载序 = 服务依赖图拓扑序 + PENDING 等待，而非手动编排启动序列

## 理由（Rationale）

1. **"等依赖就绪再启动"是 Cordis 框架的核心机制**：harness 全仓依赖（cordis-primer 核心概念第 3 条、postmortem 0001 实证）；放弃它，插件启动顺序只能靠配置序，等于放弃核心卖点（07 §2.6 P0）。
2. **key 用服务名才是服务契约**：消费者声明 `inject: ['fs']` 不感知提供者身份；插件 ID 做 key 会把插件替换（热重载、换提供方）变成破坏性变更。
3. **与 ADR-0005 零冲突**：ADR-0005 状态机已有 PENDING 态与 FAILED 处理，依赖门控只是给 PENDING 一个明确的进入/退出条件，纯宿主侧机制，不改变插件 SDK 接口面。
4. **实现成本可控**：服务可用性事件 + PENDING 检查 + manifest 校验器，均不引入新技术项（00-tech-stack.md T1-T9 范围内）。

## 权衡 / 风险（Trade-offs / Risks）

| 风险 | 说明 | 缓解 |
|------|------|------|
| 循环依赖 | inject 图成环 → 全部插件 PENDING 死锁 | manifest 校验器拒绝循环依赖（启动期 fail-fast） |
| 服务名冲突 | 两个插件 provide 同名服务 | 同 scope rebind = 报错（对齐 Cordis，见 03-context §2 rebind 决策） |
| 依赖永不就绪 | 等待无限挂起 | 启动超时（联动 05-reliability §3）→ FAILED + 告警 |
| 变更风暴 | 服务频繁替换 → 依赖方反复 reload | 同批次服务变更合并处理一次 + 冷却窗口 |

## 备选方案（Alternatives）

| 方案 | 描述 | 结论 |
|------|------|------|
| A（采纳） | 服务名 key + manifest inject + 依赖门控激活 | **采纳**：语义等价 Cordis，核心卖点保留 |
| B | 不纳入依赖门控，加载序 = 配置序 | 不采纳：放弃 Cordis 核心机制，插件替换需手动编排，热重载正确性打折 |
| C | key 用插件 ID | 不采纳：服务契约退化为实现耦合（G4 理由） |

## 影响（Consequences）

- `docs/architecture/02-plugin-model.md`：§1 manifest 增 `inject` 字段并区分两个依赖维度；§3 key 语义修正 + 依赖门控设计；§5 六条工作清单补第 7 条（服务级依赖图）
- 新增 manifest 校验器（依赖图可达性 + 循环检测 + 编译白名单检查），属启动期工具
- 与 ADR-0005 状态机集成：PENDING 进入条件 = inject 依赖未全就绪；依赖变更 → `_checkImpl` 等价物驱动 reload/unload
- `docs/decisions/README.md` 索引增补 ADR-0007

## 关联

- `docs/architecture/07-cordis-migration-gap.md` §2.2 / §2.6 / G4 / G5 / G13（来源）
- `docs/architecture/02-plugin-model.md` §1/§3/§5（落点）
- ADR-0005（生命周期状态机，PENDING 态承载等待）
- ADR-0001（插件来源/安全：manifest 校验器属于宿主侧缓解措施）
