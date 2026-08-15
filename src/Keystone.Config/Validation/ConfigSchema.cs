namespace Keystone.Config.Validation;

/// <summary>
/// 声明式配置 schema（P6 基础实现；P11 SDK 增强为源生成器形式）：
/// 必填缺失 + 未知字段 fail-fast；默认值补齐。
/// </summary>
public sealed class ConfigSchema
{
    private readonly IReadOnlyList<ConfigField> _fields;

    public ConfigSchema(IReadOnlyList<ConfigField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        _fields = fields;
    }

    public ValidationResult Validate(object? raw)
    {
        if (raw is not Dictionary<string, object?> map)
        {
            return new ValidationResult(false, ["config must be a mapping"]);
        }

        var errors = new List<string>();
        foreach (var field in _fields)
        {
            if (field.Required && !map.ContainsKey(field.Name))
            {
                errors.Add($"missing required field '{field.Name}'");
            }
        }

        foreach (var key in map.Keys)
        {
            if (_fields.All(f => !string.Equals(f.Name, key, StringComparison.Ordinal)))
            {
                errors.Add($"unknown field '{key}'");
            }
        }

        return errors.Count == 0 ? ValidationResult.Ok : new ValidationResult(false, errors);
    }

    public object? ApplyDefaults(object? raw)
    {
        if (raw is not Dictionary<string, object?> map)
        {
            return raw;
        }

        var result = new Dictionary<string, object?>(map, StringComparer.Ordinal);
        foreach (var field in _fields)
        {
            if (!result.ContainsKey(field.Name) && field.Default is not null)
            {
                result[field.Name] = field.Default;
            }
        }

        return result;
    }
}
