using Microsoft.Extensions.Configuration;

namespace Keystone.Config;

/// <summary>
/// Fluent builder for the Keystone configuration composition (doc 08 §4 layering).
///
/// 默认组合（<see cref="CreateDefault"/>）：开发阶段仅本地 YAML（<c>keystone.yml</c>，
/// optional）。配置中心（AgileConfig）为**预留可选源**（ADR-0014：开发阶段不引入配置中心），
/// 需要时显式 AddAgileConfig 追加——M.E.C 后添加者优先（届时优先级
/// 配置中心 &gt; YAML &gt; 代码内默认值）。其他配置来源（用户自实现）通过
/// <see cref="ConfigurationBuilderExtensions"/> 或任意 <see cref="IConfigurationSource"/> 追加。
/// </summary>
public sealed class KeystoneConfigBuilder
{
    private readonly ConfigurationBuilder _builder = new();

    /// <summary>The underlying Microsoft.Extensions.Configuration builder for advanced composition.</summary>
    public IConfigurationBuilder Builder => _builder;

    /// <summary>Add a YAML file source (ADR-0013/0014 default local provider).</summary>
    public KeystoneConfigBuilder AddYamlFile(string path, bool optional = true, bool reloadOnChange = true)
    {
        _builder.AddYamlFile(path, optional, reloadOnChange);
        return this;
    }

    /// <summary>Add the AgileConfig configuration-center source（预留可选源，ADR-0014）。</summary>
    public KeystoneConfigBuilder AddAgileConfig(Action<AgileConfig.AgileConfigOptions>? configure = null)
    {
        _builder.AddAgileConfig(configure);
        return this;
    }

    /// <summary>Add the AgileConfig source with an explicit client (test seam / custom center adapter).</summary>
    public KeystoneConfigBuilder AddAgileConfig(AgileConfig.IAgileConfigClient client, bool optional = true)
    {
        _builder.Add(new AgileConfig.AgileConfigConfigurationSource { Client = client, Optional = optional });
        return this;
    }

    /// <summary>Build the composed configuration.</summary>
    public IConfiguration Build() => _builder.Build();

    /// <summary>
    /// Default composition (P0 开发阶段，ADR-0014)：仅可选本地 <c>keystone.yml</c>。
    /// 优先级：YAML &gt; 代码内文档化默认值。配置中心以后阶段按需 AddAgileConfig 启用。
    /// </summary>
    public static KeystoneConfigBuilder CreateDefault() => new KeystoneConfigBuilder()
        .AddYamlFile("keystone.yml");
}
