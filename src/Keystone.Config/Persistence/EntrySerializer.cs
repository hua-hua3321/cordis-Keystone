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
            // map 形态回写（18 §2 CA-1）：name: true|false|label，按键序确定输出（diff 稳定）
            sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}  isolate:");
            foreach (var (name, spec) in entry.Isolate.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}    {name}: {spec}");
            }
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
            case Dictionary<string, object?> { Count: 0 }:
                sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}{key}: {{}}"); // P2-28：空容器显式（防塌缩 null）
                break;

            case IReadOnlyList<object?> { Count: 0 }:
                sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}{key}: []");
                break;

            case Dictionary<string, object?> map:
                sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}{key}:");
                foreach (var (k, v) in map)
                {
                    AppendValue(sb, k, v, pad + "  ");
                }

                break;

            case IReadOnlyList<object?> list:
                // P2-28（19 号审计 IN-8）：字典列表块形（`key:` 后逐 `- k: v`）——
                // 修复前每项重复 `key:` 头 → 重解析仅留最后一项（数据丢失）
                sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}{key}:");
                foreach (var item in list)
                {
                    if (item is Dictionary<string, object?> itemMap)
                    {
                        AppendMapItem(sb, itemMap, pad + "  ");
                    }
                    else
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}  - {Quote(item)}");
                    }
                }

                break;

            default:
                sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}{key}: {Quote(value)}");
                break;
        }
    }

    /// <summary>字典列表项：`- k: v` 首键随连字符，其余右对齐同缩进。</summary>
    private static void AppendMapItem(StringBuilder sb, Dictionary<string, object?> map, string pad)
    {
        var first = true;
        foreach (var (k, v) in map)
        {
            if (first)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"{pad}- {k}: {Quote(v)}");
                first = false;
                continue;
            }

            AppendValue(sb, k, v, pad + "  ");
        }
    }

    /// <summary>
    /// P2-28：含 YAML 特殊字符（<c>:</c> 后空格 / <c>#</c> / 前后空格 / 流标点）或空串的
    /// 字符串标量双引号输出（内嵌引号转义）——修复前裸输出重解析错切/裁剪/变注释。
    /// 非字符串与非特殊短字符串保持裸形态（既有回读格式不变）。
    /// </summary>
    private static readonly System.Buffers.SearchValues<char> SpecialChars =
        System.Buffers.SearchValues.Create(":,{}[]&*!|>'\"%@`");

    private static string Quote(object? value)
    {
        if (value is not string text)
        {
            return value?.ToString() ?? "null";
        }

        var needsQuoting = text.Length == 0
            || text.Contains(": ", StringComparison.Ordinal)
            || text.Contains(" #", StringComparison.Ordinal)
            || char.IsWhiteSpace(text[0])
            || char.IsWhiteSpace(text[^1])
            || text.AsSpan().IndexOfAny(SpecialChars) >= 0;
        if (!needsQuoting)
        {
            return text;
        }

        // 内嵌反斜杠与双引号转义（双引号字面量经字符字面量拼接，规避嵌套转义噪音）
        var escaped = text
            .Replace(char.ToString('\\'), char.ToString('\\') + char.ToString('\\'), StringComparison.Ordinal)
            .Replace(char.ToString('"'), char.ToString('\\') + char.ToString('"'), StringComparison.Ordinal);
        return char.ToString('"') + escaped + char.ToString('"');
    }
}
