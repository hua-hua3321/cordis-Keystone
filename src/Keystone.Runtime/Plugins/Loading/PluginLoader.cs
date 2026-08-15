using Keystone.Core.Errors;
using Keystone.Runtime.Context;
using Keystone.Runtime.Plugins.Lifecycle;
using Keystone.Runtime.Plugins.Manifest;
using Keystone.Runtime.Plugins.Services;

namespace Keystone.Runtime.Plugins.Loading;

/// <summary>
/// 插件加载器（02 §4-§7 管线）：源码 → Roslyn 编译 → 私有 ALC 加载 → 实例化 IPlugin →
/// PluginRuntime 生命周期托管。热重载 = 重编译 → 新 ALC/新 runtime → 旧 quiesce + ALC.Unload
/// （ADR-0005 决策 2 第 ⑤ 步：ALC.Unload 只允许在 quiesce 收敛之后）。
/// </summary>
public sealed class PluginLoader : IAsyncDisposable
{
    private readonly PluginManifest _manifest;
    private readonly IServiceRegistry _registry;
    private readonly Func<string, IPluginContext> _contextFactory;

    private PluginAssemblyLoadContext _alc = null!;
    private PluginRuntime? _runtime;
    private WeakReference? _unloadedAlc;

    private PluginLoader(PluginManifest manifest, IServiceRegistry registry, Func<string, IPluginContext> contextFactory)
    {
        _manifest = manifest;
        _registry = registry;
        _contextFactory = contextFactory;
    }

    /// <summary>当前插件运行时（ACTIVE 后可用）。</summary>
    public PluginRuntime Runtime => _runtime
        ?? throw new KeystoneException(ErrorCode.LifecycleInvalidState, "plugin runtime is not initialized");

    /// <summary>最近一次卸载的 ALC 弱引用（测试/诊断：回收验证）。</summary>
    public WeakReference? UnloadedAlcReference => _unloadedAlc;

    public static async Task<PluginLoader> CreateAsync(
        PluginSource source,
        PluginManifest manifest,
        IServiceRegistry registry,
        Func<string, IPluginContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(contextFactory);

        var loader = new PluginLoader(manifest, registry, contextFactory);
        await loader.LoadSourceAsync(source).ConfigureAwait(false);
        return loader;
    }

    /// <summary>热重载：加载新版本（新 ALC + 新 runtime）→ 旧版本 quiesce + ALC.Unload（02 §7）。</summary>
    public async Task ReloadAsync(PluginSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var oldRuntime = _runtime;
        var oldAlc = _alc;
        await LoadSourceAsync(source).ConfigureAwait(false);

        if (oldRuntime is not null)
        {
            await oldRuntime.StopAsync().ConfigureAwait(false); // quiesce 1-4 步（effect 收敛 + 插件 dispose + 摘注册）
        }

        _unloadedAlc = new WeakReference(oldAlc);
        oldAlc.Unload(); // 第 ⑤ 步：收敛后才 Unload
    }

    public async ValueTask DisposeAsync()
    {
        if (_alc is null)
        {
            return; // 幂等（await using 二次调用安全）
        }

        if (_runtime is not null)
        {
            await _runtime.StopAsync().ConfigureAwait(false);
        }

        _unloadedAlc ??= new WeakReference(_alc);
        _alc.Unload();
        _alc = null!; // 释放强引用，允许 GC 回收（否则 loader 字段引用阻塞卸载）
    }

    /// <summary>读取插件程序集内 SamplePlugin.Version 常量（反射，加载层允许）。</summary>
    public string GetPluginVersion()
    {
        foreach (var assembly in _alc.Assemblies)
        {
            var type = assembly.GetType("SamplePlugin");
            if (type is null)
            {
                continue;
            }

            return (string?)type.GetField("Version")?.GetValue(null) ?? "unknown";
        }

        return "unknown";
    }

    private async Task LoadSourceAsync(PluginSource source)
    {
        var pe = RoslynCompiler.Compile(source.Id, source.Code, RoslynCompiler.CreateDefaultReferences());

        var alc = new PluginAssemblyLoadContext(source.Id);
        var assembly = alc.LoadPlugin(pe);
        var pluginType = assembly.GetType("SamplePlugin")
            ?? assembly.GetTypes().FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t))
            ?? throw new KeystoneException(
                ErrorCode.LifecycleLoadFailed,
                $"plugin '{source.Id}' does not expose an IPlugin implementation");

        var plugin = (IPlugin?)Activator.CreateInstance(pluginType)
            ?? throw new KeystoneException(ErrorCode.LifecycleLoadFailed, $"plugin '{source.Id}' could not be instantiated");

        _alc = alc;
        _runtime = new PluginRuntime(_manifest, _ => plugin, _registry, _contextFactory);
        await _runtime.StartAsync().ConfigureAwait(false);
    }
}
