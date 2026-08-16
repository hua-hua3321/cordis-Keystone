---
type: architecture-doc
tags: [cordis-csharp, architecture, gap-tracking]
created: 2026-08-15
---

# 11 — 差距跟踪表（Gap Register）

> 所有 Cordis 迁移差距（07 的 G1-G16）与补充排查项（N1-N6 + 其他）的**处理状态矩阵**。
> `07-cordis-migration-gap.md` 是分析快照（历史基线，保持稳定）；**本文是跟踪载体（现状，随实现期持续更新）**。

## 1. 状态值域

| 状态 | 含义 |
|------|------|
| ✅ 已收敛 | 已有 ADR 决策或架构文档设计落地 |
| ⚠️ 部分 | 核心语义已覆盖，仍有未决细节（备注列注明） |
| 显式弃用 | 有 ADR 决策记录的不做项（区别于"默认丢弃"） |
| 委托 | 决策不变，实现层组合外部实现（MAF） |
| ❌ 开放 | 无对应处理（当前应无此项，若有请立即分流） |

## 2. 差距清单（G1-G16，来源 07）

| # | 差距 | 优先级 | 状态 | 落点 | 备注 |
|---|------|--------|------|------|------|
| G1 | 插件生命周期状态机 | P0 | ✅ 已收敛 | ADR-0005 + 02 §6 | 状态机 + 迁移图 |
| G2 | quiesce 收敛协议 | P0 | ✅ 已收敛 | ADR-0005 + 02 §7 | 卸载五步闸门 + 测试门 |
| G3 | 插件粒度 restart/update | P1 | ✅ 已收敛 | ADR-0005 | 含 FAILED 态处理 |
| G4 | key 语义 | P0 | ✅ 已收敛 | ADR-0007 + 02 §3 | key = 服务名 |
| G5 | 依赖门控激活 | P0 | ✅ 已收敛 | ADR-0007 + 02 §3 | PENDING 等待 + 服务变更重载 |
| G6 | intercept 配置 | P1 | **显式弃用**（通用机制） | ADR-0010 + 05 §5 | IOptions 命名选项为最终形态；C# 对应物见 [12-cordis-semantics-mapping.md](12-cordis-semantics-mapping.md) §2 |
| G7 | 按服务隔离 | P1 | ✅ 已收敛 | 03 §2.2 | 命名子 scope |
| G8 | set 属主校验 | P1 | ✅ 已收敛 | 03 §2.3 | context 门面属主薄封装 |
| G9 | Impl.check 谓词 | P2 | **显式弃用** | ADR-0010 | 注册即用；未就绪由插件运行期自管；C# 对应物（Ready Task/健康探针）见 [12-cordis-semantics-mapping.md](12-cordis-semantics-mapping.md) §3 |
| G10 | serial/bail 分发 | P0 | ✅ 已收敛 | ADR-0006 + 06 §2 + 04 §3 + 03 §4 | 五种模式全集 |
| G11 | 日志命名规则 | P2 | ✅ 已收敛 | 05 §5 | category = 能力域/插件 ID |
| G12 | 日志级别/provider | P2 | ✅ 已收敛 | 05 §5 | IOptions 覆盖 + provider 清单 |
| G13 | manifest inject | P0 | ✅ 已收敛 | ADR-0007 + 02 §1 | 服务级依赖字段 |
| G14 | rebind 语义 | P1 | ✅ 已收敛 | 03 §2.1 | 同 scope 重复注册报错 |
| G15 | 事件过滤形状 | P2 | ✅ 已收敛 | 03 §5 | hook 记录 ctx + scope 链过滤 |
| G16 | 动态能力丢弃清单 | P2 | ⚠️ 部分 | 07 §2.3 | 5 项已记录；**实现期在 10-plugin-sdk §8 补"已接受丢弃"引用，防回归** |

## 3. 补充排查项（N1-N6 + 其他，来源补充排查）

