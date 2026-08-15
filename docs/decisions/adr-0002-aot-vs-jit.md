---
type: adr
tags: [cordis-csharp, decisions, aot, jit, hot-reload]
created: 2026-08-15
status: accepted
---

# ADR-0002：AOT vs JIT — Roslyn 动态编译与 NativeAOT 的取舍

> 决策状态：**accepted**（2026-08-15）
> 关联待定项：`docs/architecture/02-plugin-model.md` §10

## 背景（Context）

Cordis C# 版的核心卖点是**插件热重载**：插件 = 单文件 `.cs`，运行时由 Roslyn 内存编译（`CSharpCompilation.Create` + `Emit(MemoryStream)`）加载进私有 ALC，实现"改源文件 → 重编译 → 摘旧挂新"（`docs/architecture/02-plugin-model.md` §4-§7）。

同时，C# 生态有 NativeAOT 部署选项（单二进制、无 JIT、启动快、内存低）。

**两者互斥**：Roslyn 内存编译依赖完整运行时（`Microsoft.CodeAnalysis.CSharp` 包 + JIT 执行），NativeAOT 环境下动态编译不可用（AOT 没有 Roslyn 运行时编译能力）。

## 决策（Decision）

**采用方案 A：JIT 运行时 + Roslyn 动态编译（热重载完整），放弃 NativeAOT。**

- 宿主运行在完整 .NET 运行时（JIT），插件运行时由 Roslyn 内存编译加载。
- **不采用** NativeAOT 作为宿主部署形态（原因见下）。
- 若未来需要 AOT 形态，走方案 B 的插件独立进程路线（见备选方案），不牺牲热重载。

## 理由（Rationale）

1. **热重载是项目的核心价值**：整个插件模型（`02-plugin-model.md` §4-§7）围绕"改源文件即生效"设计，放弃 Roslyn 动态编译 = 放弃核心卖点，框架退化为普通 DI 组合。
2. **部署形态不是当前痛点**：设计期项目无部署需求；AOT 的收益（启动快、内存低、单二进制）对 agent harness 类系统不是关键指标——热重载的迭代价值远大于部署便利。
3. **Roslyn 编译成本可接受**：单文件插件编译几十到几百毫秒，配编译缓存（文件 hash → assembly）后启动/重载均无感。
4. **保持生态兼容**：JIT 运行时下可用全部 .NET 生态（调试器、诊断、第三方库），AOT 有兼容性限制（反射、动态加载受限）。

## 权衡 / 风险（Trade-offs / Risks）

| 风险 | 说明 | 缓解 |
|------|------|------|
| 启动性能 | JIT 启动比 AOT 慢（秒级 vs 毫秒级） | 非关键指标；ReadyToRun（R2R）可部分缓解 |
| 内存占用 | JIT + Roslyn 运行时 > AOT 单二进制 | 非关键指标；64GB 开发机无压力 |
| 分发复杂度 | 需要 .NET 运行时环境 | 框架类项目本就要求运行时，非减分项 |

## 备选方案（Alternatives）

| 方案 | 描述 | 结论 |
|------|------|------|
| A. JIT + Roslyn 动态编译 | 完整运行时，热重载完整 | **采纳** |
| B. NativeAOT 宿主 + 插件独立进程 | 插件编译成 dll 由宿主进程加载（宿主可 AOT），插件运行时 JIT 编译进独立 ALC；需验证 AOT 环境可行性 | 不采纳（本期）；若未来需 AOT 部署，走此路线，但插件独立进程成本高（见 ADR-0001 方案 B 代价 5-10 倍） |
| C. 混合 | 宿主 AOT + 插件运行时 JIT | 依赖 AOT 环境下 Roslyn 运行时编译可行性，当前 .NET AOT 不支持，排除 |

## 影响（Consequences）

- 项目保持标准 `dotnet` 项目配置（非 PublishAot）。
- `docs/architecture/02-plugin-model.md` §4 的 Roslyn 内存编译路线不受影响，按原设计实现。
- 未来若要 AOT：单独评估方案 B，不改变核心插件模型。

## 关联

- `docs/architecture/02-plugin-model.md` §4（插件加载）、§10（待定）
- `docs/architecture/01-overview.md` §7（待定决策）
- ADR-0001（插件安全边界：同进程可信代码默认，本决策不引入进程边界）
