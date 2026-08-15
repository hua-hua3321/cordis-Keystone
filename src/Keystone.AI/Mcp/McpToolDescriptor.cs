using System.Text.Json;

namespace Keystone.AI.Mcp;

/// <summary>
/// MCP 工具描述（协议中立契约，ADR-0008 决策 4 隔离面）。
/// 不引用任何 MCP SDK 类型——协议层实现（ModelContextProtocol / 未来 MAF）由桥内部映射。
/// </summary>
public sealed record McpToolDescriptor
{
    /// <summary>工具名（跨语言生态的调用键）。</summary>
    public required string Name { get; init; }

    /// <summary>人类可读描述（供 AI 模型理解用途）。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>入参 JSON Schema（MCP 工具契约的 schema 形态）。</summary>
    public JsonElement? InputSchema { get; init; }
}
