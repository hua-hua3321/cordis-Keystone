
namespace Keystone.Config.Validation;

/// <summary>配置解析过滤器（M3 管线：raw → 过滤器链（可否决）→ 校验 → 注入）。</summary>
public interface IConfigFilter
{
    /// <summary>包裹配置解析；不调 next 即否决（waterfall 语义）。</summary>
    Task OnConfigAsync(object? raw, Func<object?, Task> next, CancellationToken cancellationToken);
}
