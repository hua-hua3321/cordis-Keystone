using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Keystone.AI.Mcp;

/// <summary>
/// MCP client 桥（ADR-0008 决策 4 落地：协议层组合官方稳定 SDK ModelContextProtocol.Core）：
/// 薄封装 <see cref="McpClient"/>——连接/枚举工具/调用工具/存活探测，不重造协议。
/// 传输由调用方注入（stdio/http/stream），本桥只承载生命周期与统一入口。
/// </summary>
public sealed class McpClientBridge : IAsyncDisposable
{
    private readonly McpClient _client;

    private McpClientBridge(McpClient client) => _client = client;

    /// <summary>建立会话（握手 + capability 协商，SDK 内部）。</summary>
    public static async Task<McpClientBridge> ConnectAsync(
        IClientTransport transport,
        McpClientOptions? options = null,
        Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        var client = await McpClient.CreateAsync(transport, options, loggerFactory, cancellationToken).ConfigureAwait(false);
        return new McpClientBridge(client);
    }

    /// <summary>枚举远端可用工具（MCP 工具市场，跨语言生态）。</summary>
    public async Task<IReadOnlyList<McpClientTool>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = await _client.ListToolsAsync(new RequestOptions(), cancellationToken).ConfigureAwait(false);
        return tools.ToArray();
    }

    /// <summary>调用远端工具（参数经 JSON-RPC 往返）。</summary>
    public async Task<CallToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
        => await _client.CallToolAsync(toolName, arguments, progress: null, options: new RequestOptions(), cancellationToken).ConfigureAwait(false);

    /// <summary>存活探测（协议层 ping）。</summary>
    public async Task PingAsync(CancellationToken cancellationToken = default)
        => await _client.PingAsync(new RequestOptions(), cancellationToken).ConfigureAwait(false);

    public ValueTask DisposeAsync() => _client.DisposeAsync();
}
