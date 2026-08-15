namespace Keystone.AI.Mcp;

/// <summary>
/// MCP server 桥契约（ADR-0008 决策 4 隔离面）。
/// 签名只使用 Keystone 协议中立契约类型——实现可换，调用方零改动。
/// </summary>
public interface IMcpServerBridge : IAsyncDisposable
{
    /// <summary>注册一个能力域工具（对外暴露为 MCP 工具，供任何 MCP 客户端消费）。</summary>
    void AddTool(McpToolDefinition tool);

    /// <summary>运行会话直到取消/断开（stdio 场景由宿主进程生命周期驱动）。</summary>
    Task RunAsync(CancellationToken cancellationToken = default);
}
