using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Keystone.AI.Mcp;

/// <summary>
/// SDK 传输构建（内部实现细节，不对外）：
/// 契约 <see cref="McpTransportOptions"/> → SDK 传输对象（IClientTransport/ITransport）。
/// 协议层升级时只改此处与两个桥的内部映射，公共契约不变。
/// </summary>
internal static class McpTransportFactory
{
    /// <summary>构建 client 侧 SDK 传输。</summary>
    public static IClientTransport CreateClientTransport(
        McpTransportOptions options,
        Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Kind switch
        {
            McpTransportKind.Stream => new StreamClientTransport(
                options.ClientWriteStream ?? throw new InvalidOperationException("Stream 传输需 ClientWriteStream"),
                options.ClientReadStream ?? throw new InvalidOperationException("Stream 传输需 ClientReadStream"),
                loggerFactory),
            McpTransportKind.Stdio => new StdioClientTransport(
                new StdioClientTransportOptions
                {
                    Command = options.Command ?? throw new InvalidOperationException("Stdio 传输需 Command"),
                    Arguments = options.Arguments?.ToArray(),
                },
                loggerFactory),
            McpTransportKind.Http => new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = options.Endpoint ?? throw new InvalidOperationException("Http 传输需 Endpoint"),
                },
                loggerFactory),
            _ => throw new InvalidOperationException($"不支持的传输方式: {options.Kind}"),
        };
    }

    /// <summary>构建 server 侧 SDK 传输。</summary>
    public static ITransport CreateServerTransport(
        McpTransportOptions options,
        Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Kind switch
        {
            McpTransportKind.Stream => new StreamServerTransport(
                options.ServerReadStream ?? throw new InvalidOperationException("Stream 传输需 ServerReadStream"),
                options.ServerWriteStream ?? throw new InvalidOperationException("Stream 传输需 ServerWriteStream"),
                "keystone-mcp-server",
                loggerFactory),
            _ => throw new InvalidOperationException($"不支持的 server 传输方式: {options.Kind}"),
        };
    }
}
