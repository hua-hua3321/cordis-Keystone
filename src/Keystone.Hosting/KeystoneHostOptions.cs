using Keystone.Config.Entries;
using Keystone.Config.Validation;
using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.Hosting;

/// <summary>宿主选项：条目 → manifest/源码/配置 schema 提供者（插件定位接线）。</summary>
public sealed class KeystoneHostOptions
{
    /// <summary>条目 → manifest（provides/inject 服务声明）。</summary>
    public Func<EntryOptions, PluginManifest> ManifestProvider { get; set; } =
        _ => throw new InvalidOperationException("ManifestProvider is not configured");

    /// <summary>条目 → 插件源码（编译进 ALC）。</summary>
    public Func<EntryOptions, PluginSource> SourceProvider { get; set; } =
        _ => throw new InvalidOperationException("SourceProvider is not configured");

    /// <summary>
    /// 条目 → 配置 schema（G-C1 配置注入，16-cordis-gap-review）：
    /// 返回 null = 该插件无 schema 声明，原始 config 直传（不校验）。
    /// 非 null = 经 ConfigResolver 校验（必填/未知字段 fail-fast）+ 默认值补齐后注入 InitializeAsync。
    /// </summary>
    public Func<EntryOptions, ConfigSchema?> ConfigSchemaProvider { get; set; } = _ => null;

    /// <summary>配置解析过滤器链（M3 管线，可否决；空 = 无过滤器）。</summary>
    public IReadOnlyList<IConfigFilter> ConfigFilters { get; set; } = [];

    /// <summary>
    /// 环境变量提供者（DC-8，ADR-0012）：<c>!!env NAME</c> tag 静态插值。
    /// null（默认）= 不展开环境变量；缺失的环境变量保留 <c>!!env NAME</c> 标记。
    /// </summary>
    public Func<string, string?>? EnvProvider { get; set; }

    /// <summary>
    /// 文件内容提供者（DC-8，ADR-0012）：<c>!!file path</c> tag 静态插值（内容递归插值 + 环检测）。
    /// null（默认）= 不展开文件引用；缺失的文件保留 <c>!!file path</c> 标记。
    /// </summary>
    public Func<string, string?>? FileProvider { get; set; }

    /// <summary>
    /// 事实事件存储（DC-11，ADR-0009）：根 context 总线携带——任务完成/失败 + 插件生命周期事实
    /// 写入 append-only 事件日志；null（默认）= 不持久化。
    /// </summary>
    public Keystone.Runtime.Persistence.IEventStore? EventStore { get; set; }

    /// <summary>是否启用能力域（01 §2 管理层职责；默认开启，纯生命周期宿主可关闭）。</summary>
    public bool EnableCapabilityDomain { get; set; } = true;

    /// <summary>能力域名称（默认 "keystone"；多宿主嵌入场景可区分）。</summary>
    public string CapabilityDomainName { get; set; } = "keystone";

    /// <summary>
    /// 日志工厂（DC-20，05 §5）：注入根 context——插件 logger 経此可见（category = {能力域}/{插件 ID}）；
    /// null（默认）= NullLogger（原行为）。可接 RingBufferLoggerProvider/Console/自定义 provider。
    /// </summary>
    public Microsoft.Extensions.Logging.ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>全局关闭超时（09 §4 第 6 步：超时强制退出 + 记录未收敛插件；默认 30s）。</summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
