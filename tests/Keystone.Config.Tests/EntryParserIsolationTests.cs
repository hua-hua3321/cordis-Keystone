using System.Reflection;
using Keystone.Config.Entries;

namespace Keystone.Config.Tests;

/// <summary>
/// 解耦隔离验收（15-decoupling-plan D2，C3）：EntryParser 公共 API 不泄漏 YamlDotNet 类型。
/// 解析入口 <see cref="EntryParser.Parse(string)"/> 返回纯框架类型；YamlNode 转换细节为 private。
/// </summary>
public class EntryParserIsolationTests
{
    [Fact]
    public void EntryParser_public_surface_has_no_yamldotnet_types()
    {
        var type = typeof(EntryParser);
        var leaks = new List<string>();

        foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            foreach (var p in m.GetParameters())
            {
                AddIfYamlDotNet(p.ParameterType, leaks, $"{m.Name}: param {p.Name}");
            }

            AddIfYamlDotNet(m.ReturnType, leaks, $"{m.Name}: return");
        }

        Assert.True(leaks.Count == 0, $"EntryParser 公共面泄漏 YamlDotNet 类型:\n{string.Join("\n", leaks)}");
    }

    private static void AddIfYamlDotNet(Type type, List<string> leaks, string context)
    {
        var n = type.Namespace ?? string.Empty;
        if (n.StartsWith("YamlDotNet", StringComparison.Ordinal))
        {
            leaks.Add($"{context}: {type.FullName}");
        }
    }
}
