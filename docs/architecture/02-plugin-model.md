---
type: architecture-doc
tags: [cordis-csharp, architecture, plugin-model]
created: 2026-08-15
---

# 02 — 插件模型

> 插件的定义、加载、注册、回收、热重载。决策 D1/D2/D5。

## 1. 插件定义

插件 = 标准单文件源文件（.cs）+ manifest（plugin-id + 版本 + 依赖白名单）。

```json
// 插件 manifest（cordis.plugin.json）
{
  "id": "plugin-fs-local",
  "version": "1.0.0",
  "main": "FsLocalPlugin.cs",
  "dependencies": ["cordis-runtime", "cordis-contracts"],
  "provides": ["fs"],
  "inject": ["llm", "telemetry"]
}
```

插件只能实现宿主定义的强类型接口（接口白名单），插件 API 面 = 宿主显式暴露的接口集。

**两个依赖维度（正交，勿混）**（ADR-0007 决策 2）：

| 字段 | 维度 | 解决什么 |
|------|------|---------|
| `dependencies` | 程序集编译白名单 | 插件代码能引用哪些程序集（Roslyn 引用集） |
| `provides` / `inject` | 服务级运行时依赖 | 插件提供/消费哪些服务（依赖图 + PENDING 等待） |

- `provides`/`inject` 里的名字是**服务名**（语义标识，类型在接口白名单声明）
- manifest 校验器校验：inject 可达、依赖图无环、provides 类型在白名单内（启动期 fail-fast）

## 2. 接口白名单（决策 D1）

**禁止** `Dictionary<string, object>` 服务注册表——会把编译期类型安全扔了，退化成 JS。

**强制**：服务类型在宿主侧编译期已知，注册表为强类型 key：

```csharp
// 服务注册：Type 为 key
registry.Register(typeof(IFsProvider), instance);
// 服务读取：编译期类型检查
var fs = ctx.Get<IFsProvider>();
```

插件只能实现宿主接口（`IFsProvider`、`ILLMProvider`、`IShellProvider` 等），
接口分层提前设计，否则插件写起来难受。

## 3. 键控服务 + 子容器（决策 D2）

**键控服务（Keyed Services）**解决"强类型接口 + 运行期实例区分"（ADR-0007 决策 1）：

```csharp
// 注册：key = 服务名（语义标识，消费者按服务名声明依赖，不感知提供者身份）
services.AddKeyedScoped<IFsProvider, LocalFsProvider>("fs");
services.AddKeyedScoped<ILLMProvider, ClaudeProvider>("llm");

// 解析：编译期类型 + 服务名 key
var fs = ctx.Get<IFsProvider>("fs");          // GetRequiredKeyedService 等价物
var llm = ctx.Get<ILLMProvider>("llm");
```

- **key = 服务名**（类型 + 名称二元组），**插件 ID 只用于子容器分组与回收**，不参与服务解析 key——否则消费者必须知道"哪个插件提供 fs"，依赖从服务契约退化成实现耦合
- 同一服务名同一 scope 内重复注册 = **报错**（rebind 语义，见 03-context §2），禁止同名覆盖

**子容器**解决"隔离 + 回收"：

```
每个插件实例 = 独立子 IServiceProvider（或 IServiceScope）
  ├─ 插件注册：AddKeyedScoped 到自己的容器（key = 服务名）
  ├─ 解析：GetRequiredKeyedService<T>(服务名)   ← 编译期类型安全
  ├─ 隔离：实例间容器独立，同名 key 不冲突
  └─ 卸载：释放整个容器 = 自动回收所有注册
```

- DI 没有原生的"移除键控服务"简单 API → 用子容器，卸载 = 扔容器
- 子容器按插件 ID 分组（回收粒度），服务解析 key 按服务名（契约粒度）——两层各司其职

**依赖门控激活**（ADR-0007 决策 3）：插件在 `inject` 声明的服务全部可用前保持 PENDING（状态机见 §6/ADR-0005），缺服务不抛异常而是等待；服务提供方卸载/替换 → 依赖方自动 reload/unload。这是"等依赖就绪再启动"的 Cordis 核心机制。

## 4. 插件加载（Roslyn 内存编译）

