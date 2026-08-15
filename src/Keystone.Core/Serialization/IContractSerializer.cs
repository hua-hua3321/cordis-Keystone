namespace Keystone.Core.Serialization;

/// <summary>
/// 契约序列化器抽象（ADR-0004 决策 1：跨域/持久化边界显式序列化，MessagePack 默认 / JSON 可配置）。
/// 15-decoupling-plan D3（C6）：序列化动作不钉死具体实现——默认 <see cref="MessagePackContractSerializer"/>，
/// 可注入 <see cref="JsonContractSerializer"/>（调试/审计）。
/// AOT 安全：实现类必须用源生成（MessagePack [MessagePackObject] / STJ [JsonSerializable]），禁止反射。
/// </summary>
public interface IContractSerializer
{
    /// <summary>序列化为字节（契约类型源生成）。</summary>
    byte[] Serialize<T>(T value);

    /// <summary>从字节反序列化（契约类型源生成）。</summary>
    T Deserialize<T>(byte[] data);
}
