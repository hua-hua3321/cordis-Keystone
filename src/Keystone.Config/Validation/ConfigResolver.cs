namespace Keystone.Config.Validation;

using Keystone.Core.Errors;

/// <summary>
/// 配置解析管线（M3 定稿）：过滤器链（可否决）→ schema 校验（fail-fast 精确报错）→ 默认值补齐。
/// </summary>
public sealed class ConfigResolver
{
    public async Task<object?> ResolveAsync(
        object? raw,
        ConfigSchema schema,
        IReadOnlyList<IConfigFilter> filters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(filters);

        // 过滤器 next 链（形状 B 反向包装）
        Func<object?, Task> next = _ => Task.CompletedTask;
        for (var i = filters.Count - 1; i >= 0; i--)
        {
            var filter = filters[i];
            var inner = next;
            next = value => filter.OnConfigAsync(value, inner, cancellationToken);
        }

        await next(raw).ConfigureAwait(false);

        var validation = schema.Validate(raw);
        if (!validation.IsValid)
        {
            throw new KeystoneException(
                ErrorCode.ConfigValidationFailed,
                $"invalid config:\n{string.Join("\n", validation.Errors)}");
        }

        return schema.ApplyDefaults(raw);
    }
}
