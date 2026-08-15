using System.Runtime.CompilerServices;

namespace Keystone.Runtime.Effects;

/// <summary>
/// effect 注册表（M1）：注册 disposer + 诊断元数据（label + [CallerInfo] 自动注入），
/// 嵌套 effect 形成诊断树；DisposeAll 逆序收敛（quiesce 语义，ADR-0005）。
/// </summary>
public interface IEffectRegistry
{
    /// <summary>
    /// 注册一个 disposer；label 供诊断；callerMember 由 [CallerMemberName] 自动注入调用者信息
    /// （M1 定稿；net10 BCL 无 CallerInfo 类型，用 CallerMemberName 等价，ID-07）。
    /// 在另一 effect 的回调内注册 → 成为其子节点。
    /// </summary>
    IDisposable Register(Func<Task> disposer, string? label = null, [CallerMemberName] string? callerMember = null);

    /// <summary>当前注册树（诊断视图）。</summary>
    IReadOnlyList<EffectMeta> GetEffects();

    /// <summary>逆序执行全部 disposer（先子后父、后注册先执行），幂等。</summary>
    Task DisposeAllAsync();
}
