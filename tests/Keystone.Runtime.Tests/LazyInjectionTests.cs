using Keystone.Runtime.Context;

namespace Keystone.Runtime.Tests;

/// <summary>
/// G-C5/M4 方法级延迟注入测试（16-cordis-gap-review）：GetLazy 首次访问才解析——
/// 初始化时声明、方法执行时服务已就绪（对齐 Cordis @Inject 方法级，registry.ts:45-59）。
/// </summary>
public class LazyInjectionTests
{
    private sealed record Service(int Value);

    [Fact]
    public async Task GetLazy_resolves_on_first_access()
    {
        var root = new ContextFacade("root");
        var consumer = new ContextFacade("consumer", root);

        // 初始化时声明 Lazy（服务未提供）——不立即解析
        var lazy = consumer.GetLazy<Service>("svc");

        // 服务后提供
        root.Provide("svc", new Service(42));

        // 方法执行时首次访问 → 解析成功
        var service = await lazy.Value;

        Assert.Equal(42, service.Value);
    }

    [Fact]
    public async Task GetLazy_is_evaluated_once()
    {
        var root = new ContextFacade("root");
        var consumer = new ContextFacade("consumer", root);
        root.Provide("svc", new Service(1));

        var lazy = consumer.GetLazy<Service>("svc");

        var first = await lazy.Value;
        var second = await lazy.Value;

        Assert.Equal(1, first.Value);
        Assert.Same(first, second); // Lazy：只解析一次，缓存实例
    }

    [Fact]
    public async Task GetLazy_throws_when_service_never_provided()
    {
        var consumer = new ContextFacade("consumer");

        var lazy = consumer.GetLazy<Service>("missing");

        await Assert.ThrowsAsync<Keystone.Core.Errors.KeystoneException>(async () => await lazy.Value);
    }
}
