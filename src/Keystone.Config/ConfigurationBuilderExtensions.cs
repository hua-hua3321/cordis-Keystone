using Microsoft.Extensions.Configuration;
using Keystone.Config.AgileConfig;
using Keystone.Config.Yaml;

namespace Keystone.Config;

/// <summary>
/// Extension methods that register the built-in configuration providers on any
/// <see cref="IConfigurationBuilder"/> — the extension point for custom providers:
/// implement <see cref="IConfigurationSource"/> + <c>IConfigurationProvider</c> and add a
/// matching <c>Add...()</c> extension (ADR-0013).
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>Register a YAML file source.</summary>
    public static IConfigurationBuilder AddYamlFile(
        this IConfigurationBuilder builder,
        string path,
        bool optional = true,
        bool reloadOnChange = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Add(new YamlFileConfigurationSource
        {
            Path = path,
            Optional = optional,
            ReloadOnChange = reloadOnChange,
        });
    }

    /// <summary>
    /// Register the AgileConfig configuration-center source（**预留可选源**，ADR-0014：开发阶段不进入
    /// 默认组合，后续阶段按需启用）。When the options are left unconfigured (empty AppId) and
    /// <see cref="AgileConfigOptions.Optional"/> is true, the source is skipped so startup does not
    /// depend on a configuration center.
    /// </summary>
    public static IConfigurationBuilder AddAgileConfig(
        this IConfigurationBuilder builder,
        Action<AgileConfigOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new AgileConfigOptions();
        configure?.Invoke(options);

        if (options.Optional && string.IsNullOrWhiteSpace(options.AppId))
        {
            return builder; // 未配置配置中心且 optional：跳过（不阻塞启动）
        }

        var client = new AgileConfigClientAdapter(options);
        return builder.Add(new AgileConfigConfigurationSource { Client = client, Optional = options.Optional });
    }
}
