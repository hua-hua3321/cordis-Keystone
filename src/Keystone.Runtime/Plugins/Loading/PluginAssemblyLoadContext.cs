using System.Reflection;
using System.Runtime.Loader;

namespace Keystone.Runtime.Plugins.Loading;

/// <summary>
/// 插件私有 ALC（可卸载，isCollectible: true）。Resolving fallback 到默认 ALC
/// （02 §5 清单 #2：插件解析不到宿主/共享程序集 → 复用默认 ALC 已加载实例）。
/// </summary>
public sealed class PluginAssemblyLoadContext : AssemblyLoadContext
{
    public PluginAssemblyLoadContext(string name)
        : base(name, isCollectible: true)
    {
        Resolving += (_, assemblyName) =>
        {
            // 宿主/共享程序集已在默认 ALC 加载 → fallback（避免插件各自副本）
            return Default.Assemblies.FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.Ordinal));
        };
    }

    /// <summary>从 PE 字节加载程序集（02 §4 步骤 2）。</summary>
    public Assembly LoadPlugin(byte[] pe)
    {
        ArgumentNullException.ThrowIfNull(pe);
        return LoadFromStream(new MemoryStream(pe));
    }
}
