namespace Keystone.Config.AgileConfig;

/// <summary>Options for the AgileConfig configuration-center provider (ADR-0013 default remote source).</summary>
public sealed class AgileConfigOptions
{
    /// <summary>AgileConfig application id.</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>AgileConfig application secret.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Comma-separated AgileConfig server node addresses.</summary>
    public string Nodes { get; set; } = string.Empty;

    /// <summary>Environment name (DEV / TEST / STAGING / PROD).</summary>
    public string Env { get; set; } = "DEV";

    /// <summary>
    /// When true (default) and the AgileConfig client cannot be configured (missing AppId/Secret/Nodes)
    /// or cannot connect, the provider loads an empty configuration instead of failing startup.
    /// </summary>
    public bool Optional { get; set; } = true;

    /// <summary>Local cache directory; empty uses the AgileConfig client default.</summary>
    public string CacheDirectory { get; set; } = string.Empty;
}
