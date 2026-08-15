namespace Keystone.AI.Mcp;

/// <summary>
/// MCP server 工具定义（协议中立契约，ADR-0008 决策 4 隔离面）。
/// Handler 为业务委托——SDK 从委托签名推导 JSON Schema（typed AIFunction 形态）。
/// </summary>
public sealed record McpToolDefinition
{
    /// <summary>工具名（MCP 工具市场调用键，需符合 MCP 命名规则）。</summary>
    public required string Name { get; init; }

    /// <summary>人类可读描述。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>业务处理委托（参数即工具入参，返回值即工具结果）。</summary>
    public required Delegate Handler { get; init; }
}
