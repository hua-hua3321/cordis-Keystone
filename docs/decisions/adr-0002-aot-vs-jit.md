---
type: adr
tags: [cordis-csharp, decisions, aot, jit, hot-reload]
created: 2026-08-15
status: accepted
---

# ADR-0002：AOT vs JIT — Roslyn 动态编译与 NativeAOT 的取舍

> 决策状态：**accepted**（2026-08-15）
> 关联：`docs/architecture/02-plugin-model.md` §10、`docs/architecture/01-overview.md` §7（均已决，索引本 ADR）

## 背景（Context）

Cordis C# 版的核心卖点是**插件热重载**：插件 = 单文件 `.cs`，运行时由 Roslyn 内存编译（`CSharpCompilation.Create` + `Emit(MemoryStream)`）加载进私有 ALC，实现"改源文件 → 重编译 → 摘旧挂新"（`docs/architecture/02-plugin-model.md` §4-§7）。

同时，C# 生态有 NativeAOT 部署选项（单二进制、无 JIT、启动快、内存低）。

**两者互斥**：Roslyn 内存编译依赖完整运行时（`Microsoft.CodeAnalysis.CSharp` 包 + JIT 执行），NativeAOT 环境下动态编译不可用（AOT 没有 Roslyn 运行时编译能力）。本 ADR 汇总前置分析子任务（AOT/JIT 三路线可行性分析）的结论，明确取舍。

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

三条路线的完整六维权衡（维度 = 热重载能力 / AOT 单二进制部署 / 插件隔离与安全 / 启动运行性能 / 实现复杂度 / 生态兼容性）：

| 维度 | A. JIT + Roslyn 动态编译 | B. NativeAOT 宿主 + 插件独立进程 | C. 混合（宿主 AOT + 插件运行时 JIT 进 ALC） |
|------|--------------------------|----------------------------------|---------------------------------------------|
| 热重载能力 | ★★★ 完整：Roslyn 重编译→新 ALC→dispose 旧→卸载 | ★★☆ 插件进程内可自热重载；但宿主↔插件进程边界 → 重载=重启插件进程，进程内状态/上下文需外置或重建 | ★ 不可行（见分析依据） |
| AOT 单二进制/部署 | ✗ 放弃 NativeAOT；保留 JIT 自包含发布（无运行时依赖）+ R2R 预编译加速 | ✓ 宿主单文件原生二进制；插件独立进程各自打包 | 纸面可行（宿主 AOT），实际不可行 |
| 插件隔离与安全 | 同进程 ALC 隔离（与 ADR-0001 决策 1 零成本兼容）；插件崩溃影响宿主 | 进程级强隔离（故障/资源/权限） | — |
| 启动/运行性能 | 启动偏慢（JIT）；运行期热路径可 tiered 接近原生；插件调用零 IPC 开销 | 宿主启动快；每次调用跨进程 IPC，延迟+序列化开销，推翻 01-overview §6 域内直接调用原则 | — |
| 实现复杂度 | 低：沿用 D5 现成管线，无新机制 | 高：进程编排+IPC 契约+生命周期同步+调试复杂度（ADR-0001 已估成本 5-10x） | 中→但死路 |
| 生态兼容性 | 完整：所有 NuGet/反射/动态生成可用 | 宿主侧受 AOT trimming 限制（Roslyn 编译器自带 IL2104/IL3000 警告）；插件进程完整 | — |

结论：**A 采纳，B 不采纳（本期，作为未来 AOT 演进路线预留），C 不采纳（不可行）。**

- **A**（采纳）：唯一与已决决策全部零冲突的路线（ADR-0001 同进程可信模型、D5 热重载管线、01-overview §6 域内直接调用），且热重载是 C# 版核心价值，A 是唯一完整支持热重载的路线。
- **B**（本期不采纳）：成本高（5-10x）、推翻域内直接调用原则；但它是未来出现硬性 AOT 需求时的唯一演进路径——经 ADR-0001 预留的 `IPluginHost` 扩展点实现，不改变核心插件模型。
- **C**（排除）：依赖 AOT 环境下 Roslyn 运行时编译 + 动态加载，已被官方文档与本机实验证伪（见下）。

