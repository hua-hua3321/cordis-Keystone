using System.Globalization;
using System.Text;
using Keystone.Config.Entries;

namespace Keystone.Config.Persistence;

/// <summary>条目 → YAML 文本（手写序列化，规则 0：无反射序列化器）。</summary>
public static class EntrySerializer
{
    public static string Serialize(IReadOnlyList<EntryOptions> entries)
        // 显式 lambda：方法组会绑定 Select 的 (T,int) 索引重载，把元素下标灌进 indent（P45 回归）
        => string.Join("\n", entries.Select(e => SerializeEntry(e)));

    private static string SerializeEntry(EntryOptions entry, int indent = 0)
    {
        var sb = new StringBuilder();
        var pad = new string(' ', indent);
        sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}- id: {entry.Id}");
        if (entry.Name is not null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}  name: {entry.Name}");
        }

        if (entry.Config is not null)
        {
            AppendValue(sb, "config", entry.Config, pad + "  ");
        }

        if (entry.Disabled is not null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}  disabled: {entry.Disabled.Value.ToString().ToLowerInvariant()}");
        }

        if (entry.Inject.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}  inject: [{string.Join(", ", entry.Inject)}]");
        }

        if (entry.Isolate.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}  isolate: [{string.Join(", ", entry.Isolate)}]");
        }

        if (entry.Group is not null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}  group:");
            foreach (var child in entry.Group)
            {
                sb.Append(SerializeEntry(child, indent + 2));
            }
        }

        return sb.ToString();
    }

    private static void AppendValue(StringBuilder sb, string key, object? value, string pad)
    {
        switch (value)
        {
            case Dictionary<string, object?> map:
                sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}{key}:");
                foreach (var (k, v) in map)
                {
                    AppendValue(sb, k, v, pad + "  ");
                }

                break;

            case IReadOnlyList<object?> list:
                foreach (var item in list)
                {
                    if (item is Dictionary<string, object?> itemMap)
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}{key}:");
                        foreach (var (k, v) in itemMap)
                        {
                            AppendValue(sb, k, v, pad + "  ");
                        }
                    }
                    else
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}{key}: [{item}]");
                    }
                }

                break;

            default:
                sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}{key}: {value}");
                break;
        }
    }
}
