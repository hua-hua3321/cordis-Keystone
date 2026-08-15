---
type: adr
tags: [cordis-csharp, decisions, intercept, check-predicate, scoping]
created: 2026-08-15
status: accepted
---

# ADR-0010：G6/G9 取舍决策 — 弃用 intercept 通用语义与 check 谓词

> 决策状态：**accepted**（2026-08-15）
> 关联待定项：`docs/architecture/07-cordis-migration-gap.md` 差距 G6/G9
> 来源：07 差距分析（G6 intercept 配置、G9 Impl.check 谓词）+ 差距跟踪盘点（两项仅有"承接"声明、无实际决策）

## 背景（Context）

`07-cordis-migration-gap.md` 的两项差距此前只有句号式承接声明（"由 ADR-0007 的依赖门控设计承接 / 用 IOptions 命名选项替代"），**没有形成实际决策**：

- **G6（P1）intercept 配置**：Cordis 每次注入可携带该服务的 intercept 配置（如 logger 的 name/level），沿祖先链合并（`service.ts:86-102` `resolveConfig`）。C# 设计目前只覆盖了 logger 特例（05 §5：每插件 `IOptions<T>` 命名选项覆盖 category/level），**通用 intercept 机制无 C# 形态**。
- **G9（P2）Impl.check 可用性谓词**：Cordis 依赖方加载前可查服务提供方的可用性谓词（"服务已注册但暂不就绪"，reflect.ts:124）。ADR-0007 只设计了"服务已注册"门控，**"可用但未就绪"谓词无对应物**。

两项都涉及"是否引入通用动态机制"，与项目的静态类型目标（D1 接口白名单）存在张力，必须显式决策而非默认丢弃。

## 决策（Decision）

### 决策 1（G6）：弃用 intercept 通用语义，IOptions 命名选项为最终形态

- **不做**"每次注入携带服务配置、沿祖先链合并"的通用 intercept 机制
- **最终形态**：服务配置 = 每插件 `IOptions<T>` 命名选项（08-configuration-layer §5 schema 校验后注入）+ 配置层显式合并（patch/overlay 分层，08 §4）
- 已落地的 logger 特例（05 §5：category = `{能力域}/{插件 ID}`，级别经命名选项覆盖）即代表该形态，不再扩展为通用机制

### 决策 2（G9）：弃用 check 谓词，加载序门控 = "服务已注册"

- 加载序门控范围明确为 ADR-0007 现状：**服务已注册即可用**（inject 依赖全部注册 → PENDING 转 LOADING）
- **不做**"服务可用但未就绪"谓词机制；"未就绪"属于**运行期状态**，由提供方插件自行管理（提供方插件 ACTIVE 后内部自检；需要等待外部后端的场景由插件自身用 PENDING/FAILED + 重试策略表达，05-reliability §3）
- 依赖门控的语义边界在 02-plugin-model §3 / ADR-0007 中保持不变

## 理由（Rationale）

1. **与 D1 静态类型目标一致**：通用 intercept（解析时动态注入配置）需要把服务解析参数化，本质是动态机制；C# 强类型下"配置走 IOptions 命名选项、合并走配置层"是干净且 AOT 安全的等价物（规则 0）。
2. **实际需求已覆盖**：Cordis intercept 的真实用途（logger 级别、客户端配置）都能用命名选项表达；没有第二个真实用例支撑通用机制。
3. **check 谓词是 JS 动态场景的产物**：JS 服务可"半就绪"（对象存在但内部未 ready）；C# 强类型下"注册即可用"是更清晰的契约，未就绪是运行期状态，不属于加载序问题——引入谓词会把加载序从静态可验证变成运行期动态判断，破坏启动流程的可预测性（09-management-layer §2 fail-fast）。
4. **显式弃用优于默认丢弃**：07 判定标准要求"没有显式决策记录 = 遗漏"；本 ADR 把两项从"承接声明"转为"显式弃用（有决策记录）"，防止实现期反复纠结。

## 权衡 / 风险（Trade-offs / Risks）

| 风险 | 说明 | 缓解 |
|------|------|------|
| 服务配置合并能力降级 | 无通用 intercept，祖先链合并需靠配置层显式分层 | 配置层 patch/overlay 分层（08 §4）覆盖合并需求；显式优于隐式 |
| "未就绪"表达变重 | 无 check 谓词，依赖外部后端的插件要自管状态 | 插件可用 PENDING/FAILED + 重试（ADR-0005/05 §3）表达；提供方自检是成熟模式 |
| 与 Cordis 语义不等价 | 两项被显式弃用，07 判定标准要求声明差异 | 本 ADR 即差异声明：弃用项 + 替代路径已记录，实现期不再对标 |

## 备选方案（Alternatives）

| 方案 | 描述 | 结论 |
|------|------|------|
| A（采纳） | 弃用 intercept 通用语义（IOptions 特例为最终形态）+ 弃用 check 谓词（注册即用） | **采纳**：与静态类型目标一致，启动流程保持可预测 |
| B | 实现通用 intercept 机制（服务解析参数化 + 祖先链合并） | 不采纳：动态机制与 D1 冲突，AOT 安全成本高，无第二用例 |
| C | 实现 check 谓词（依赖方加载前查可用性） | 不采纳：加载序变成运行期动态判断，破坏 fail-fast 可预测性；未就绪属运行期状态 |

## 影响（Consequences）

- `docs/architecture/07-cordis-migration-gap.md`：G6/G9 状态从"部分/开放"转**显式弃用**（§5 收敛状态更新，指向本 ADR）
- `docs/architecture/03-context.md` §2：intercept 语义的最终形态注释（IOptions 命名选项，见 05 §5）
- `docs/architecture/05-reliability.md` §5：logger 特例即为 intercept 替代形态，无需改动
- `docs/decisions/README.md` 索引增补 ADR-0010
- 11-gap-register：G6/G9 登记"显式弃用（ADR-0010）"

## 关联

- `docs/architecture/07-cordis-migration-gap.md` §2.2/§2.3 / G6 / G9（来源）
- `docs/architecture/02-plugin-model.md` §3（依赖门控，语义边界不变）、`docs/architecture/05-reliability.md` §5（logger 特例 = intercept 替代形态）
- ADR-0007（依赖门控："服务已注册"门控为最终形态，本 ADR 补弃用声明）
- ADR-0005（PENDING/FAILED 承载"未就绪"表达）
