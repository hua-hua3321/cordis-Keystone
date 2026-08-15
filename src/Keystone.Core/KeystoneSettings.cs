using Microsoft.Extensions.Configuration;

namespace Keystone.Core;

/// <summary>
/// Framework settings resolved from the <c>keystone</c> configuration section.
///
/// 编码纪律（禁止硬编码）：运行期可调值一律从这里取——配置缺失时用本类型内文档化的默认值兜底，
/// 配置存在时以配置为准（见 ADR-0013）。业务代码不得内嵌魔法值。
/// </summary>
public sealed record KeystoneSettings
{
    /// <summary>Configuration section name that carries framework settings.</summary>
    public const string SectionName = "keystone";

    /// <summary>Directory (relative to the host working directory) that holds plugin assemblies.</summary>
    public string PluginDirectory { get; init; } = "plugins";

    /// <summary>How long a dependency-gated plugin waits for its declared services before failing.</summary>
    public TimeSpan DependencyWaitTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>How long a quiesce (graceful unload) waits for in-flight work before forcing disposal.</summary>
    public TimeSpan QuiesceTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Default concurrency for capability-domain pipelines (serial = 1).</summary>
    public int DefaultConcurrency { get; init; } = 1;

    /// <summary>Default log level category filter, e.g. "Information".</summary>
    public string LogLevel { get; init; } = "Information";

    /// <summary>
    /// Bind framework settings from the <c>keystone</c> section. Missing section yields defaults;
    /// malformed values throw (fail-fast at startup). Manual binding keeps the host AOT-safe
    /// (doc 00 T9, rule 0 §5); the configuration-binder source generator is a later option.
    /// </summary>
    public static KeystoneSettings Bind(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SectionName);
        if (!section.Exists())
        {
            return new KeystoneSettings();
        }

        return new KeystoneSettings
        {
            PluginDirectory = section[nameof(PluginDirectory)] ?? new KeystoneSettings().PluginDirectory,
            DependencyWaitTimeout = Parse(section, nameof(DependencyWaitTimeout), new KeystoneSettings().DependencyWaitTimeout),
            QuiesceTimeout = Parse(section, nameof(QuiesceTimeout), new KeystoneSettings().QuiesceTimeout),
            DefaultConcurrency = Parse(section, nameof(DefaultConcurrency), new KeystoneSettings().DefaultConcurrency),
            LogLevel = section[nameof(LogLevel)] ?? new KeystoneSettings().LogLevel,
        };
    }

    private static TimeSpan Parse(IConfigurationSection section, string key, TimeSpan fallback)
    {
        var raw = section[key];
        return raw is null ? fallback : TimeSpan.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int Parse(IConfigurationSection section, string key, int fallback)
    {
        var raw = section[key];
        return raw is null ? fallback : int.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
    }
}
