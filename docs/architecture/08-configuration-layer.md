---
type: architecture-doc
tags: [cordis-csharp, architecture, configuration]
created: 2026-08-15
---

# 08 — 配置层设计

> 三层架构的第一层：配置形态、条目模型、分层叠加、schema 校验、热更新触发。
> 本文补齐 01-overview §2"配置层"的专题设计（来源：补充排查 N2/N5）。

## 1. 职责与定位

配置层回答三个问题：

1. **声明什么**：插件清单、能力域定义、管道组成、scope 父子关系
2. **怎么写**：文件格式、条目模型、分层叠加（profile/组合包/patch/overlay）
3. **怎么生效**：schema 校验（启动期 fail-fast）+ 配置热更新（运行期原子替换）

配置层是**声明式**的：管理层读配置 → 构建运行时（actor/context/管道），运行时本身不感知配置文件细节。

## 2. 配置形态与文件格式

- **YAML 默认**（对齐 harness `cordis.yml` 生态），JSON 可选（同构映射）
- 主配置文件：`cordis.yml`（宿主级：能力域定义 + 管道组成 + 分层叠加）
- 插件自带配置块：`config` 字段按条目内联（见 §3），插件自身 schema 校验（见 §5）
- **配置来源解绑（ADR-0013，P0 已落地）**：配置经提供者抽象获取——基于 M.E.C `IConfigurationSource`/`IConfigurationProvider` 契约（`src/Keystone.Config`）：
  - **默认源（开发阶段，ADR-0014）**：本地 YAML（`keystone.yml`，YamlStream 节点树解析，AOT 安全）；**AgileConfig 配置中心为预留可选源**（提供者代码就绪，`AddAgileConfig` 显式追加可用，开发阶段不进入默认组合）
  - 优先级：YAML > 代码内文档化默认值；配置中心未来启用时沿用 M.E.C"后添加者优先"（配置中心 > YAML > 默认值）
  - 用户自定义来源：实现 `IConfigurationSource` + `Add...()` 扩展（`KeystoneConfigBuilder`/`ConfigurationBuilderExtensions` 为入口）
  - 框架可调值（插件目录/超时/并发/日志级别）经 `KeystoneSettings`（`keystone` 节）绑定——**禁止硬编码**

```
cordis.yml                       # 宿主级配置（能力域/管道/分层）
plugins/
  plugin-fs-local/
    cordis.plugin.json           # 插件 manifest（02-plugin-model §1）
    FsLocalPlugin.cs             # 插件源码（Roslyn 内存编译）
```

## 3. 条目模型（entry）

每个配置条目是一个可挂载/卸载的单元（对齐 Cordis loader 条目，`docs/cordis-tutorial/06`）：

```yaml
- id: fs-plugin          # 稳定标识：区分"修改条目"与"删了再加"（热更新依据）
  name: ./plugins/fs     # 插件定位（源文件路径、内置插件 ID、或 cordis: 内建前缀）
  config:                # 插件配置块（经插件 schema 校验后注入）
    root: /data
  disabled: false        # true = 挂起该条目（卸载插件但不删除条目）；纯布尔，不支持表达式（ADR-0011）
  inject:                # 条目级依赖声明（可选）：与插件 manifest inject 合并（F2）
    - telemetry
  # 组与隔离（可选）
  group:                 # 组嵌套：一组条目作为单元加载/卸载
    - id: auth
      name: ./plugins/auth
  isolate:               # 服务隔离声明（18 §2 CA-1：map 两档，对齐 Cordis Dict<name → true|"label">）
    fs: true              # true = 条目私有域（realm #entryId）
    cache: shared-a       # "label" = 命名共享域（realm @label，同 label 共享）
  # 旧列表写法 isolate: [fs] 经 shim 等价展开为全私有（fs: true）
```

