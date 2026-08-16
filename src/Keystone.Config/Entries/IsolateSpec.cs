namespace Keystone.Config.Entries;

/// <summary>
/// isolate 单名声明值（两档域 + 显式解除）。值语义（readonly record struct）：
/// 分层合并、ConfigDiffer 结构键、测试断言均按 (Kind, Label) 成员相等。
/// </summary>
public readonly record struct IsolateSpec
{
    private IsolateSpec(IsolateKind kind, string? label)
    {
        Kind = kind;
        Label = label;
    }

    /// <summary>声明档位。</summary>
    public IsolateKind Kind { get; }

    /// <summary>命名共享域的 label（仅 <see cref="IsolateKind.Shared"/> 时非 null）。</summary>
    public string? Label { get; }

    /// <summary>true 档：条目私有域。</summary>
    public static IsolateSpec Private() => new(IsolateKind.Private, null);

    /// <summary>label 档：命名共享域。</summary>
    public static IsolateSpec Shared(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return new(IsolateKind.Shared, label);
    }

    /// <summary>false 档：显式解除（分层补丁撤销底层声明）。</summary>
    public static IsolateSpec None() => new(IsolateKind.None, null);

    /// <summary>诊断/序列化形态（与 YAML 标量值一致）。</summary>
    public override string ToString()
        => Kind switch
        {
            IsolateKind.Private => "true",
            IsolateKind.Shared => Label ?? string.Empty,
            _ => "false",
        };
}
