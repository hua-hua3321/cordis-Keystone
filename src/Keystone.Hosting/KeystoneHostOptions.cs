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

    /// <summary>是否启用能力域（01 §2 管理层职责；默认开启，纯生命周期宿主可关闭）。</summary>
    public bool EnableCapabilityDomain { get; set; } = true;

    /// <summary>能力域名称（默认 "keystone"；多宿主嵌入场景可区分）。</summary>
    public string CapabilityDomainName { get; set; } = "keystone";
}
