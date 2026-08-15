# cordis-csharp 治理规则

> 继承 [`central-governance`](../../central-governance) 通用治理框架。
> 自动生成于 2026-08-15

---

## 项目信息

| 项目 | 值 |
|------|-----|
| 名称 | cordis-csharp |
| 根目录 | /Users/tovis/Projects/cordis-csharp/GOVERNANCE.md |
| 语言 | dotnet |
| 构建命令 | <!-- 如 dotnet build, npm run build --> |
| 测试命令 | <!-- 如 dotnet test, npm test --> |

---

## 共享基础设施

本项目自动继承以下共享 MCP 服务和技能包。详见 [`config/shared-infrastructure.md`](../../central-governance/config/shared-infrastructure.md) 和 [`config/skill-matrix.json`](../../central-governance/config/skill-matrix.json)。

| 类型 | 名称 | 说明 |
|------|------|------|
| 📦 Skill | central-governance | 治理规则管理（init/rules/check/contribute） |
| 📦 Skill | error-knowledge-mcp | 跨项目错误模式知识库 |
| 📦 Skill | graphify | 知识图谱（代码结构分析） |
| 📦 Skill | qmd | 知识库混合检索（按需启用） |
| 🔌 MCP | error-knowledge | 错误搜索/记录/统计服务 |
| 🔌 MCP | qmd | BM25 + 向量 + LLM 重排序检索 |

---

## 选用规则

<!-- 勾选当前项目适用的规则，不用的删掉或注释 -->

### 通用规则（所有语言）

- [ ] R00 错误知识库 MCP — 遇到报错先搜知识库再排查，解决后主动记录
- [ ] R01 子任务约束 — 大任务拆小子任务，子智能体必设超时（单任务 ≤5min）
- [ ] R02 安全基线 — 不提交密钥、Token、密码、凭证到仓库
- [ ] R03 提交规范 — 单功能单提交，提交消息格式 `<type>: <desc>`
- [ ] R04 TDD — 先写失败测试（红）→ 最小实现（绿）→ 重构
- [ ] R05 代码风格 — 遵循既有风格，优先复用现有模式
- [ ] R06 命名规范 — 统一文件/目录/接口/ID 命名规则
- [ ] R07 graphify 配置 — 知识图谱工具配置与使用规范
- [ ] R08 hermes 协作 — 平等探讨而非盲从，业务目标优先
- [ ] R09 编辑范围保护 — 不回滚/不越界/不改未要求文件
- [ ] R10 文档治理 — 主索引 + frontmatter 标准 + 一致性检查
- [ ] R11 验证透明度 — 最小验证 + 跳过必声明
- [ ] R12 调试方法论 — 先调查再修复（隧道视野/改动计数/传播检查）
- [ ] R13 Backlog 纪律 — 推迟必有回补（三失败模式）
- [ ] R14 Agent 协作契约 — 指挥官 vs 操作员 + 止损分级
- [ ] R15 方案审查协议 — 多角色审查 + 连通性检查
- [ ] R16 Worktree 隔离 Agent 协议
- [ ] R17 软件设计基本原则 — 面向对象 + SOLID + 组合优于继承
- [ ] R18 API 统一响应格式 — 数据与错误的标准包装
- [ ] R19 重构纪律 — 内部重构不留兼容，立即暴露问题

### 语言专用规则（dotnet）

- [ ] D01 C# / .NET 代码风格规范
- [ ] D02 禁用 C# 原生 enum → Smart Enum
- [ ] D03 C# 命名约定
- [ ] D04 现代 C# 语法偏好
- [ ] D05 C# 测试规范
- [ ] D06 .NET 测试陷阱 — env var gate + stale dll + 分层探针
- [ ] D07 上帝类禁令 — 量化阈值 + 三阶段拆分协议
- [ ] D08 过时标记规范 — [Obsolete] 特性 + 替代推荐

---

## 项目特有规则

> 以下为本项目独有的规则，不属于中央库。
> 标注 `<!-- @publish -->` 的表示愿意共享到中央库（scan 会收集）；
> 不标注的默认为 `@local`（仅本项目）。

### 规则 0：AOT 就绪编码标准（最高优先级，先于一切规则）

<!-- @publish -->
<!--
触发条件：本项目任何 C# 代码编写/审查/提交
约束动作：即使当前不采用 NativeAOT（ADR-0002：JIT + Roslyn 动态编译），所有代码必须按 AOT 兼容标准编写——
  1) 禁止 Reflection.Emit / Expression.Compile / 运行时动态程序集（Roslyn 插件编译层是唯一刻意例外）
  2) 反射动态加载仅限插件加载层，业务代码禁止运行时反射（改用 Source Generator / 编译期已知类型）
  3) 序列化显式（[MessagePackObject]/[JsonSerializable]），不依赖运行时反射序列化
  4) 禁止 CSharpScript / CodeDom / Assembly.Load(byte[]) 于宿主路径
  5) IOptions 配置绑定用编译期泛型，不写反射式绑定
  6) 规避 AOT 不兼容 API（BinaryFormatter / Type.GetType(string) 运行时解析等）
  7) 裁剪告警（ILLink/rd.xml）视为构建错误处理
  验证：提交前跑 `dotnet publish -c Release -r <rid> --self-contained /p:PublishAot=true` 冒烟，或 `dotnet build -warnaserror` 无裁剪警告
为什么有效：AOT 兼容是单向约束——从 JIT 迁 AOT 成本极高（反射/动态生成/序列化全要重写），从 AOT 标准写 JIT 代码零成本。写码时守 AOT 标准 = 后期切换零改动。跨项目通用：任何"当前不 AOT 但未来可能"的 .NET 项目都应遵守。
-->

**本项目当前不采用 NativeAOT（ADR-0002），但所有代码必须按 AOT 兼容标准编写**——后期切换 AOT 零改动直接可用。完整约束见 AGENTS.md 规则 0。唯一例外：插件加载层（Roslyn 内存编译 + 独立 ALC，ADR-0002/ADR-0001）。

