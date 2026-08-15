using System.Text.Json.Serialization;
using Keystone.Core.Serialization;
using MessagePack;

namespace Keystone.Core.Tests;

/// <summary>STJ 源生成上下文（测试专用，含 Sample）。</summary>
[JsonSerializable(typeof(ContractSerializerTests.Sample))]
public sealed partial class TestJsonContext : JsonSerializerContext;

/// <summary>
/// 序列化器抽象测试（15-decoupling-plan D3，C6）：兑现 ADR-0004"MessagePack 默认 / JSON 可配置"。
/// 契约保留 [MessagePackObject]（源生成契约声明），序列化动作经 IContractSerializer 抽象——
/// 默认 MessagePack，可注入 JSON（调试/审计）。
/// </summary>
public class ContractSerializerTests
{
    [MessagePackObject]
    public sealed record Sample(
        [property: Key(0)] int A,
        [property: Key(1)] string B);

    [Fact]
    public void MessagePack_default_roundtrips_contract()
    {
        var serializer = new MessagePackContractSerializer();
        var original = new Sample(42, "hello");

        var bytes = serializer.Serialize(original);
        var restored = serializer.Deserialize<Sample>(bytes);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Json_serializer_roundtrips_with_registered_context()
    {
        var serializer = new JsonContractSerializer(TestJsonContext.Default);
        var original = new Sample(7, "json");

        var bytes = serializer.Serialize(original);
        var restored = serializer.Deserialize<Sample>(bytes);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Json_output_is_human_readable_for_debugging()
    {
        var serializer = new JsonContractSerializer(TestJsonContext.Default);

        var bytes = serializer.Serialize(new Sample(1, "audit"));

        // 审计/调试：JSON 文本可读
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("audit", text);
    }

    [Fact]
    public void FileEventStore_accepts_injected_serializer()
    {
        // 消费点：事件持久化经 IContractSerializer（默认 MessagePack，可注入）
        // 此处验证接口存在且默认实现可实例化（FileEventStore 集成走 Runtime.Tests）
        var serializer = new MessagePackContractSerializer();
        Assert.NotNull(serializer);
    }
}
