using System.Reflection;
using Keystone.Runtime.Actors;

namespace Keystone.Runtime.Tests;

/// <summary>
/// 解耦隔离验收（15-decoupling-plan D1，C1/C1b）：
/// CapabilityDomain 的常规公共 API（Create/Spawn/RequestAsync）不得泄漏 Proto.Actor 类型——
/// 调用方（宿主/插件）不应被迫引用 Proto.Actor 才能使用能力域。
/// <see cref="CapabilityDomain.Attach"/> 是显式测试缝/高级共享场景（注入既有 ActorSystem），
/// 调用方在该场景主动选择 Proto 共享，不计入常规面泄漏。
/// CapabilityActor（实现细节，Proto.IActor/IContext）必须保持 internal。
/// </summary>
public class CapabilityDomainIsolationTests
{
    [Fact]
    public void CapabilityDomain_public_surface_has_no_proto_types()
    {
        var type = typeof(CapabilityDomain);
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
            if (m.Name == "Attach")
            {
                continue; // 显式测试缝/共享 ActorSystem 高级场景
            }

            foreach (var p in m.GetParameters())
            {
                AddIfProto(p.ParameterType, leaks, $"{m.Name}: param {p.Name}");
            }

            AddIfProto(m.ReturnType, leaks, $"{m.Name}: return");
        }

        Assert.True(leaks.Count == 0, $"CapabilityDomain 公共面泄漏 Proto 类型:\n{string.Join("\n", leaks)}");
    }

    [Fact]
    public void CapabilityActor_is_not_public()
    {
        // C1b：CapabilityActor 是实现细节（实现 Proto.IActor），必须 internal——Proto 类型随之内聚
        var type = typeof(CapabilityDomain).Assembly.GetType("Keystone.Runtime.Actors.CapabilityActor");
        Assert.NotNull(type);
        Assert.False(type!.IsPublic, "CapabilityActor 不应是 public（Proto.IActor/IContext 泄漏面）");
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
