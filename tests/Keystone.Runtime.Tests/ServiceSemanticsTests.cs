using Keystone.Core.Errors;
using Keystone.Runtime.Context;

namespace Keystone.Runtime.Tests;

/// <summary>
/// P68（19 号审计 D-6 + P2-29）：服务注册/更新语义对齐 Cordis。
/// D-6（对齐 reflect.ts:289-291/254-265）：同域二次 Provide 一律抛错（无论属主——修复前
/// 同属主 rebind 静默覆盖）；更新走显式 Set——属主校验、未提供抛错、不通知
///（依赖方门控不重评：值换血不等于服务下线/上线）。
/// P2-29（对齐 events.ts fire-and-forget）：EmitFireAndForget——异步监听不阻塞发布方，
/// 异常被观察（不产生未观察任务异常）。
/// </summary>
public class ServiceSemanticsTests
{
    private sealed record Ping(string Value);

    // ── D-6：Provide 报错式 ──

    [Fact]
    public void Second_provide_same_owner_throws()
    {
        var store = new KeyedServiceStore();
        store.Provide("fs", string.Empty, "old", "plugin-a");

        var exception = Assert.Throws<KeystoneException>(
            () => store.Provide("fs", string.Empty, "new", "plugin-a"));
        Assert.Equal(ErrorCode.ServiceAlreadyRegistered, exception.Code);
        Assert.Equal("old", store.TryGet<string>("fs", string.Empty)); // 原值不被破坏
    }

    [Fact]
    public void Facade_second_provide_same_owner_throws()
    {
        var root = new ContextFacade("root");
        var a = new ContextFacade("a", root);
        a.Provide("fs", "old");

        var exception = Assert.Throws<KeystoneException>(() => a.Provide("fs", "new"));
        Assert.Equal(ErrorCode.ServiceAlreadyRegistered, exception.Code);
        Assert.Equal("old", root.Get<string>("fs"));
    }

    // ── D-6：Set（更新不通知）──

    [Fact]
    public void Set_updates_value_without_notification()
    {
        var store = new KeyedServiceStore();
        var notifications = 0;
        store.Subscribe(_ => notifications++);
        store.Provide("fs", string.Empty, "old", "plugin-a");

        store.Set("fs", string.Empty, "new", "plugin-a");

        Assert.Equal("new", store.TryGet<string>("fs", string.Empty));
        Assert.Equal(1, notifications); // Provide 一次；Set 不通知（依赖方门控不重评）
    }

    [Fact]
    public void Set_requires_existing_key_and_owner()
    {
        var store = new KeyedServiceStore();

        var missing = Assert.Throws<KeystoneException>(
            () => store.Set("fs", string.Empty, "v", "plugin-a"));
        Assert.Equal(ErrorCode.GatingServiceNotFound, missing.Code); // 未提供抛错

        store.Provide("fs", string.Empty, "old", "plugin-a");
        var wrongOwner = Assert.Throws<KeystoneException>(
            () => store.Set("fs", string.Empty, "v", "plugin-b"));
        Assert.Equal(ErrorCode.ServiceAlreadyRegistered, wrongOwner.Code); // 属主校验
        Assert.Equal("old", store.TryGet<string>("fs", string.Empty));
    }

    [Fact]
    public void Facade_set_updates_value_silently()
    {
        var root = new ContextFacade("root");
        var a = new ContextFacade("a", root);
        a.Provide("fs", "old");

        a.Set("fs", "new");

        Assert.Equal("new", root.Get<string>("fs"));
    }

    // ── P2-29：fire-and-forget emit ──

    [Fact]
    public async Task EmitFireAndForget_returns_before_async_listener_completes()
    {
        var context = new ContextFacade("root");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = 0;
        context.SubscribeParallel<Ping>(async _ =>
        {
            await gate.Task;
            completed++;
        });

        context.EmitFireAndForget(new Ping("x")); // 不等待异步监听（Cordis emit 语义）

        Assert.Equal(0, completed); // 发布即刻返回（gate 未放行）
        gate.TrySetResult();
        await WaitForAsync(() => completed == 1);
        Assert.Equal(1, completed); // 监听最终执行
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail("condition not met within timeout");
            }

            await Task.Delay(20);
        }
    }
}
