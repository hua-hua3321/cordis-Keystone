namespace Keystone.Runtime.Effects;

/// <summary>
/// effect 诊断元数据（M1，doc 12 §8）：label + 调用者信息 + 嵌套子 effect。
/// 嵌套树 = effect 回调执行期间注册的 effect（Cordis getEffects 等价物）。
/// </summary>
public sealed record EffectMeta(string Label, string? CallerMember, IReadOnlyList<EffectMeta> Children);