## 分析依据（Analysis Basis）

### 路线 C 可行性结论：**不可行（.NET 10 现状）**

三重证据链：

1. **官方文档**（[Native AOT deployment - learn.microsoft.com](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)）明确列出 NativeAOT 限制：*No dynamic loading, e.g. Assembly.LoadFile* 与 *No runtime code generation, e.g. System.Reflection.Emit*。
2. **本机实验**（.NET 10.0.301, osx-arm64, `PublishAot=true`，对照项目 `/private/tmp/naot-alc-test/HostAot` 与 `/private/tmp/naot-alc-test/HostJit`）：

   | 步骤 | JIT 对照（HostJit） | NativeAOT（HostAot） |
   |------|--------------------|----------------------|
   | IsDynamicCodeSupported | True | **False** |
   | Roslyn Emit（.cs→PE） | OK (2048B) | **OK (2048B)** ← 编译本身不依赖动态代码生成 |
   | ALC.LoadFromStream | OK | **PlatformNotSupportedException** |
   | ALC.LoadFromAssemblyPath | OK | **PlatformNotSupportedException** |
   | 反射实例化+调用 | OK | —（加载已失败） |

   关键结论：Roslyn Emit 在 AOT 进程内能编译出 PE，但 **AOT 运行时没有 JIT/解释器，动态加载的 IL 程序集无法执行**——两条 ALC 加载路径全部抛 `PlatformNotSupportedException`。
3. **无逃生口**：.NET 10 无 NativeAOT interpreter 实验开关（dotnet/runtime 无 `InterpreterSupport` 产物，搜索 0 命中）。

### 路线 B 的前提修正

任务分解时的路线 B 表述"插件编译为 dll 由宿主加载"**不成立**：NativeAOT 宿主加载不了任何非 AOT 编译的 dll。路线 B 若走，插件必须是**独立进程（自带 runtime）**，宿主只经 IPC 调用——即 ADR-0001 预留的 `IPluginHost` 隔离形态。本 ADR 的 B 行按修正后的定义评估。

### 附加真实限制（写进 ADR 防再踩）

- 单文件 AOT 下 `typeof(X).Assembly.Location` 返回空字符串——宿主接口即使想编译也拿不到可引用 dll，必须外部 ref pack；Roslyn 编译器自身在 AOT 下报 IL3000。
- 因此任何"宿主 AOT + 动态加载插件"的方案在 .NET 10 都是死路，不满足 C# 版热重载卖点。

## 影响（Consequences）

- 项目保持标准 `dotnet` 项目配置（非 PublishAot）。
- `docs/architecture/02-plugin-model.md` §4 的 Roslyn 内存编译路线不受影响，按原设计实现。
<<<<<<< HEAD
- **AOT 就绪约束**：虽然当前不采用 NativeAOT，本项目所有代码必须按 AOT 兼容标准编写（AGENTS.md 规则 0）——禁止运行时反射/动态生成/反射序列化于宿主路径，保证未来切换 AOT 零改动。
- 未来若要 AOT：单独评估方案 B，不改变核心插件模型。
=======
- 未来若要 AOT：单独评估方案 B（经 `IPluginHost`），不改变核心插件模型。
>>>>>>> wt/t_07b173f0

## 关联

- 分析依据：AOT/JIT 三路线可行性分析任务（kanban t_a6956719，完整权衡表 + 路线 C 结论）
- `docs/architecture/02-plugin-model.md` §4（插件加载）、§10（待定，本 ADR 落地后已决）
- `docs/architecture/01-overview.md` §7（已决决策索引）
- ADR-0001（插件安全边界：同进程可信代码默认，本决策不引入进程边界）
