namespace Keystone.AI.Mcp;

/// <summary>
/// MCP client 桥契约（ADR-0008 决策 4 隔离面）。
/// 签名只使用 Keystone 协议中立契约类型——实现可换（ModelContextProtocol / 未来 MAF Mcp），调用方零改动。
/// </summary>
public interface IMcpClientBridge : IAsyncDisposable
{
    /// <summary>枚举远端可用工具（MCP 工具市场，跨语言生态）。</summary>
    Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>调用远端工具（参数经 JSON-RPC 往返）。</summary>
    Task<McpToolCallResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default);

    /// <summary>存活探测（协议层 ping；最新协议版本可能不支持，见实现）。</summary>
    Task PingAsync(CancellationToken cancellationToken = default);
}
