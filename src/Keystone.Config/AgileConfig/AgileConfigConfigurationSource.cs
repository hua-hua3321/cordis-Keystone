using Microsoft.Extensions.Configuration;

namespace Keystone.Config.AgileConfig;

/// <summary>
/// Configuration source backed by an AgileConfig configuration center (ADR-0013 default remote source).
/// </summary>
public sealed class AgileConfigConfigurationSource : IConfigurationSource
{
    /// <summary>Client used to read and subscribe to the configuration center.</summary>
    public required IAgileConfigClient Client { get; init; }

    /// <summary>When true, connect/load failures produce an empty configuration instead of throwing.</summary>
    public bool Optional { get; init; } = true;

    public IConfigurationProvider Build(IConfigurationBuilder builder) => new AgileConfigConfigurationProvider(Client, Optional);
}
