# 贡献指南（Contributing）

感谢你考虑为 **Keystone** 地基插件框架做贡献！本文档说明如何参与开发、提交问题与合并请求（PR）。

> English version: [CONTRIBUTING.en.md](CONTRIBUTING.en.md)

## 行为准则

参与本项目即表示你同意遵守
[行为准则](CODE_OF_CONDUCT.md)。请对所有社区成员保持尊重与友善。

## 我能贡献什么？

- 报告 Bug（使用 GitHub Issue 的 Bug 报告模板）
- 提出新功能 / 设计改进（使用 Feature 请求模板，或先开 Discussion 讨论）
- 修复文档错误、补充示例与教程
- 提交代码修复与功能实现（走 PR 流程）

## 开发环境

**前置条件**

- [.NET 10 SDK](https://dot.net/)（项目使用 `net10.0` + C# 14）
- 任意支持 .NET 的编辑器（推荐 Visual Studio / VS Code + C# 插件）
- 可选：Python 3（用于运行文档 frontmatter 校验，属于内部治理工具）

**获取代码**

```bash
git clone https://github.com/hua-hua3321/cordis-Keystone.git
cd cordis-Keystone
dotnet restore cordis-csharp.slnx
```

## 构建与测试

```bash
dotnet build cordis-csharp.slnx            # 警告即错误（TreatWarningsAsErrors + 分析器）
dotnet test  cordis-csharp.slnx            # 500+ 单元测试
```

风格校验（CI 也会跑）：

```bash
dotnet format cordis-csharp.slnx --verify-no-changes
```

### AOT 就绪纪律（最高优先级）

本项目**当前不启用 NativeAOT**（ADR-0002），但**所有宿主代码必须按 AOT 兼容标准编写**（见
`AGENTS.md` 规则 0）。提交前请确保：

- 不写运行时 `Reflection.Emit` / `Expression.Compile` / 动态程序集生成
- 业务代码不依赖运行时反射；优先用源生成器（Source Generator）或编译期已知类型
- 序列化使用显式契约（`[MessagePackObject]` / `[JsonSerializable]`）
- 不调用 `CSharpScript` / `CodeDom` / `Assembly.Load(byte[])`
- 提交前跑一次 AOT 冒烟（若当前配置可用）：
  ```bash
  dotnet publish src/Keystone.Core -c Release -r <rid> --self-contained /p:PublishAot=true
  ```
  唯一例外：插件加载层（Roslyn + ALC，ADR-0001/0002）。

### 编码风格

- 项目已提供 `.editorconfig` 与 `Directory.Build.props`，请遵循既有风格。
- 优先复用现有模式（命名约定、结构化日志、错误码 `KeystoneException`）。
- 不重造 .NET 已提供的能力（DI、配置、日志、中间件形状）。

## 提交规范

提交信息采用 `<type>: <desc>` 形式：

| type      | 含义                         |
|-----------|------------------------------|
| `feat`    | 新功能                       |
| `fix`     | 缺陷修复                     |
| `docs`    | 文档                         |
| `refactor`| 重构（无行为变化）           |
| `test`    | 测试                         |
| `chore`   | 构建/工具/杂项               |

示例：`fix: 修复热更新时同名服务注册冲突导致冷重启报错`

**单功能单提交**，保持提交原子、可回退。

## 设计决策（ADR）

涉及架构/接口层面的变更，**先写 ADR** 到 `docs/decisions/`（ADR-0001 ~ ADR-0018 已收敛），
并在 `AGENTS.md` 索引与关联架构文档中同步。实现期的新决策走实施记录
`docs/architecture/14-implementation-log.md` 第 4 节通道。

## PR 流程

1. Fork 并基于 `main` 创建特性分支（`feat/xxx`、`fix/xxx`）。
2. 确保 `dotnet build` / `dotnet test` / `dotnet format` 全部通过。
3. 填写 PR 模板，说明变更动机、范围与验证方式。
4. 至少一个维护者 Review 通过后合并（见 [CODEOWNERS](CODEOWNERS)）。
5. 若涉及用户可见行为变化，同步更新 `CHANGELOG.md` 与对应文档。

## 文档

- 架构文档位于 `docs/architecture/`，决策位于 `docs/decisions/`，教程位于
  `docs/tutorials/`。
- 新增/修改架构文档请保持 frontmatter 头（内部治理工具会校验；不影响普通贡献）。

---

再一次感谢你的参与！🫘
