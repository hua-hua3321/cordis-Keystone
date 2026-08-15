using System.Reflection;

namespace Keystone.Hosting.Tests;

/// <summary>
/// 解耦隔离验收（15-decoupling-plan D1，C2）：KeystoneHost 接线能力域——
/// 宿主可创建/持有能力域（管理层职责，01 §2/09 §2），且公共 API 不泄漏 Proto.Actor 类型。
/// </summary>
public class KeystoneHostCapabilityTests
{
    [Fact]
    public void Host_public_surface_has_no_proto_types()
    {
        var type = typeof(KeystoneHost);
        var leaks = new List<string>();

        foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (var p in ctor.GetParameters())
            {
                AddIfProto(p.ParameterType, leaks, $"ctor: param {p.Name}");
            }
        }

        foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            foreach (var p in m.GetParameters())
            {
                AddIfProto(p.ParameterType, leaks, $"{m.Name}: param {p.Name}");
            }

            AddIfProto(m.ReturnType, leaks, $"{m.Name}: return");
        }

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            AddIfProto(prop.PropertyType, leaks, $"prop {prop.Name}");
        }

        Assert.True(leaks.Count == 0, $"KeystoneHost 公共面泄漏 Proto 类型:\n{string.Join("\n", leaks)}");
    }

    [Fact]
    public async Task Host_provides_capability_domain_for_cross_domain_calls()
    {
        // 架构接线（01 §2/09 §2）：宿主持有能力域（管理层职责），跨域请求经框架句柄
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var host = new KeystoneHost(new KeystoneHostOptions());
        await host.StartAsync("");

        var domain = host.GetCapabilityDomain();
        Assert.NotNull(domain);

        var handle = domain.Spawn("probe", envelope =>
            Task.FromResult(new Keystone.Core.Contracts.TaskResultEnvelope
            {
                TaskId = envelope.TaskId,
                Succeeded = true,
                Type = Keystone.Core.Contracts.TaskResultType.Completed,
            }));

        var result = await domain.RequestAsync(handle, new Keystone.Core.Contracts.TaskEnvelope
        {
            TaskId = Guid.NewGuid(),
            Capability = "probe",
            Operation = "ping",
            PayloadBytes = [],
        }, cts.Token);

        Assert.True(result.Succeeded);

        await host.ShutdownAsync();
    }

    private static void AddIfProto(Type type, List<string> leaks, string context)
    {
        var n = type.Namespace ?? string.Empty;
        if (n.StartsWith("Proto", StringComparison.Ordinal))
        {
            leaks.Add($"{context}: {type.FullName}");
        }
    }
}
