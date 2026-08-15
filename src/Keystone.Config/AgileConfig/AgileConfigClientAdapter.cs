using AgileConfig.Client;

namespace Keystone.Config.AgileConfig;

/// <summary>
/// Default adapter over the official <c>AgileConfig.Client</c> package (version 1.9.1 API:
/// <see cref="ConfigClient"/> with <see cref="ConfigClientOptions"/>, <c>Data</c> dictionary,
/// <c>ReLoaded</c> push event, <c>ConnectAsync</c>).
/// </summary>
public sealed class AgileConfigClientAdapter : IAgileConfigClient
{
    private readonly ConfigClient _client;

    public AgileConfigClientAdapter(AgileConfigOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.AppId)
            || string.IsNullOrWhiteSpace(options.Secret)
            || string.IsNullOrWhiteSpace(options.Nodes))
        {
            throw new ArgumentException("AgileConfig 客户端需要 AppId、Secret、Nodes 均已配置。", nameof(options));
        }

        var clientOptions = new ConfigClientOptions
        {
            AppId = options.AppId,
            Secret = options.Secret,
            Nodes = options.Nodes,
            ENV = options.Env,
            CacheEnabled = true,
        };
        if (!string.IsNullOrWhiteSpace(options.CacheDirectory))
        {
            clientOptions.CacheDirectory = options.CacheDirectory;
        }

        _client = new ConfigClient(clientOptions);
        _client.ReLoaded += _ => ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool IsInitialized => _client.Status == ConnectStatus.Connected;

    public event EventHandler? ConfigChanged;

    public string? GetValue(string key) => _client.Get(key);

    public IReadOnlyDictionary<string, string> GetAll() => _client.Data;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _client.ConnectAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public void Dispose()
    {
        // AgileConfig.Client 的 ConfigClient 不实现 IDisposable（1.9.1 API）；连接由 DisconnectAsync 结束。
        // 此处保留空实现以满足 IAgileConfigClient 契约，自定义客户端可在此释放资源。
    }
}
