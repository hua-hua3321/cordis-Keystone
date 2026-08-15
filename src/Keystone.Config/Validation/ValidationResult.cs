namespace Keystone.Config.Validation;

/// <summary>校验结果（精确报错：字段 + 期望）。</summary>
public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Ok { get; } = new(true, []);
}
