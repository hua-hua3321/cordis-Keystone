using Microsoft.Extensions.Configuration;

namespace Keystone.Config.AgileConfig;

/// <summary>
/// Configuration provider that mirrors an AgileConfig client snapshot into the configuration
/// key space and reloads when the center pushes changes (websocket push → <c>ConfigChanged</c> →
/// <see cref="Load"/> → OnReload).
///
/// 失败语义：拉取/连接失败且非 optional 时抛错（启动 fail-fast）；optional 或已加载后推送失败时
/// 保持旧数据（"最后好数据保持"，对齐 doc 08 §6.3 事务刷新语义）。
/// </summary>
public sealed class AgileConfigConfigurationProvider : ConfigurationProvider
{
    private readonly IAgileConfigClient _client;
    private readonly bool _optional;
    private bool _loaded;

    public AgileConfigConfigurationProvider(IAgileConfigClient client, bool optional)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _optional = optional;
        _client.ConfigChanged += (_, _) => ReloadBestEffort();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031",
        Justification = "optional 且未成功加载时配置中心失败 = 空配置继续启动（fail-open，见类注释）")]
    public override void Load()
    {
        try
        {
            if (!_client.IsInitialized)
            {
                // P0 启动期同步拉取：M.E.C 提供者契约是同步的；配置中心首拉为 HTTP 快照 + websocket 连接。
                // 后续变更走 ConfigChanged 推送，不再阻塞。
                _client.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
            }

            Data = LoadSnapshot();
            _loaded = true;
            OnReload();
        }
        catch (Exception) when (_optional && !_loaded)
        {
            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            OnReload();
        }
    }

    private Dictionary<string, string?> LoadSnapshot()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in _client.GetAll())
        {
            data[key] = value;
        }

        return data;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031",
        Justification = "配置中心推送后拉取失败 = 保持旧数据（'最后好数据保持'，见类注释）")]
    private void ReloadBestEffort()
    {
        try
        {
            Load();
        }
        catch (Exception)
        {
            // 推送后拉取失败：保持旧数据（_loaded 已为 true，optional 分支不再吞错路径生效）
        }
    }
}
