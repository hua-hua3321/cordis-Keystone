namespace Keystone.AI.Mcp;

/// <summary>
/// MCP 传输方式（协议中立契约）。桥实现内部据此构建 SDK 传输（stdio/http/stream）。
/// </summary>
public enum McpTransportKind
{
    /// <summary>内存流对接（in-process 双端 / 测试 / 管道）。</summary>
    Stream,

    /// <summary>标准输入输出传输（外部 MCP 进程，CLI 工具生态）。</summary>
    Stdio,

    /// <summary>Streamable HTTP 传输（远程 MCP server）。</summary>
    Http,
}
