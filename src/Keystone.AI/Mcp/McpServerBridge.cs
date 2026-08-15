using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Keystone.AI.Mcp;

/// <summary>
/// MCP server 桥（ADR-0008 决策 4 落地：协议层组合官方稳定 SDK ModelContextProtocol.Core）：
/// 薄封装 <see cref="McpServer"/>——注册工具 + 运行会话，不重造协议。
/// 传输由调用方注入（stdio/http/stream），本桥只承载工具注册与生命周期。
/// </summary>
public sealed class McpServerBridge : IAsyncDisposable
{
    private readonly McpServer _server;
    private readonly McpServerOptions _options;

    private McpServerBridge(McpServer server, McpServerOptions options)
    {
        _server = server;
        _options = options;
    }

    /// <summary>创建 server（会话传输就绪后）。</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000",
        Justification = "McpServer 所有权转移给 McpServerBridge，由本桥 DisposeAsync 统一释放")]
    public static McpServerBridge Create(
        ITransport transport,
        McpServerOptions options,
        Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = null,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(options);
        options.ToolCollection ??= new McpServerPrimitiveCollection<McpServerTool>(StringComparer.Ordinal);
        var server = McpServer.Create(transport, options, loggerFactory, services);
        return new McpServerBridge(server, options);
    }

    /// <summary>注册一个能力域工具（typed AIFunction 形态，MCP 工具市场对外面）。</summary>
    public void AddTool(McpServerTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        _options.ToolCollection!.Add(tool);
    }

    /// <summary>运行会话直到取消/断开（stdio 场景由宿主进程生命周期驱动）。</summary>
    public Task RunAsync(CancellationToken cancellationToken = default)
        => _server.RunAsync(cancellationToken);

    public ValueTask DisposeAsync() => _server.DisposeAsync();
}
