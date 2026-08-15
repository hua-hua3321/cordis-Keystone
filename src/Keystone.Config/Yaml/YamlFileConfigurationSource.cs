using Microsoft.Extensions.Configuration;

namespace Keystone.Config.Yaml;

/// <summary>
/// Configuration source backed by a YAML file (ADR-0013 default local provider).
/// Values are flattened into the Microsoft.Extensions.Configuration key space
/// (nested mappings and sequences use the standard ':' delimiter).
/// </summary>
public sealed class YamlFileConfigurationSource : IConfigurationSource
{
    /// <summary>YAML file path, resolved relative to the current directory.</summary>
    public required string Path { get; init; }

    /// <summary>When the file is missing, load an empty configuration instead of throwing.</summary>
    public bool Optional { get; init; } = true;

    /// <summary>Watch the file and reload (debounced) when it changes on disk.</summary>
    public bool ReloadOnChange { get; init; } = true;

    /// <summary>Debounce window for file-change events (doc 08 §6.3 write-back debounce semantics).</summary>
    public TimeSpan ReloadDelay { get; init; } = TimeSpan.FromMilliseconds(250);

    public IConfigurationProvider Build(IConfigurationBuilder builder) => new YamlFileConfigurationProvider(this);
}
