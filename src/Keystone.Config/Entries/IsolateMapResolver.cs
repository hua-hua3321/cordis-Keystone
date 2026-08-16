
namespace Keystone.Config.Entries;

/// <summary>
/// isolate map 解析（P57-T5，18 §2 CA-1）：entry.Isolate 声明 → name→realm 视图。
/// realm 规则（对齐 Cordis LocalRealm/GlobalRealm）：
/// Private → <c>#声明处条目Id</c>（组声明=组内共享 <c>#groupId</c>；叶自声明=独占 <c>#leafId</c>）；
/// Shared(label) → <c>@label</c>（跨组命名共享）；None → 移除（解除继承，回落 ""）。
/// 谱系叠加：外层先、内层后，per-name 子影子覆盖父（对齐 context.isolate() 原型链 shadow）。
/// </summary>
public static class IsolateMapResolver
{
    /// <summary>把一层声明并入累积 map（子层调用覆盖父层同名项）。</summary>
    public static void Apply(EntryOptions entry, IDictionary<string, string> map)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(map);

        foreach (var (name, spec) in entry.Isolate)
        {
            switch (spec.Kind)
            {
                case IsolateKind.Private:
                    map[name] = $"#{entry.Id}";
                    break;
                case IsolateKind.Shared:
                    map[name] = $"@{spec.Label}";
                    break;
                case IsolateKind.None:
                default:
                    map.Remove(name);
                    break;
            }
        }
    }

    /// <summary>沿谱系（外→内）解析生效 map；全空声明 → null（≡ 无 map，回落默认共享 ""）。</summary>
    public static IReadOnlyDictionary<string, string>? Resolve(IEnumerable<EntryOptions> genealogy)
    {
        ArgumentNullException.ThrowIfNull(genealogy);

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in genealogy)
        {
            Apply(entry, map);
        }

        return map.Count == 0 ? null : map;
    }
}
