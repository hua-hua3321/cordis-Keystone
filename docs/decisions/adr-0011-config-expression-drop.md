---
type: adr
tags: [cordis-csharp, adr, configuration, security]
created: 2026-08-15
status: accepted
id: adr-0011
---

# ADR-0011：弃用配置内动态表达式（`!!js`），静态插值替代

## 背景

Cordis 官方包提供配置表达式机制（复查发现 F1，来源 `@deepseek-ai/cordis-plugin-include` + `cordis-plugin-loader`）：

1. **YAML 方言**：`!!js` scalar 解析为 `{__jsExpr: "..."}` 节点，写回时保持原形
2. **求值**：`new Function('ctx', 'expr', 'with (ctx) { return eval(expr) }')` —— with 作用域 eval，对 loader 上下文求值
3. **插值时机**：loader 在 `internal/config` waterfall 中递归 `interpolate()`（树载体条目豁免）；`disabled` 字段同样支持表达式求值
4. **用途**：配置里内联条件逻辑（如按环境禁用条目、引用运行期值）

## 决策

**C# 版不做配置内动态表达式。** 配置是纯静态数据；动态性由静态机制承接：

| Cordis 表达式用途 | C# 静态替代 |
|------------------|------------|
| 按环境禁用条目（`disabled: !!js ...`） | Profile 分层叠加（08 §4）：环境差异放进 patch 层 |
| 引用环境变量/运行期值 | .NET 配置提供程序（环境变量/JSON 多源合并）+ 占位符插值（静态替换，非求值） |
| 条件逻辑 | 配置层分层（08 §4）+ manifest 校验（08 §5），不在配置里写代码 |

## 理由

1. **规则 0 第 4 条**：`new Function + eval` 的 C# 等价物是 `CSharpScript`/`CodeDom`——宿主路径禁止动态代码执行。配置层是宿主路径
2. **可审计性**：静态配置可 diff、可 schema 校验、可安全审查；表达式把逻辑藏进数据
3. **分层清晰**：Cordis 表达式实际用途（环境差异）在 08 §4 分层叠加里有更干净的解
4. **AOT 兼容**：动态求值与 AOT 裁剪冲突

## 权衡与风险

- **损失**：无法在单个配置文件里表达"运行期条件"（如根据某服务是否存在决定 disabled）。接受——这类逻辑属于管理层/hosting API（09 §5），不属于配置数据
- **迁移成本**：从 Cordis 迁移配置时 `!!js` 节点需人工改写为分层配置

## 备选方案

1. **纳入表达式机制（Roslyn 沙箱求值）**——被否决：违反规则 0，配置层引入编译器依赖得不偿失
2. **只支持只读表达式（无副作用 DSL）**——被否决：仍需求值引擎，且 90% 用途已被分层覆盖

## 影响

- 08-configuration-layer：条目模型与解析管线不含表达式节点；配置插值仅支持静态占位符（环境变量展开）
- 12-cordis-semantics-mapping F 系列记录映射
- `disabled` 字段为纯布尔（不做表达式求值）

## 关联

- 规则 0（AGENTS.md）
- ADR-0010（同为"显式弃用 Cordis 机制"先例）
- 08-configuration-layer §3/§4/§5、11-gap-register F1、12 §10（F 系列）