| # | 差距 | 状态 | 落点 | 备注 |
|---|------|------|------|------|
| N1 | 插件 Config schema 校验 | ✅ 已收敛 | 08 §5 + 10 §2 | 校验后注入 |
| N2 | 配置条目语义（id/disabled/group/isolate） | ✅ 已收敛 | 08 §3 | 分层叠加见 08 §4 |
| N3 | disposal-aware 计时器 | ✅ 已收敛 | 10 §4 | 随 fiber 回收 |
| N4 | 脚手架/模板工程 | ✅ 已收敛 | 10 §7 | dotnet new cordis-plugin |
| N5 | 配置热更新触发 | ✅ 已收敛 | 08 §6 | 双触发（插件源文件/配置文件） |
| N6 | config 注入（插件拿配置） | ✅ 已收敛 | 10 §2 | InitializeAsync(ctx, config) |
| O1 | 事件持久化存储 | ✅ 已收敛 | ADR-0009 + 03 §4 | append-only 事件日志 + IEventStore |
| O2 | 跨域编排流程 | 委托 | ADR-0008 + 06 §6 | 实现层组合 MAF Workflows（TaskId 映射为实现期细节） |
| O3 | EventsService 接口形态 | ✅ 已收敛 | 10 §4 | Subscribe*/Emit* 按模式分方法 |
| O4 | 宿主嵌入形态（hosting API） | ✅ 已收敛 | 09 §5 | StartAsync/ShutdownAsync/Reload/DumpConfig |
| O5 | 中间件形状 A/B | ✅ 已收敛 | 04 §2 | 形状 A（IMiddleware）定案 |
| O6 | 插件脚本/DLL 双轨 | ✅ 已收敛 | 00-tech-stack T11 | 文件式应用 + ALC 管线 |
| O7 | AI 能力域组合 | ✅ 已收敛 | ADR-0008 | MAF/MCP 单向依赖；**MCP 双端已落地（P14）**：MAF Mcp 无稳定版 → 协议层组合官方稳定 SDK `ModelContextProtocol.Core` 2.2.0（ID-12），公共面 = Keystone 协议中立契约隔离（ID-13，调用方零 SDK 类型） |

### 3.1 框架层通读补充项（H/M/L 系列，来源 12-cordis-semantics-mapping §7-§9）

| # | 未解析机制 | 状态 | 落点 | 备注 |
|---|-----------|------|------|------|
| H1 | traceable 上下文跟随 | ✅ 已映射 | 05 §5 + 12 §7.1 | Activity.Current + CallerInfo（.NET 9+）+ 解析隔离；无新机制 |
| H2 | 编程式挂载（ctx.plugin/ctx.inject） | ✅ 已落地（P7） | KeystoneHost.MountAsync + PluginLoader 全管线 | 动态管道组合 + 门控 + 生命周期托管；12 §7.2 定稿 |
| H3 | 服务访问拦截（internal/get/set） | ✅ 已落地（P2） | IContextInterceptor + ContextFacade | AOT 安全门面拦截；12 §7.3 定稿 |
| M1 | 通用 ctx.effect + 诊断树 | ✅ 已落地（P2） | IContext.Effect + EffectRegistry | [CallerMemberName] 注入（net10 无 CallerInfo，ID-07）；12 §8 定稿 |
| M2 | Callable service | ✅ 已覆盖 | 10 §4 + 12 §8 | GetLogger(name) 方法形态替代；补显式声明 |
| M3 | internal/config 配置解析拦截 | ✅ 已落地（P6） | ConfigResolver + ConfigSchema | 过滤器链可否决 + 校验 + 默认值；12 §8 定稿 |
| M4 | @Inject 方法级延迟调用 | ✅ 已映射 | 12 §8 | Lazy<Task<T>> |
| M5 | Plugin.Transform | ✅ 已覆盖 | 08 §5 | IOptions 绑定 + 转换步骤 |
| M6 | CordisError 错误码 | ✅ 已覆盖 | 06 §1 | TaskResult.ErrorCode + 框架异常码表（实现期定） |
| M7 | 监听器 prepend/once | ✅ 已映射 | 12 §8 | IObservable/Rx/Dataflow 订阅原语 |
| M8 | update noSave | ✅ 已覆盖 | 08 §6 | 配置层内存更新 vs 写回 |
| L1-L9 | 环形缓冲/extend/strict/Error 展开/长栈/root/printf 格式化/ANSI 配色/listener this | ✅ 已映射 | 12 §9 | 无需对应或实现期随手处理；导出面穷举审计凭证见 12 §12 |

