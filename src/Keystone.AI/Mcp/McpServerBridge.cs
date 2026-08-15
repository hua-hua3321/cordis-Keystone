using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Keystone.AI.Mcp;

/// <summary>
/// MCP server 桥实现（协议层 = ModelContextProtocol.Core 2.2.0，ADR-0008 决策 4）。
/// 公共面只暴露 <see cref="IMcpServerBridge"/> 契约与 Keystone 协议中立类型；SDK 类型
/// （McpServer/McpServerTool/ITransport/McpServerOptions）全部内聚在本类——
/// 协议层升级或未来 MAF agent 集成层接入，调用方零改动。
/// </summary>
public sealed class McpServerBridge : IMcpServerBridge
{
    private readonly ModelContextProtocol.Server.McpServer _server;
    private readonly ModelContextProtocol.Server.McpServerOptions _options;
    private readonly ITransport _transport;

    private McpServerBridge(ModelContextProtocol.Server.McpServer server, ModelContextProtocol.Server.McpServerOptions options, ITransport transport)
    {
        _server = server;
        _options = options;
        _transport = transport;
    }

    /// <summary>
    /// 创建 server（会话传输就绪后）。传输从 <paramref name="transport"/> 契约构建并随本桥存活；
    /// 会话 ITransport 由本桥释放（SDK 源码：McpServerImpl 不释放 _sessionTransport，所有权在调用方）。
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000",
        Justification = "sdkTransport/McpServer 所有权转移给 McpServerBridge，由本桥 DisposeAsync 统一释放")]
    public static IMcpServerBridge Create(
        McpTransportOptions transport,
        Keystone.AI.Mcp.McpServerOptions? options = null,
        Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = null,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(transport);

        var sdkTransport = McpTransportFactory.CreateServerTransport(transport, loggerFactory);
        var sdkOptions = ToSdkOptions(options);
        sdkOptions.ToolCollection ??= new McpServerPrimitiveCollection<McpServerTool>(StringComparer.Ordinal);
        var server = McpServer.Create(sdkTransport, sdkOptions, loggerFactory, services);
        return new McpServerBridge(server, sdkOptions, sdkTransport);
    }

    /// <inheritdoc />
    public void AddTool(McpToolDefinition tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (tool.Handler is null)
        {
            throw new ArgumentException("工具处理器不可为空", nameof(tool));
        }

        var sdkTool = McpServerTool.Create(
            tool.Handler,
            new McpServerToolCreateOptions { Name = tool.Name, Description = tool.Description });
        _options.ToolCollection!.Add(sdkTool);
    }

    /// <inheritdoc />
    public Task RunAsync(CancellationToken cancellationToken = default)
        => _server.RunAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        // 顺序：先释放 server，再释放会话 ITransport（McpServer 不释放它，所有权在本桥）
        await _server.DisposeAsync().ConfigureAwait(false);
        await _transport.DisposeAsync().ConfigureAwait(false);
    }

    private static ModelContextProtocol.Server.McpServerOptions ToSdkOptions(Keystone.AI.Mcp.McpServerOptions? options)
    {
        var sdk = new ModelContextProtocol.Server.McpServerOptions();
        if (options is null)
        {
            return sdk;
        }

        if (options.ServerInfo is not null)
        {
            sdk.ServerInfo = new Implementation { Name = options.ServerInfo.Name, Version = options.ServerInfo.Version };
        }

        if (options.ProtocolVersion is not null)
        {
            sdk.ProtocolVersion = options.ProtocolVersion;
        }

        if (options.ServerInstructions is not null)
        {
            sdk.ServerInstructions = options.ServerInstructions;
        }

        return sdk;
    }
}
