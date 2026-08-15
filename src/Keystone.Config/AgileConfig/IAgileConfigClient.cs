namespace Keystone.Config.AgileConfig;

/// <summary>
/// Thin abstraction over the AgileConfig client so the provider logic is testable and
/// swappable (users may supply their own implementation for other configuration centers).
/// </summary>
public interface IAgileConfigClient : IDisposable
{
    /// <summary>True after a successful connect (websocket up, config pulled).</summary>
    bool IsInitialized { get; }

    /// <summary>Read a single value by key; null when absent.</summary>
    string? GetValue(string key);

    /// <summary>Full key/value snapshot currently held by the client.</summary>
    IReadOnlyDictionary<string, string> GetAll();

    /// <summary>Raised when the configuration center pushes a change (any key).</summary>
    event EventHandler? ConfigChanged;

    /// <summary>Connect and pull the initial configuration snapshot.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);
}