### 3.2 官方包源码级复查项（F 系列，第二轮，来源 12 §10）

| # | 机制 | 状态 | 落点 | 备注 |
|---|------|------|------|------|
| F1 | `!!js` 配置表达式方言 | ✅ 已决策 | ADR-0011 + ADR-0012 + 08 §3/§5 | 弃用求值（规则 0 第 4 条），保留 YamlDotNet 自定义 tag 静态插值（!!env/!!file/anchors + 引用环检测） |
| F2 | 条目级 inject 字段 | ✅ 已补设计 | 08 §3 | 与 manifest inject 合并，参与 ADR-0007 门控 |
| F3 | diff 分级重启 | ✅ 已补设计 | 08 §6.1 | name/inject/group → 冷重启；仅 config → 热更新 |
| F4 | 组级事务 | ✅ 已补设计 | 08 §6.2 | 并行应用 + 逆序回滚 + 重 id 检测 |
| F5 | 条目 CRUD API | ✅ 已补设计 | 09 §5 | create/remove/move/resolve（`:` 嵌套 id）+ 持久化 |
| F6 | 配置写回管线 | ✅ 已补设计 | 08 §6.3 | 原子写/重试/防抖/队列/readonly/事务刷新/initial 引导 |
| F7 | disabled 继承 | ✅ 已补 | 08 §3 | 父组挂起 → 子树全挂；组自身永不挂 |
| F8 | `cordis:` 内建前缀 | ✅ 已补 | 08 §3 | 内建插件命名空间 |
| F9 | loader 事件面 | ✅ 已补设计 | 09 §5 | 5 事件（含 PatchContext waterfall） |
| F10 | isolate 服务转移/GC | ✅ 已补 | 03 §2.2 | 变更语义已设计；转移优化 = 实现期 |
| F11-F13 | loader await 门控/envData/unwrapExports | ✅ 无需对应 | 12 §10 | 启动序天然覆盖/环境变量/Roslyn 无互操作问题 |
| F14 | tree-carrier 豁免 | ✅ 已覆盖（复查降级） | 12 §10 + F4 | F1 弃用表达式后插值不存在 → 豁免必要性消失，归并 F4/08 §6.2（组 config = 子条目列表结构语义） |

### 3.3 代码级复查项（CA 系列，实现后第二轮，来源 18）

> 2026-08-16 第二轮代码级审计（18-cordis-code-parity-audit）；同日 P52 **逐项二次研判**（完整读码替代抽样 grep）：CA-9 判定误报降级（effect 挂接已存在）、CA-1 缺口收窄（机制已有，缺配置接线+门控域）、CA-10 确认为唯一 P0 正确性。A 类 12 + B 类 6 ≈ 18%；**全部待人工决策**（18 §2/§3 含 v2 解决方案，§5 决策矩阵）。

