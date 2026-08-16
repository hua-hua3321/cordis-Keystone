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
    /// 获取端抽象（DC-19，ADR-0001 决策 2）：配置后插件源码经 <c>IPluginSource.FetchAsync</c>
    /// 获取（本地文件/远程分发可替换，编译/ALC/dispose 管线不动）；优先于 SourceProvider 委托。
    /// </summary>
    public Keystone.Runtime.Plugins.Loading.IPluginSource? PluginSource { get; set; }

    /// <summary>
    /// 运行形态扩展点（DC-19，ADR-0001 决策 1）：**预留**——本期唯一形态同进程 ALC；
    /// 独立进程隔离（方案 B）未来经此引入。
    /// </summary>
    public Keystone.Runtime.Plugins.Loading.IPluginHost? PluginHost { get; set; }

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

    /// <summary>
    /// 配置落盘路径（DC-15，09 §5/08 §6.3）：设置后 CRUD 变更经 ConfigFileWriter
    /// 防抖写回（原子写 tmp+Move + 占用重试）；FlushConfigAsync 冲刷、Shutdown 排空；
    /// null（默认）= 纯内存（原行为）。
    /// </summary>
    public string? ConfigFilePath { get; set; }

    /// <summary>
    /// 事实保留策略（DC-18，ADR-0009 决策 3）：配置后宿主启动定时 Prune
    /// （周期 <see cref="PruneInterval"/>，随宿主启停；失败降级续跑）；
    /// null（默认）= 不自动清理（嵌入方手动 PruneAsync）。
    /// </summary>
    public Keystone.Runtime.Persistence.RetentionPolicy? RetentionPolicy { get; set; }

    /// <summary>定时 Prune 周期（默认 1 小时；仅 <see cref="RetentionPolicy"/> 配置时生效）。</summary>
    public TimeSpan PruneInterval { get; set; } = TimeSpan.FromHours(1);

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

    /// <summary>initial 引导条目（CA-6，P60，对齐 Cordis include initial）：
    /// <see cref="KeystoneHost.StartFromFileAsync"/> 时配置文件不存在 → 写入这些条目再启动；
    /// 文件已存在 → 忽略（现网配置优先）。null/空 = 无引导。</summary>
    public IReadOnlyList<EntryOptions>? InitialEntries { get; set; }

    /// <summary>运行期 patch（CA-5，P61，对齐 Cordis include Config.patches）：启动时解析后、
    /// manifest 校验前应用（插入组/根 + 按 id 覆盖）；注意与 PatchContext 瀑布（F9）语义不同。</summary>
    public IReadOnlyList<Keystone.Config.Entries.EntryPatch>? ConfigPatches { get; set; }

    /// <summary>服务级选项（CA-12，P60，intercept 对应物·宿主级一层）：服务名 → 选项字典。
    /// 日志首例："logger" → { defaultLevel, capacity, levels: {category→level} }——
    /// 未注入 LoggerFactory 时构造 RingBufferLoggerProvider（显式 LoggerFactory 优先，不覆盖嵌入方）。
    /// P2-19：levels 键 = 完整 category（含域前缀，如 "keystone/logp"）——裸名不命中（精确匹配）。</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? ServiceOptions { get; set; }
}
