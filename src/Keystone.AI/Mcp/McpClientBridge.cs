using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Keystone.AI.Mcp;

/// <summary>
/// MCP client 桥实现（协议层 = ModelContextProtocol.Core 2.2.0，ADR-0008 决策 4）。
/// 公共面只暴露 <see cref="IMcpClientBridge"/> 契约与 Keystone 协议中立类型；SDK 类型
/// （McpClient/McpClientTool/CallToolResult/IClientTransport/McpClientOptions）全部内聚在本类——
/// 协议层升级或未来 MAF agent 集成层接入，调用方零改动。
/// </summary>
public sealed class McpClientBridge : IMcpClientBridge
{
    private readonly McpClient _client;
    private readonly IClientTransport _transport;

    private McpClientBridge(McpClient client, IClientTransport transport)
    {
        _client = client;
        _transport = transport;
    }

    /// <summary>
    /// 建立会话（握手 + capability 协商，SDK 内部）。
    /// 传输从 <paramref name="transport"/> 契约构建并随本桥存活；会话 ITransport 由 McpClient 释放
    /// （SDK 源码：McpClientImpl.DisposeAsync → _transport.DisposeAsync），工厂本身若实现
    /// IAsyncDisposable（如 HttpClientTransport）由本桥释放。
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000",
        Justification = "sdkTransport/McpClient 所有权转移给 McpClientBridge，由本桥 DisposeAsync 统一释放")]
    public static async Task<IMcpClientBridge> ConnectAsync(
        McpTransportOptions transport,
        McpClientOptions? options = null,
        Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);

        var sdkTransport = McpTransportFactory.CreateClientTransport(transport, loggerFactory);
        var sdkOptions = ToSdkOptions(options);
        var client = await McpClient.CreateAsync(sdkTransport, sdkOptions, loggerFactory, cancellationToken).ConfigureAwait(false);
        return new McpClientBridge(client, sdkTransport);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = await _client.ListToolsAsync(new RequestOptions(), cancellationToken).ConfigureAwait(false);
        return tools
            .Select(t => new McpToolDescriptor
            {
                Name = t.Name,
                Description = t.Description ?? string.Empty,
                InputSchema = t.JsonSchema,
            })
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<McpToolCallResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var result = await _client
            .CallToolAsync(toolName, arguments, progress: null, options: new RequestOptions(), cancellationToken)
            .ConfigureAwait(false);

        return new McpToolCallResult
        {
            IsError = result.IsError == true,
            TextContents = result.Content.OfType<TextContentBlock>().Select(b => b.Text).ToArray(),
            StructuredContent = result.StructuredContent,
        };
    }

    /// <inheritdoc />
    public async Task PingAsync(CancellationToken cancellationToken = default)
        => await _client.PingAsync(new RequestOptions(), cancellationToken).ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        // 顺序：先释放 client（其内部释放会话 ITransport），再释放传输工厂（若有独立资源）
        await _client.DisposeAsync().ConfigureAwait(false);
        if (_transport is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static ModelContextProtocol.Client.McpClientOptions? ToSdkOptions(McpClientOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        var sdk = new ModelContextProtocol.Client.McpClientOptions();
        if (options.ClientInfo is not null)
        {
            sdk.ClientInfo = new Implementation { Name = options.ClientInfo.Name, Version = options.ClientInfo.Version };
        }

        if (options.ProtocolVersion is not null)
        {
            sdk.ProtocolVersion = options.ProtocolVersion;
        }

        return sdk;
    }
}
