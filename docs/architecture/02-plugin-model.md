---
type: architecture-doc
tags: [cordis-csharp, architecture, plugin-model]
created: 2026-08-15
---

# 02 — 插件模型

> 插件的定义、加载、注册、回收、热重载。决策 D1/D2/D5。

## 1. 插件定义

插件 = 标准单文件源文件（.cs）+ manifest（plugin-id + 版本 + 依赖白名单）。

```csharp
// 插件 manifest（cordis.plugin.json）
{
  "id": "plugin-fs-local",
  "version": "1.0.0",
  "main": "FsLocalPlugin.cs",
  "dependencies": ["cordis-runtime", "cordis-contracts"],
  "provides": ["IFsProvider"]
}
```

插件只能实现宿主定义的强类型接口（接口白名单），插件 API 面 = 宿主显式暴露的接口集。

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

**键控服务（Keyed Services）**解决"强类型接口 + 运行期实例区分"：

```csharp
// 注册：key = 插件 ID 或能力域实例 ID
services.AddKeyedScoped<IFsProvider, LocalFsProvider>("plugin-fs-a");
services.AddKeyedScoped<IFsProvider, RemoteFsProvider>("plugin-fs-b");

// 解析：编译期类型 + 运行期 key
var fs = ctx.GetRequiredKeyedService<IFsProvider>("plugin-fs-a");
```

**子容器**解决"隔离 + 回收"：

```
每个插件实例 = 独立子 IServiceProvider（或 IServiceScope）
  ├─ 插件注册：AddKeyedScoped 到自己的容器（key = 插件内服务名）
  ├─ 解析：GetRequiredKeyedService<T>(key)   ← 编译期类型安全
  ├─ 隔离：实例间容器独立，同名 key 不冲突
  └─ 卸载：释放整个容器 = 自动回收所有注册
```

- DI 没有原生的"移除键控服务"简单 API → 用子容器，卸载 = 扔容器
- key 必须全局唯一（插件 ID），禁止同名覆盖

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

## 10. 待定

- 安全边界：插件可信代码（同进程）vs 隔离进程
- 插件来源：本地 vs 远程分发（签名/版本管理）
- AOT 冲突：Roslyn 动态编译与 NativeAOT 互斥（二选一或插件独立进程）
