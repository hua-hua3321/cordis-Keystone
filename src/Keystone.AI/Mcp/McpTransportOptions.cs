namespace Keystone.AI.Mcp;

/// <summary>
/// MCP 传输配置（协议中立契约，ADR-0008 决策 4 隔离面）。
/// 桥实现内部据此构建具体 SDK 传输对象；调用方不接触任何 SDK 传输类型。
/// </summary>
public sealed record McpTransportOptions
{
    /// <summary>传输方式。</summary>
    public McpTransportKind Kind { get; init; } = McpTransportKind.Stream;

    /// <summary>Stream 模式：client 侧写入流（发往 server）。</summary>
    public Stream? ClientWriteStream { get; init; }

    /// <summary>Stream 模式：client 侧读取流（来自 server）。</summary>
    public Stream? ClientReadStream { get; init; }

    /// <summary>Stream 模式：server 侧读取流（来自 client）。</summary>
    public Stream? ServerReadStream { get; init; }

    /// <summary>Stream 模式：server 侧写入流（发往 client）。</summary>
    public Stream? ServerWriteStream { get; init; }

    /// <summary>Stdio 模式：外部 MCP 进程命令。</summary>
    public string? Command { get; init; }

    /// <summary>Stdio 模式：进程参数。</summary>
    public IReadOnlyList<string>? Arguments { get; init; }

    /// <summary>Http 模式：远端端点。</summary>
    public Uri? Endpoint { get; init; }
}
