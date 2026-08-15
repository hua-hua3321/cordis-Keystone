---
type: adr
tags: [cordis-csharp, adr, configuration, yaml]
created: 2026-08-15
status: accepted
id: adr-0012
---

# ADR-0012：保留 YAML 自定义 tag 做静态插值（补充 ADR-0011 的边界澄清）

## 背景

ADR-0011 弃用 Cordis `!!js` 配置表达式后，被质疑："C# 有 YAML 操作库（YamlDotNet），没法替换？"

澄清一个关键边界：**`!!js` 机制是两步，弃用的是第二步，不是第一步**：

| 步骤 | Cordis 实现 | C# 可实现性 |
|------|------------|------------|
| ① 解析：`!!js` YAML tag → 表达式节点 | `new yaml.Type('tag:yaml.org,2002:js', { construct: () => ({ __jsExpr }) })` | ✅ **完全可行**：YamlDotNet `WithTagMapping`/自定义 `INodeDeserializer` |
| ② 求值：表达式节点 → 运行期值 | `new Function('ctx','expr','with(ctx){return eval(expr)}')`，在 loader/fiber 上下文求值 | ❌ **不可行**：C# 等价 CSharpScript/CodeDom——规则 0 第 4 条禁止；ADR-0002 例外仅覆盖插件加载层，配置层是宿主路径 |

## 决策

1. **保留 YamlDotNet 自定义 tag 解析机制**，但只承载**加载期静态插值**（确定性数据变换，非代码求值）：

| tag | 语义 | 例子 |
|-----|------|------|
| `!!env NAME` | 环境变量静态替换（加载期查一次） | `root: !!env PLUGIN_DATA_DIR` |
| `!!file path` | 文件内容静态引入（加载期读一次） | `config: !!file ./defaults.yaml` |
| YAML anchors/aliases + merge keys | YamlDotNet 内建复用 | `<<: *common` |

2. **弃用范围维持 ADR-0011**：配置内任意代码求值（`!!js` 的 eval 语义）不做；运行期状态条件（"服务 X 不存在才禁用"）由既有声明式机制表达——ADR-0007 依赖门控（服务缺失 → 依赖方 PENDING）就是这类需求的原生解，不需要配置里写代码。

## 理由

1. **解析 ≠ 求值**：tag 解析是静态的、可校验的；eval 才是动态代码执行。两者在规则 0 下的待遇完全不同
2. **静态插值有真实需求**：环境相关路径/密钥引用是配置常见诉求；`!!env`/`!!file` 是加载期查表/读文件，审计友好、AOT 安全、无副作用
3. **YamlDotNet 能力复用**：项目已选 YAML 配置形态（00 技术栈），自定义 tag 是库内建扩展点，零新依赖

## 权衡与风险

- **不引入求值引擎**：`!!js` 的表达式能力（如 `ctx.loader.entries().length > 3`）确实损失——接受，这类逻辑属于管理层/hosting API（09 §5），不属于配置数据
- **静态插值的安全边界**：`!!env`/`!!file` 也需限制——只允许加载期读取，不允许递归引用/循环引用（实现期校验：引用图无环）

## 备选方案

1. **完整移植 `!!js`（YamlDotNet 解析 + Roslyn 脚本求值）**——被否决：求值=规则 0 第 4 条违反
2. **声明式条件 DSL**（`disabled-when: {service: absent}` 结构化谓词）——评估：90% 场景已被分层叠加/依赖门控覆盖，暂不引入；实现期如出现真实诉求再按 ADR 流程评估（届时用 YamlDotNet 解析该 DSL 的数据结构，依旧不求值代码）

## 影响

- 08-configuration-layer §3：配置插值仅支持静态 tag（`!!env`/`!!file`/anchors），不支持求值
- 08 §5：schema 校验范围扩展——静态插值结果参与校验；引用环检测
- ADR-0011 维持不变（本 ADR 是对其边界的补充澄清，非推翻）

## 关联

- ADR-0011（被补充澄清的对象）
- 规则 0 第 4 条（禁止动态代码执行）
- 08-configuration-layer §3/§5、11-gap-register F1、12 §10（F 系列）