| 字段 | 语义 | 对应 Cordis |
|------|------|------------|
| `id` | 稳定标识，热更新 diff 依据 | loader entry id |
| `name` | 插件定位（源文件路径 / 内置 ID / `cordis:` 内建前缀——内建插件命名空间，对齐 Loader.builtins） | loader entry name |
| `config` | 插件配置块（schema 校验后注入） | loader entry config |
| `disabled` | 挂起不删，改回即恢复（依赖它的 PENDING 插件随之加载）；**父组 disabled → 子树全部挂起**（组自身永不被挂起） | loader disabled（含继承） |
| `inject` | 条目级依赖声明，与 manifest `inject` **并集合并**（同名冲突以条目级为准，实现期验证）后参与 ADR-0007 门控 | loader entry inject |
| `group` | 嵌套条目组，单元加载/卸载 | plugin-group |
| `isolate` | 服务隔离 map 两档：`{名: true}` 私有域 / `{名: "label"}` 命名共享域 / `{名: false}` 显式解除（分层补丁撤销底层声明，合并按名移除）；列表写法 shim ≡ 全私有 | loader isolate（Dict 两档） |

> **表达式边界**（ADR-0011 + ADR-0012）：Cordis 的 `!!js` 配置表达式**求值**（任意代码 eval）不纳入——配置不写代码；但 YamlDotNet 自定义 tag 机制**保留**，承载加载期静态插值：`!!env NAME`（环境变量替换）、`!!file path`（文件内容引入）、anchors/merge keys（YamlDotNet 内建）。运行期状态条件（"服务 X 不存在才禁用"）由 ADR-0007 依赖门控表达（服务缺失 → 依赖方 PENDING），不需要配置里写逻辑。

## 4. 分层叠加（profile / patch / overlay）

harness 的配置树是**按序叠加的多层**（`docs/architecture.zh.md` Profile 与组合包）：空条目列表 → 组合包（按 profile 顺序）→ profile patch → 用户 patch → 任意 overlay。C# 版对应：

```
空条目列表
  → base 组合包（发行版默认：能力域定义 + 内置插件）
  → profile 层（具名组装，如 web/headless 模板）
  → 用户 patch（cordis.patch.yml，按 id 定位条目替换 config 或插入新条目）
  → 运行期 overlay（--patch 命令行覆盖）
```

- **patch 定位**：按条目 `id` 定位并替换整个 `config`，或插入新条目（对齐 harness patch 语义）
- **环境选择**：由 overlay 层完成（环境变量 → 选择不同 overlay 文件），条目本身保持字面值
- 叠加以**条目 id 为主键**，重复 id = 配置错误（启动期 fail-fast）

## 5. 插件 Config schema 校验

对齐 Cordis（Schemastery/Standard Schema：插件声明 schema → apply 前校验 → 坏配置精确报错、默认值补齐，`docs/cordis-tutorial/05-config`）：

- 插件用宿主提供的 schema 声明工具描述配置（C# 侧 = 源生成器友好的**声明式 schema + 编译期校验**，遵守 AGENTS.md 规则 0，不写运行时反射绑定）
- 校验时机：插件 LOADING 前（apply/InitializeAsync 之前）——**插件绝不在配置不完整时启动**
- 失败语义：精确报错（条目 id + 字段 + 期望/实际）+ 启动期 fail-fast；配置热更新场景 = 校验失败回滚旧配置（ADR-0003 决策 2）
- 默认值补齐：schema 声明默认值，`apply` 永远收到完整且验证过的配置
- 能力域级校验：`concurrency` 字段（serial/parallel）与管道版本号（触发热更新）纳入配置 schema（ADR-0003 影响）
- **静态插值校验**（ADR-0012）：`!!env`/`!!file` 在**加载期展开后**参与校验（展开结果才进 schema）；插值引用图**无环检测**（递归/循环引用 = 配置错误 fail-fast）

## 6. 配置热更新触发

两个触发维度（来源：补充排查 N5）：

| 触发源 | 机制 | 落点 |
|--------|------|------|
| 插件源文件变更 | FileSystemWatcher 监听插件目录 → Roslyn 重编译（02-plugin-model §7） | 插件粒度 reload（ADR-0005 quiesce） |
| 配置文件变更 | FileSystemWatcher 监听 cordis.yml/patch 层 → 重载配置 → schema 校验 → 原子替换 | 管道粒度 swap（ADR-0003 决策 2）+ 插件粒度 update（ADR-0005 决策 3） |

配置变更处理管线：

