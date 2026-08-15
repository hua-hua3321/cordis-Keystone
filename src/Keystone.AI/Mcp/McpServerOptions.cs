namespace Keystone.AI.Mcp;

/// <summary>
/// MCP server 会话选项（协议中立契约，ADR-0008 决策 4 隔离面）。
/// 仅暴露调用方关心的常用项；capability/资源/提示词等进阶项由实现层 SDK 默认。
/// </summary>
public sealed record McpServerOptions
{
    /// <summary>服务端身份（MCP 握手中声明）。</summary>
    public McpSessionIdentity? ServerInfo { get; init; }

    /// <summary>协议版本（日期制，如 "2025-11-25"；默认由 SDK 协商最新）。</summary>
    public string? ProtocolVersion { get; init; }

    /// <summary>服务端指令（可选，向客户端说明服务用途）。</summary>
    public string? ServerInstructions { get; init; }
}
