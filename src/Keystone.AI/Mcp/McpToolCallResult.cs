using System.Text.Json;

namespace Keystone.AI.Mcp;

/// <summary>
/// MCP 工具调用结果（协议中立契约，ADR-0008 决策 4 隔离面）。
/// 文本内容与结构化内容并列：MCP 边界 JSON-RPC 的语义映射由桥内部完成。
/// </summary>
public sealed record McpToolCallResult
{
    /// <summary>是否标记为错误（true = 工具执行失败）。</summary>
    public bool IsError { get; init; }

    /// <summary>文本内容块（多数工具结果的主要承载）。</summary>
    public IReadOnlyList<string> TextContents { get; init; } = [];

    /// <summary>结构化结果（可选，SDK 2.x 的 StructuredContent 透传）。</summary>
    public JsonElement? StructuredContent { get; init; }
}
