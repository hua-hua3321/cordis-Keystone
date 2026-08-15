namespace Keystone.AI.Mcp;

/// <summary>
/// MCP client 会话选项（协议中立契约，ADR-0008 决策 4 隔离面）。
/// 仅暴露调用方关心的常用项；capability/元数据等进阶项由实现层 SDK 默认（进阶定制可扩展本契约）。
/// </summary>
public sealed record McpClientOptions
{
    /// <summary>客户端身份（MCP 握手中声明）。</summary>
    public McpSessionIdentity? ClientInfo { get; init; }

    /// <summary>协议版本（日期制，如 "2025-11-25"；默认由 SDK 协商最新）。</summary>
    public string? ProtocolVersion { get; init; }
}