| # | 差距 | 状态 | 落点 | 备注 |
|---|------|------|------|------|
| CA-9 | ~~计时器不随卸载回收~~（**初判误报**，P52 复核降级）→ 残留 CTS dispose 竞态 + 在途回调不收敛两个加固点 | ⚠️ 待决策（降级 P2 加固） | 18 §2 | effect 挂接已存在（ctx.Context.Effect + quiesce 收敛）；初版 grep 模式漏检 |
| CA-10 | 组 CRUD 不级联（删组留孤儿运行插件/建组不加载子树） | ⚠️ 待决策（**唯一 P0 正确性**） | 18 §2 | DisposeHostedAsync 抽取 + 逆序逐叶 + EnumerateActiveLeaves 加载 |
| CA-1 | isolate：运行时机制已有（独立 context 链），缺口 = EntryOptions.Isolate 配置接线 + 门控（registry）域感知 | ⚠️ 待决策（P1，**需选档**：最小=值域隔离 / 完整=registry 域感知） | 18 §2 | ContextFacade isolateNames 分支方案（v2 研判收窄） |
| CA-3 | 组级事务（并行应用+聚合+逆序回滚） | ⚠️ 待决策（建议 P1 实施） | 18 §2 | 08 §6.2 已设计未实现 |
| CA-4 | EntryTree.update 组合语义（config+移动+position） | ⚠️ 待决策（建议 P1 实施） | 18 §2 | 新 UpdateEntryAsync |
| CA-6 | initial 引导（EnsureInitialAsync 死代码） | ⚠️ 待决策（建议 P1 实施） | 18 §2 | InitialEntries 选项接线 |
| CA-12 | 服务级配置合并链（intercept 对应物） | ⚠️ 待决策（建议 P1 实施） | 18 §2 | DC-20 剩余收口；ServiceOptions + 日志首例 |
| CA-2 | 插件源文件 watcher | ⚠️ 待决策（建议 P2 实施） | 18 §2 | ReloadPluginAsync 已具备，缺触发器 |
| CA-5 | 运行期 patch 注入（Config.patches） | ⚠️ 待决策（建议 P2 实施） | 18 §2 | EntryPatcher 纯函数 |
| CA-7 | 配置写 readonly 优雅降级 | ⚠️ 待决策（建议 P2 实施） | 18 §2 | 08 §6.3 承诺 |
| CA-15 | update noSave 参数 | ⚠️ 待决策（建议 P2 实施） | 18 §3 | 防 watcher 回环写 |
| CA-13 | 依赖换实例重载（epoch uid） | ⚠️ 待决策（P2 增强） | 18 §3 | RebindPolicy 选项形态 |
| CA-8 | JSON 配置格式 | ⚠️ 待决策（建议弃用） | 18 §2 | 与 !!env 插值冲突；ADR-0014 范围 |
| CA-11 | `cordis:` 内建前缀 | ⚠️ 待决策（建议弃用） | 18 §2 | 内建 = 宿主组合根直接构造 |
| CA-14/16/17/18 | await 抛错/listener·dispatch 事件/写队列粒度/Service 基族 | ⚠️ 待决策（建议接受差异） | 18 §3 | 12 §补注记 |

## 4. 当前开放项

**无 ❌ 开放项**——G1-G16 与全部补充排查项均已收敛、显式弃用或委托。**CA 系列（§3.3，18 项）为 2026-08-16 代码级审计新增，全部 ⚠️ 待人工决策——这是当前唯一的开放批次**。历史残留跟踪点：

| 项 | 性质 | 实现期动作 |
|----|------|-----------|
| G16 防回归 | ⚠️ 部分 | 10-plugin-sdk §8 补"已接受丢弃"引用（accessor/mixin/trace 等 5 项，来源 07 §2.3） |
| O2 TaskId 映射 | ✅ 已验证（P12） | WorkflowBridge fan-out/fan-in TaskId/ParentTaskId 贯穿测试（ADR-0008 不回退项闭合） |
| 事件格式迁移 | 实现期 | ADR-0009 风险表：StoredFact.SchemaVersion 迁移策略 |
| （H2/H3/M1/M3 已随 P2/P6/P7 落地闭合，见 §3.1） | ✅ | — |

## 5. 跟踪纪律

- **状态值域**：只用 §1 的五种状态，不用模糊表述
- **更新时机**：实现期每收敛一项 → 更新状态 + 落点；新差距发现 → 追加行（编号延续 N/O/H/M/L/F 序列，注明来源）
- **07 与本文的关系**：07 保持为分析快照（历史基线）；本文是现状跟踪载体。**07 的差距清单不再随状态变化改写**（除了 §5 收敛状态段落的 ADR 指针）
- **对应治理**：差距收敛遵循 R10（改文档同步索引 + ADR）与 ADR 流程（新决策落地前写 ADR 到 decisions/）

## 6. 与相邻文档的关系

| 文档 | 关系 |
|------|------|
| 07-cordis-migration-gap.md | 分析快照（本表的状态来源） |
| decisions/README.md | ADR-0005~0011 是本表"已收敛/显式弃用"的决策依据 |
| 02/03/04/05/06/08/09/10 | 各差距的设计落点 |
| 12-cordis-semantics-mapping.md | 被弃用机制的 C# 对应物字典（G6/G9 等） |
| 18-cordis-code-parity-audit.md | 实现后第二轮代码级审计（CA 系列来源 + 实现提案） |
