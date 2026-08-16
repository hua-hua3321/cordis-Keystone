namespace Keystone.Runtime.Context;

/// <summary>
/// 服务定位键（18 §2 CA-1）：(服务名, 域)。
/// realm ∈ {"" 默认共享, "#groupId" 私有, "@label" 命名共享}（对齐 Cordis LocalRealm/GlobalRealm 后缀）。
/// </summary>
public readonly record struct ServiceKey(string Name, string Realm);
