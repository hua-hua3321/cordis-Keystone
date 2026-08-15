using System.IO.Pipelines;
using System.Reflection;
using Keystone.AI.Mcp;
using Microsoft.Extensions.Logging.Abstractions;

namespace Keystone.AI.Tests;

/// <summary>
/// MCP 双端适配层测试（ADR-0008 决策 4 落地：协议层组合官方稳定 SDK ModelContextProtocol.Core，
/// 公共面 = Keystone 协议中立契约）。
/// in-process 双端：Stream 传输经 Pipe 内存流对接，无外部进程/网络。
/// 本测试文件除隔离验证用例外，不引用任何 MCP SDK 类型——证明调用方只依赖契约。
/// </summary>
public class McpBridgeTests
{
    private static readonly McpSessionIdentity ServerInfo = new() { Name = "keystone-test", Version = "1.0.0" };
    private static readonly McpSessionIdentity ClientInfo = new() { Name = "keystone-client", Version = "1.0.0" };

    private static (IMcpServerBridge Server, IMcpClientBridge Client, Task ServerRun) ConnectAsync(
        CancellationToken ct,
        string? protocolVersion = null)
    {
        // 接线：c2s（client→server）+ s2c（server→client）两条 Pipe
        var c2s = new Pipe();
        var s2c = new Pipe();
        var loggerFactory = NullLoggerFactory.Instance;

        var serverOptions = new McpServerOptions { ServerInfo = ServerInfo };
        if (protocolVersion is not null)
        {
            serverOptions = serverOptions with { ProtocolVersion = protocolVersion };
        }

        var server = McpServerBridge.Create(
            new McpTransportOptions
            {
                Kind = McpTransportKind.Stream,
                ServerReadStream = c2s.Reader.AsStream(),
                ServerWriteStream = s2c.Writer.AsStream(),
            },
            serverOptions,
            loggerFactory);
        var serverRun = server.RunAsync(ct);

        var clientOptions = new McpClientOptions { ClientInfo = ClientInfo };
        if (protocolVersion is not null)
        {
            clientOptions = clientOptions with { ProtocolVersion = protocolVersion };
        }

        var client = McpClientBridge.ConnectAsync(
            new McpTransportOptions
            {
                Kind = McpTransportKind.Stream,
                ClientWriteStream = c2s.Writer.AsStream(),
                ClientReadStream = s2c.Reader.AsStream(),
            },
            clientOptions,
            loggerFactory,
            ct).GetAwaiter().GetResult();
        return (server, client, serverRun);
    }

    [Fact]
    public async Task Client_discovers_and_calls_tool_exposed_by_server()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var (server, client, serverRun) = ConnectAsync(cts.Token);

        await using var _server = server;
        await using var _client = client;
        server.AddTool(new McpToolDefinition
        {
            Name = "echo",
            Description = "Echoes the input back",
            Handler = (string message) => $"echo:{message}",
        });

        // 枚举：client 侧可见 server 暴露的工具（跨"语言生态"边界）
        var tools = await client.ListToolsAsync(cts.Token);
        var echo = Assert.Single(tools);
        Assert.Equal("echo", echo.Name);
        Assert.Contains("Echoes", echo.Description);

        // 调用：参数经 JSON-RPC 往返，结果带回
        var result = await client.CallToolAsync("echo", new Dictionary<string, object?> { ["message"] = "hello" }, cts.Token);
        Assert.False(result.IsError);
        Assert.Contains("echo:hello", string.Concat(result.TextContents));

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
        server.AddTool(new McpToolDefinition { Name = "alpha", Handler = (string x) => x });
        server.AddTool(new McpToolDefinition { Name = "beta", Handler = (string x) => x });

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

    [Fact]
    public void Bridge_public_contracts_reference_no_MCP_SDK_types()
    {
        // 隔离验收（用户关注点）：调用方契约（接口 + DTO）的公共签名不得出现任何
        // ModelContextProtocol.* 类型——实现可换（协议层升级 / MAF agent 集成层），调用方零改动。
        var contractTypes = new[]
        {
            typeof(IMcpClientBridge),
            typeof(IMcpServerBridge),
            typeof(McpToolDescriptor),
            typeof(McpToolCallResult),
            typeof(McpToolDefinition),
            typeof(McpTransportOptions),
            typeof(McpClientOptions),
            typeof(McpServerOptions),
            typeof(McpSessionIdentity),
        };

        foreach (var type in contractTypes)
        {
            var sdkTypeNames = GetExposedSdkTypeNames(type);
            Assert.True(
                sdkTypeNames.Count == 0,
                $"{type.Name} 公共面泄漏 SDK 类型: {string.Join(", ", sdkTypeNames)}");
        }
    }

    private static HashSet<string> GetExposedSdkTypeNames(Type type)
    {
        var result = new HashSet<string>();
        var methods = type.IsInterface
            ? type.GetMethods()
            : type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        foreach (var m in methods)
        {
            foreach (var p in m.GetParameters())
            {
                AddIfSdk(p.ParameterType, result);
            }

            AddIfSdk(m.ReturnType, result);
        }

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            AddIfSdk(prop.PropertyType, result);
        }

        return result;
    }

    private static void AddIfSdk(Type type, HashSet<string> result)
    {
        var n = type.Namespace ?? string.Empty;
        if (n.StartsWith("ModelContextProtocol", StringComparison.Ordinal) ||
            n.StartsWith("Microsoft.Extensions.AI", StringComparison.Ordinal))
        {
            result.Add(type.FullName ?? type.Name);
        }
    }
}
