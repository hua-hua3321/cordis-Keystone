using System.IO.Pipelines;
using Keystone.AI.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Keystone.AI.Tests;

/// <summary>
/// MCP 双端适配层测试（ADR-0008 决策 4 落地：MAF Mcp 未稳定，协议层组合官方稳定 SDK ModelContextProtocol.Core）。
/// in-process 双端：StreamServerTransport + StreamClientTransport 经 Pipe 内存流对接，无外部进程/网络。
/// </summary>
public class McpBridgeTests
{
    private static readonly Implementation ServerInfo = new() { Name = "keystone-test", Version = "1.0.0" };
    private static readonly Implementation ClientInfo = new() { Name = "keystone-client", Version = "1.0.0" };

    private static (McpServerBridge Server, McpClientBridge Client, Task ServerRun) ConnectAsync(
        CancellationToken ct,
        string? protocolVersion = null)
    {
        // 接线：c2s（client→server）+ s2c（server→client）两条 Pipe
        var c2s = new Pipe();
        var s2c = new Pipe();
        var loggerFactory = NullLoggerFactory.Instance;

        var serverTransport = new StreamServerTransport(c2s.Reader.AsStream(), s2c.Writer.AsStream(), "test-server", loggerFactory);
        var clientTransport = new StreamClientTransport(c2s.Writer.AsStream(), s2c.Reader.AsStream(), loggerFactory);

        var serverOptions = new McpServerOptions { ServerInfo = ServerInfo };
        if (protocolVersion is not null)
        {
            serverOptions.ProtocolVersion = protocolVersion;
        }

        var server = McpServerBridge.Create(serverTransport, serverOptions, loggerFactory, services: null);
        var serverRun = server.RunAsync(ct);
        var clientOptions = new McpClientOptions { ClientInfo = ClientInfo };
        if (protocolVersion is not null)
        {
            clientOptions.ProtocolVersion = protocolVersion;
        }

        var client = McpClientBridge.ConnectAsync(clientTransport, clientOptions, loggerFactory, ct).GetAwaiter().GetResult();
        return (server, client, serverRun);
    }

    [Fact]
    public async Task Client_discovers_and_calls_tool_exposed_by_server()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var (server, client, serverRun) = ConnectAsync(cts.Token);

        await using var _server = server;
        await using var _client = client;
        server.AddTool(McpServerTool.Create(
            (string message) => $"echo:{message}",
            new McpServerToolCreateOptions { Name = "echo", Description = "Echoes the input back" }));

        // 枚举：client 侧可见 server 暴露的工具（跨"语言生态"边界）
        var tools = await client.ListToolsAsync(cts.Token);
        var echo = Assert.Single(tools);
        Assert.Equal("echo", echo.Name);
        Assert.Contains("Echoes", echo.Description);

        // 调用：参数经 JSON-RPC 往返，结果带回
        var result = await client.CallToolAsync("echo", new Dictionary<string, object?> { ["message"] = "hello" }, cts.Token);
        Assert.NotEqual(true, result.IsError); // 未标记错误（bool?，null 或 false 均视为成功）
        var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text));
        Assert.Contains("echo:hello", text);

        await cts.CancelAsync();
        try { await serverRun; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task Client_can_enumerate_multiple_tools()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var (server, client, serverRun) = ConnectAsync(cts.Token);

        await using var _server = server;
        await using var _client = client;
        server.AddTool(McpServerTool.Create((string x) => x, new McpServerToolCreateOptions { Name = "alpha" }));
        server.AddTool(McpServerTool.Create((string x) => x, new McpServerToolCreateOptions { Name = "beta" }));

        var tools = await client.ListToolsAsync(cts.Token);

        Assert.Equal(2, tools.Count);
        Assert.Contains(tools, t => t.Name == "alpha");
        Assert.Contains(tools, t => t.Name == "beta");

        await cts.CancelAsync();
        try { await serverRun; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task Client_ping_works_on_legacy_protocol_versions()
    {
        // 协议演进：2026-07-28 移除 ping 方法（默认版本无 ping，discover/call 已验证）；
        // 旧协议版本（2025-11-25）仍支持 ping——桥方法在旧协议下可用。
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var (server, client, serverRun) = ConnectAsync(cts.Token, protocolVersion: "2025-11-25");

        await using var _server = server;
        await using var _client = client;

        await client.PingAsync(cts.Token); // 旧协议：ping 可用，不抛即会话活

        await cts.CancelAsync();
        try { await serverRun; } catch (OperationCanceledException) { }
    }
}
