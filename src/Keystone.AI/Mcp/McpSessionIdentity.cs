namespace Keystone.AI.Mcp;

/// <summary>
/// MCP 会话身份（协议中立契约）。映射到 SDK 的 Implementation（Name/Version）。
/// </summary>
public sealed record McpSessionIdentity
{
    /// <summary>实现名称（客户端/服务端标识）。</summary>
    public required string Name { get; init; }

    /// <summary>实现版本。</summary>
    public string Version { get; init; } = "1.0.0";
}
