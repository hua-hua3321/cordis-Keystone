using Keystone.Config.Entries;
using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.Hosting;

/// <summary>宿主选项：条目 → manifest/源码 提供者（插件定位接线）。</summary>
public sealed class KeystoneHostOptions
{
    /// <summary>条目 → manifest（provides/inject 服务声明）。</summary>
    public Func<EntryOptions, PluginManifest> ManifestProvider { get; set; } =
        _ => throw new InvalidOperationException("ManifestProvider is not configured");

    /// <summary>条目 → 插件源码（编译进 ALC）。</summary>
    public Func<EntryOptions, PluginSource> SourceProvider { get; set; } =
        _ => throw new InvalidOperationException("SourceProvider is not configured");
}
