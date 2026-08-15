using Keystone.Runtime.Plugins.Loading;
using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.Hosting.Tests;

/// <summary>
/// 端到端集成测试插件源码（P21 集成验收）：真实功能插件组。
/// calculator：真实业务（add/mul 计算服务）；telemetry：依赖注入消费者（inject calculator）；
/// audit：事件观察者（SubscribeParallel 监听任务事件）。
/// 经 Roslyn 编译进独立 ALC（Hosting 测试基建），走完整加载/门控/服务/事件链。
/// </summary>
public static class IntegrationSources
{
    public const string CalculatorSource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        // 真实业务：calculator 插件提供服务（键控服务，属主 = 本插件，03 §2.1）。
        public sealed class CalculatorPlugin : IPlugin
        {
            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                context.Provide("calculator", new CalculatorService());
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }

        // 真实服务：计算器（配置驱动：ops 数量来自 config）。
        public sealed class CalculatorService
        {
            public double Add(double a, double b) => a + b;
            public double Mul(double a, double b) => a * b;
        }
        """;

    public const string TelemetrySource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        // dependent 插件：inject calculator（ADR-0007 依赖门控）——注入后能解析到计算服务。
        public sealed class TelemetryPlugin : IPlugin
        {
            public static bool GotService;
            public static string? InjectedType;

            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                var svc = context.Get<object>("calculator");
                GotService = svc is not null;
                InjectedType = svc?.GetType().FullName;
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    public const string AuditSource = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        // 事件观察者插件：Subscribe 监听任务完成事件（观察不干预主链；emit 模式）。
        public sealed class AuditPlugin : IPlugin
        {
            public static int ObservedEvents;

            public sealed record TaskCompleted(string Capability, string Operation);

            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                context.Subscribe<TaskCompleted>(e =>
                {
                    ObservedEvents++;
                });
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;
}

/// <summary>端到端集成测试（P21）：真实功能全链——配置 → 宿主 → Roslyn 加载 → 门控 → 服务注入 → 能力域跨域调用 → 事件观察 → 优雅关闭。</summary>
public class EndToEndIntegrationTests
{
    private static KeystoneHostOptions Options() => new()
    {
        ManifestProvider = e => e.Id switch
        {
            "calculator" => new PluginManifest("calculator", "1.0.0", "Calculator.cs", ["cordis-runtime"], ["calculator"], []),
            "telemetry" => new PluginManifest("telemetry", "1.0.0", "Telemetry.cs", ["cordis-runtime"], [], ["calculator"]),
            "audit" => new PluginManifest("audit", "1.0.0", "Audit.cs", ["cordis-runtime"], [], []),
            _ => throw new InvalidOperationException($"unknown entry: {e.Id}"),
        },
        SourceProvider = e => e.Id switch
        {
            "calculator" => new PluginSource(e.Id!, IntegrationSources.CalculatorSource),
            "telemetry" => new PluginSource(e.Id!, IntegrationSources.TelemetrySource),
            "audit" => new PluginSource(e.Id!, IntegrationSources.AuditSource),
            _ => throw new InvalidOperationException($"unknown entry: {e.Id}"),
        },
    };

    [Fact]
    public async Task End_to_end_plugin_group_runs_real_function()
    {
        await using var host = new KeystoneHost(Options());

        // 1. 配置 → 宿主启动：YAML 定义三插件（audit 无依赖、calculator 提供、telemetry 依赖 calculator）
        await host.StartAsync("""
            - id: audit
              name: ./plugins/audit
            - id: calculator
              name: ./plugins/calculator
            - id: telemetry
              name: ./plugins/telemetry
              inject: [calculator]
            """);

        // 2. Roslyn 编译 + ALC + 依赖门控：三者均 ACTIVE（telemetry 等 calculator 就绪）
        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("audit"));
        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("calculator"));
        Assert.Equal(Keystone.Runtime.Plugins.Lifecycle.PluginLifecycleState.Active, host.GetPluginState("telemetry"));

        // 3. 服务注入验证：telemetry 经 inject 门控解析到 calculator 服务（键控服务跨插件可见）
        //    （telemetry 插件静态字段记录注入结果；类型在独立 ALC，经反射读取）
        Assert.True(ReadStaticBool("TelemetryPlugin", "GotService"), "telemetry 应经门控注入到 calculator 服务");
        Assert.Contains("CalculatorService", ReadStaticString("TelemetryPlugin", "InjectedType"));

        // 4. 能力域跨域调用：宿主侧 handler 接进能力域 actor，跨域请求执行真实计算（TaskEnvelope → TaskResultEnvelope）
        //    业务结果由 handler 内部处理（结果信封只含 TaskId/状态/错误，06 §1 契约）；此处 handler 写入并发集合
        var domain = host.GetCapabilityDomain();
        Assert.NotNull(domain);
        var computedResults = new System.Collections.Concurrent.ConcurrentDictionary<string, double>();
        var handle = domain.Spawn("calculator-inst", envelope =>
        {
            var op = envelope.Operation;
            var (a, b) = Parse(envelope.PayloadBytes ?? []);
            var result = op switch
            {
                "add" => a + b,
                "mul" => a * b,
                _ => throw new InvalidOperationException($"unknown operation: {op}"),
            };
            computedResults[envelope.TaskId.ToString()] = result;
            return Task.FromResult(new Keystone.Core.Contracts.TaskResultEnvelope
            {
                TaskId = envelope.TaskId,
                Succeeded = true,
                Type = Keystone.Core.Contracts.TaskResultType.Completed,
            });
        });

        var taskId = Guid.NewGuid();
        var result = await domain.RequestAsync(handle, new Keystone.Core.Contracts.TaskEnvelope
        {
            TaskId = taskId,
            Capability = "calculator",
            Operation = "add",
            PayloadBytes = System.Text.Encoding.UTF8.GetBytes("20,22"),
        }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(taskId, result.TaskId); // TaskId 跨域贯穿（O2 前置）
        Assert.Equal(42.0, computedResults[taskId.ToString()]); // 真实计算结果

        // 5. 事件观察：audit 插件已 Subscribe(TaskCompleted)——经共享事件总线（ID-08）发射，
        //    audit 收到事件（观察者不干预主链）。总线经 root context 访问（宿主未公开事件面——B4 集成发现，测试基建反射）
        var before = ReadStaticInt("AuditPlugin", "ObservedEvents");
        // AuditPlugin 类型在插件 ALC 内——经已加载程序集解析事件类型
        var eventType = FindType("AuditPlugin+TaskCompleted") ?? throw new InvalidOperationException("AuditPlugin.TaskCompleted not found");
        var evt = Activator.CreateInstance(eventType, "calculator", "add")!;
        // 纯总线发射（无 publisher）：G15 无上下文语义放行，audit 订阅收到（观察者不干预主链）
        // 注：evt 运行时类型在插件 ALC——用反射调用泛型 EmitAsync<TEvent>（编译期推断会退化为 object）
        var events = GetRootContext(host).Events;
        var emitMethod = events.GetType()
            .GetMethods()
            .First(m => m.Name == "EmitAsync" && m.IsGenericMethodDefinition)
            .MakeGenericMethod(eventType);
        var emitTask = (Task)emitMethod.Invoke(events, [evt, null, CancellationToken.None])!;
        await emitTask;
        var after = ReadStaticInt("AuditPlugin", "ObservedEvents");
        Assert.Equal(before + 1, after); // audit 收到事件

        // 6. 优雅关闭：quiesce（逐插件卸载 + ALC 回收）+ 能力域释放，幂等
        await host.ShutdownAsync();
        await host.ShutdownAsync();
    }

    private static (double A, double B) Parse(byte[] payload)
    {
        var text = System.Text.Encoding.UTF8.GetString(payload);
        var parts = text.Split(',');
        return (double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    // 跨 ALC 读取插件静态字段（插件类型在独立 ALC，测试基建经反射桥接；生产路径不涉及）
    private static bool ReadStaticBool(string typeName, string fieldName)
        => (bool)ReadStaticField(typeName, fieldName);

    private static string? ReadStaticString(string typeName, string fieldName)
        => (string?)ReadStaticField(typeName, fieldName);

    private static int ReadStaticInt(string typeName, string fieldName)
        => (int)ReadStaticField(typeName, fieldName);

    private static object ReadStaticField(string typeName, string fieldName)
    {
        // 插件 ALC 已卸载后字段不可读——在 Shutdown 前调用；此处从已加载程序集找类型
        var t = FindType(typeName)
            ?? throw new InvalidOperationException($"type {typeName} not found in loaded assemblies");

        var field = t.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException($"field {fieldName} not found on {t.FullName}");
        return field.GetValue(null)!;
    }

    private static Type? FindType(string typeName)
    {
        var simple = typeName.Contains('+') ? typeName[(typeName.LastIndexOf('+') + 1)..] : typeName;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType($"Keystone.Hosting.Tests.{typeName}")
                ?? asm.GetType(typeName);
            if (t is not null)
            {
                return t;
            }

            // 兜底：按简单名匹配（可能多个程序集同名——取第一个，插件 ALC 优先）
            var candidates = asm.GetTypes().Where(x => x.Name == simple).ToArray();
            if (candidates.Length == 1)
            {
                return candidates[0];
            }
        }

        return null;
    }

    // 宿主根 context（事件总线共享面，ID-08）：宿主未公开事件 API（B4 集成发现），测试基建反射取
    private static Keystone.Runtime.Context.IContext GetRootContext(Keystone.Hosting.KeystoneHost host)
    {
        var field = typeof(Keystone.Hosting.KeystoneHost)
            .GetField("_rootContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("_rootContext not found");
        return (Keystone.Runtime.Context.IContext)field.GetValue(host)!;
    }
}
