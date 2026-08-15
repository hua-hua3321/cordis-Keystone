using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Keystone.Core.Serialization;

/// <summary>
/// JSON 契约序列化器（ADR-0004 "JSON 可配置"实现，调试/审计场景）：
/// 注入 <see cref="JsonSerializerContext"/>（STJ 源生成）保证 AOT 安全——禁止反射序列化（规则 0 第 3 条）。
/// </summary>
public sealed class JsonContractSerializer : IContractSerializer
{
    private readonly JsonSerializerContext _context;

    public JsonContractSerializer(JsonSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public byte[] Serialize<T>(T value)
    {
        var typeInfo = _context.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
            ?? throw new InvalidOperationException($"类型 {typeof(T).FullName} 未注册到 JsonSerializerContext（源生成）");
        return JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
    }

    /// <inheritdoc />
    public T Deserialize<T>(byte[] data)
    {
        var typeInfo = _context.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
            ?? throw new InvalidOperationException($"类型 {typeof(T).FullName} 未注册到 JsonSerializerContext（源生成）");
        return JsonSerializer.Deserialize(data, typeInfo)!;
    }
}
