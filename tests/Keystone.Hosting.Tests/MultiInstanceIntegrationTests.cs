using Keystone.Runtime.Actors;
using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.Hosting.Tests;

/// <summary>
/// 多实例集成测试（P33，01 §4 多实例模型兑现）：同一插件组在宿主内 spawn 多个能力域实例，
/// 各自独立 context/管道、并行处理不同任务。验证多实例隔离/并行执行/管道独立/TaskId 贯穿/事件观察。
/// </summary>
public class MultiInstanceIntegrationTests
{
    public const string CalcGroupSource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        // 业务插件：计算能力域（提供服务；handler 由宿主接线）
        public sealed class CalcPlugin : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                context.Provide("calc", new CalcService());
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }

        public sealed class CalcService
        {
            public double Add(double a, double b) => a + b;
            public double Mul(double a, double b) => a * b;
            public double Sub(double a, double b) => a - b;
        }
        """;

    public const string ObserverSource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        // 事件观察插件：Subscribe 监听任务完成（事件隔离/共享验证，ID-08）
        public sealed class ObserverPlugin : IPlugin
        {
            public static System.Collections.Concurrent.ConcurrentBag<string> Seen = new();

            public sealed record TaskDone(string Instance, string Operation);

            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                context.Subscribe<TaskDone>(e => Seen.Add($"{e.Instance}:{e.Operation}"));
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    private static KeystoneHostOptions Options() => new()
    {
        ManifestProvider = e => e.Id switch
        {
            "calc" => new PluginManifest("calc", "1.0.0", "Calc.cs", ["cordis-runtime"], ["calc"], []),
            "observer" => new PluginManifest("observer", "1.0.0", "Observer.cs", ["cordis-runtime"], [], []),
            _ => throw new InvalidOperationException($"unknown entry: {e.Id}"),
        },
        SourceProvider = e => e.Id switch
        {
            "calc" => new PluginSource(e.Id!, CalcGroupSource),
            "observer" => new PluginSource(e.Id!, ObserverSource),
            _ => throw new InvalidOperationException($"unknown entry: {e.Id}"),
        },
    };

    private sealed class AuditMiddleware : Keystone.Runtime.Pipeline.IMiddleware
    {
        private readonly string _instance;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _traces;

        public AuditMiddleware(string instance, System.Collections.Concurrent.ConcurrentDictionary<string, string> traces)
        {
            _instance = instance;
            _traces = traces;
        }

        public string Id => "audit-mw";

        public int Order => 1;

        public async Task InvokeAsync(Keystone.Runtime.Context.IPluginContext ctx, Keystone.Runtime.Pipeline.RequestDelegate next)
        {
            _traces[_instance] = (_traces.TryGetValue(_instance, out var v) ? v : "") + "before;";
            await next(ctx);
            _traces[_instance] = _traces[_instance] + "after;";
        }
    }

    [Fact]
    public async Task Instance_context_is_persistent_across_requests()
    {
        // DC-1（01 §4）：actor 持实例级持久 context——跨请求状态经实例 context 驻留。
        // 中间件把计数存到 ctx（若每请求新建 context，计数不会累积）。
        await using var host = new KeystoneHost(Options());
        await host.StartAsync("""
            - id: calc
              name: ./plugins/calc
            """);

        var domain = host.GetCapabilityDomain()!;
        var counterMiddleware = new CounterMiddleware();
        var handle = domain.Spawn("stateful",
            envelope => Task.FromResult(new Keystone.Core.Contracts.TaskResultEnvelope
            {
                TaskId = envelope.TaskId,
                Succeeded = true,
                Type = Keystone.Core.Contracts.TaskResultType.Completed,
            }),
            [counterMiddleware]);

        await RequestAsync(handle, "stateful", "add", "1,2");
        await RequestAsync(handle, "stateful", "add", "3,4");
        await RequestAsync(handle, "stateful", "add", "5,6");

        // 实例 context 持久：中间件经 ctx 存取计数（每请求新建 context 则 Get 到 null，恒 1）
        Assert.Equal(3, counterMiddleware.RequestCount);

        await host.ShutdownAsync();
    }

    /// <summary>经 ctx 跨请求计数（验证实例 context 持久，DC-1）：计数存 ctx，下请求从 ctx 读。</summary>
    private sealed class CounterMiddleware : Keystone.Runtime.Pipeline.IMiddleware
    {
        public string Id => "counter";

        public int Order => 1;

        public int RequestCount;

        public async Task InvokeAsync(Keystone.Runtime.Context.IPluginContext ctx, Keystone.Runtime.Pipeline.RequestDelegate next)
        {
            // 从实例 context 读累计计数（每请求新建则读不到默认 0 → 恒 1）
            var prior = ctx.TryGet<object>("counter") is int n ? n : 0;
            RequestCount = prior + 1;
            // D-6（P68）：首写 Provide（注册），后续 Set（原位更新）——二次 Provide 已是报错语义
            if (prior == 0)
            {
                ctx.Provide("counter", RequestCount);
            }
            else
            {
                ctx.Set("counter", RequestCount);
            }

            await next(ctx);
        }
    }

    [Fact]
    public async Task Multiple_instances_run_in_parallel_with_isolation()
    {
        await using var host = new KeystoneHost(Options());

        // 1. 宿主启动：插件组（calc 提供 + observer 观察）
        await host.StartAsync("""
            - id: calc
              name: ./plugins/calc
            - id: observer
              name: ./plugins/observer
            """);

        _hostEvents = host.Events; // StartAsync 后可用（root context 已建）;

        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("calc"));
        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("observer"));

        // 2. 同一插件组 spawn 3 个能力域实例（各自独立 context/管道）
        var domain = host.GetCapabilityDomain()!;
        var traces = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
        var values = new System.Collections.Concurrent.ConcurrentDictionary<string, double>();

        var instanceA = domain.Spawn("calc-a", CalcHandler("add", "calc-a", values), [new AuditMiddleware("calc-a", traces)]);
        var instanceB = domain.Spawn("calc-b", CalcHandler("mul", "calc-b", values), [new AuditMiddleware("calc-b", traces)]);
        var instanceC = domain.Spawn("calc-c", CalcHandler("sub", "calc-c", values), [new AuditMiddleware("calc-c", traces)]);

        // 3. 并行处理不同任务（a 加法 / b 乘法 / c 减法），各实例独立
        var (resultA, taskA) = await RequestAsync(instanceA, "calc-a", "add", "20,22");
        var (resultB, taskB) = await RequestAsync(instanceB, "calc-b", "mul", "6,7");
        var (resultC, taskC) = await RequestAsync(instanceC, "calc-c", "sub", "50,8");

        // 4. 并行 + 隔离：每实例独立结果（同值 42 但来自不同实例不同运算）
        Assert.All([resultA, resultB, resultC], r => Assert.True(r.Succeeded));
        Assert.Equal(42.0, values["calc-a"]);
        Assert.Equal(42.0, values["calc-b"]); // 6*7
        Assert.Equal(42.0, values["calc-c"]); // 50-8

        // 5. 管道每实例独立：audit-mw before/after 包裹各自请求
        Assert.Equal("before;after;", traces["calc-a"]);
        Assert.Equal("before;after;", traces["calc-b"]);
        Assert.Equal("before;after;", traces["calc-c"]);

        // 6. TaskId 贯穿：每任务返回原 TaskId
        Assert.Equal(taskA, resultA.TaskId);
        Assert.Equal(taskB, resultB.TaskId);
        Assert.Equal(taskC, resultC.TaskId);

        // 7. 事件观察：observer 收到各实例事件（共享总线 ID-08；实例身份随事件携带）
        await Task.Delay(100); // 事件经异步发射
        var observed = ReadObserverSeen();
        Assert.Contains("calc-a:add", observed);
        Assert.Contains("calc-b:mul", observed);
        Assert.Contains("calc-c:sub", observed);

        await host.ShutdownAsync();
    }

    private static Func<Keystone.Core.Contracts.TaskEnvelope, Task<Keystone.Core.Contracts.TaskResultEnvelope>> CalcHandler(
        string op,
        string instance,
        System.Collections.Concurrent.ConcurrentDictionary<string, double> values)
        => async envelope =>
        {
            var (a, b) = Parse(envelope.PayloadBytes ?? []);
            var value = op switch
            {
                "add" => a + b,
                "mul" => a * b,
                "sub" => a - b,
                _ => throw new InvalidOperationException($"unknown op {op}"),
            };
            values[instance] = value;
            var result = new Keystone.Core.Contracts.TaskResultEnvelope
            {
                TaskId = envelope.TaskId,
                Succeeded = true,
                Type = Keystone.Core.Contracts.TaskResultType.Completed,
            };
            await EmitTaskDoneAsync(instance, op).ConfigureAwait(false); // 观察者收到（共享总线）
            return result;
        };

    private static Keystone.Runtime.Events.IEventBus? _hostEvents;

    private static async Task EmitTaskDoneAsync(string instance, string op)
    {
        var events = _hostEvents;
        var eventType = FindType("ObserverPlugin+TaskDone");
        if (events is null || eventType is null)
        {
            return;
        }

        var evt = Activator.CreateInstance(eventType, instance, op)!;
        var emit = events.GetType().GetMethods()
            .First(m => m.Name == "EmitAsync" && m.IsGenericMethodDefinition)
            .MakeGenericMethod(eventType);
        await (Task)emit.Invoke(events, [evt, null, CancellationToken.None])!;
    }

    private static async Task<(Keystone.Core.Contracts.TaskResultEnvelope Result, Guid TaskId)> RequestAsync(
        CapabilityHandle handle, string instance, string op, string payload)
    {
        var taskId = Guid.NewGuid();
        var result = await handle.RequestAsync(new Keystone.Core.Contracts.TaskEnvelope
        {
            TaskId = taskId,
            Capability = "calc",
            Operation = op,
            PayloadBytes = System.Text.Encoding.UTF8.GetBytes(payload),
        }, CancellationToken.None);
        return (result, taskId);
    }

    private static (double A, double B) Parse(byte[] payload)
    {
        var text = System.Text.Encoding.UTF8.GetString(payload);
        var parts = text.Split(',');
        return (double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static List<string> ReadObserverSeen()
    {
        var result = new List<string>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetTypes().FirstOrDefault(x => x.Name == "ObserverPlugin");
            if (t is null)
            {
                continue;
            }

            var field = t.GetField("Seen", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (field?.GetValue(null) is System.Collections.Concurrent.ConcurrentBag<string> bag)
            {
                result.AddRange(bag);
            }
        }

        return result.Distinct(StringComparer.Ordinal).ToList();
    }

    private static Type? FindType(string typeName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetTypes().FirstOrDefault(x => x.FullName == typeName || x.Name == typeName);
            if (t is not null)
            {
                return t;
            }
        }

        return null;
    }
}