```
FileSystemWatcher 事件（防抖合并）
  → 重载受影响层配置
  → schema 校验（失败 → 记日志 + 保留旧配置，不回滚已运行管道）
  → diff（按条目 id 比对：新增/修改/删除/disabled 翻转）
  → 逐条目执行 update()（插件粒度，waterfall 可否决）/ 管道原子替换（管道粒度）
  → 依赖方自动重载（ADR-0007 服务可用性事件驱动）
```

### 6.1 变更分级（diff 决定热更新还是冷重启，F3）

逐条目 diff 后按**变更字段的级别**选择动作（对齐 Cordis loader entry.update 语义）：

| diff 内容 | 动作 | 依据 |
|----------|------|------|
| 无变化 | 不动 | deepEqual 相等即跳过 |
| 仅 `config` 变 | **热更新**：插件粒度 update()（ADR-0005 决策 3，waterfall 可否决） | patchContext 路径 |
| `name` / `inject` / `group` 变 | **冷重启**：dispose 旧实例 → 加载新实例（ADR-0005 restart 路径） | 全替换路径 |
| `disabled` → true | 仅卸载（条目保留） | dispose-only 路径 |

失败回滚：每步失败回滚条目选项并恢复旧实例；回滚本身失败 → 聚合异常上报（对齐 Cordis AggregateError 语义）。

### 6.2 组级事务（整组原子应用，F4）

组（group）条目列表的更新是**事务**（对齐 Cordis EntryGroup.update）：

1. 重复 id 检测（组内重 id = 配置错误，fail-fast）
2. 整组条目**并行应用**，失败聚合（单个 → 抛原因；多个 → 聚合异常）
3. 失败 → **组级回滚**：逆序卸载本次新建条目 + 按旧配置重建 + 恢复数据；回滚失败 → 聚合上报
4. 旧有而新配置没有的条目 → 卸载

> 组级事务与 §6.1 条目分级的关系：diff 分级决定**单个条目**的动作；组级事务保证**整组应用**的原子性。
>
> 边界语义：**树卸载中的组更新不回滚**——所在条目树正在卸载时（fiber 已失效），应用失败由卸载主导终止，不再执行回滚（对齐 Cordis "Disposal owns termination"）。

### 6.3 配置写回管线（F6）

运行期条目变更（09 §5 CRUD API）与 include 文件写回（对齐 Cordis plugin-include）：

- **原子写**：写临时文件 → `File.Move(tmp, target, overwrite: true)` 原子替换（对齐 tmp+rename；同卷 MoveFileEx/MOVEFILE_REPLACE_EXISTING，目标不存在也成立——`File.Replace` 要求目标存在，不采用）
- **写重试**：目标文件被占用（`IOException` HRESULT：0x80070020 共享冲突 / 0x80070005 拒绝访问，对应 Cordis EBUSY/EACCES/EPERM）→ 有限次退避重试（10 次 × 50ms 递增）
- **写防抖**：多次变更合并为一次写（next-tick 级合并）；写队列串行化避免交错
- **readonly 检测**：无写权限 → 只读模式，写操作报错不崩溃
- **事务性刷新**：重读文件 → 校验 → 应用；应用失败回滚保持旧树（"最后好树保持运行"）
- **initial 引导**：文件不存在且有 initial 配置 → 先写初始文件再加载
- **apply 串行化**：初始化应用与 watcher 首扫刷新经同一队列，防竞态交错

## 7. 与相邻文档的关系

| 文档 | 关系 |
|------|------|
| 01-overview.md | 本层是三层架构的"配置层"专题 |
| 02-plugin-model.md | manifest（cordis.plugin.json）属于插件侧配置，本文管宿主侧 cordis.yml |
| 03-context.md | scope 父子关系、isolate 服务隔离在配置层声明（§3 isolate 字段） |
| 04-pipeline.md | 管道组成（中间件顺序）在此声明 |
| 05-reliability.md | 配置校验失败 = fail-fast；热更新失败回滚 |
| ADR-0003 | 管道原子替换 + concurrency 字段的 schema 落点 |
| ADR-0007 | manifest inject/provides 校验器与本文 §5 校验同族 |