```csharp
// 1. 编译：单文件 .cs → 内存程序集
CSharpCompilation.Create(pluginId, ...)
    .WithReferences(引用集)      // 宿主接口白名单 + 共享库
    .Emit(peStream);             // MemoryStream

// 2. 加载：私有 ALC（可卸载）
var alc = new AssemblyLoadContext(pluginId, isCollectible: true);
var assembly = alc.LoadFromStream(peStream);

// 3. 实例化：通过宿主接口加载
var pluginType = assembly.GetType("...");
var plugin = (IPlugin)Activator.CreateInstance(pluginType);
```

编译缓存：文件 hash → assembly，避免每次启动重编译全部插件。

## 5. 依赖共享（六条工作清单）

| # | 问题 | 解法 |
|---|------|------|
| 1 | 宿主接口双向引用 | Roslyn 引用集 = 宿主 + 共享库白名单 |
| 2 | Resolving 事件 | 插件 ALC 解析不到 → fallback 到 Default ALC |
| 3 | 共享库版本冲突 | 白名单锁版本，冲突升级或锁死 |
| 4 | 传递依赖 | 显式规则：X 走共享 or 私有，不靠运气 |
| 5 | 编译引用集 vs 运行解析集一致 | 两者同源，防"编译过、跑缺引用" |
| 6 | 插件间类型共享 | 走宿主接口/DTO 中转，插件不直接引用 |
| 7 | 服务级依赖图（inject/provides） | manifest 校验器：可达性 + 无环 + 白名单检查；加载序 = 拓扑序 + PENDING 等待（ADR-0007） |

## 6. 注册回收（disposer 协议）

**每个插件必须实现 disposer 接口**，dispose 是"摘除自己注册的东西"，不是清空 context：

```csharp
public interface IPlugin : IAsyncDisposable
{
    Task InitializeAsync(IPluginContext ctx);
    // DisposeAsync: 取消全部事件注册、清空 static、释放子容器
}
```

**按插件 ID 分组回收**：每个插件的注册以 plugin ID 为 scope 记录，
`dispose(pluginId)` 只回收该 ID 的注册，不影响同 context 其他插件。

热重载 = "dispose 旧插件 + 加载新插件"，是常规操作不是危险操作。

## 7. 热重载流程

```
FileSystemWatcher 监听插件源文件
  → 变更 → 重新 Roslyn 编译
  → 新 ALC 加载新版本
  → dispose 旧插件（取消注册/清状态）
  → 挂载新插件到 context
  → 旧 ALC.Unload() + 触发 GC
```

**卸载残留（HMR 失败头号原因）**：ALC.Unload() 是尽力而为——只要有残留引用
（事件监听器、static 字段、delegate 捕获），卸载静默失败。解法 = disposer 协议强制。

## 8. 状态外置（决策 D6）

插件无状态，状态在 context：

```
能力域 actor（持 context）
  ├─ 插件：无状态，处理时从 ctx 读/写
  └─ context：状态容器（服务 + 数据），长命
```

热重载不丢状态（状态在 context 不在插件）。这也让多实例天然隔离
（每实例独立 context，状态互不可见）。

## 9. 调试

- Roslyn 内存编译默认调试器进不去 → Emit 时带 embedded PDB + source link
- 否则插件代码是黑盒，出 bug 只能靠日志

## 10. 已决决策（ADR-0001/0002/0007）

- **安全边界**：插件作为同进程可信代码执行（默认），信任边界 = 用户；预留 `IPluginHost` 扩展点支持未来进程隔离（ADR-0001）
- **插件来源**：本地文件（初始），manifest 记录版本；演进路径 = 本地+版本记录 → 本地+签名校验 → `IPluginSource` 抽象引入远程分发（ADR-0001）
- **AOT vs JIT**：JIT 运行时 + Roslyn 动态编译（热重载完整），不采用 NativeAOT；未来 AOT 走插件独立进程路线（ADR-0002）
- **key 语义 / 依赖门控**：key = 服务名（类型+名称二元组），插件 ID 仅子容器分组；manifest 增 `inject` 服务级依赖字段；插件 PENDING 等待依赖就绪，服务变更自动重载（ADR-0007）
