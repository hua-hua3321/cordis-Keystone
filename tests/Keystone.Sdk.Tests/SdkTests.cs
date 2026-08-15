using Keystone.Runtime.Context;
using Keystone.Sdk.Manifest;
using Keystone.Sdk.Timers;

namespace Keystone.Sdk.Tests;

public class TimerExtensionsTests
{
    [Fact]
    public async Task SetTimeout_fires_after_delay()
    {
        var ctx = new ContextFacade("test");
        var fired = new TaskCompletionSource();
        ctx.SetTimeout(() =>
        {
            fired.TrySetResult();
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(20));

        await fired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(fired.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Dispose_cancels_pending_timer()
    {
        var ctx = new ContextFacade("test");
        var fired = false;
        var handle = ctx.SetTimeout(() =>
        {
            fired = true;
            return Task.CompletedTask;
        }, TimeSpan.FromSeconds(30));

        await handle.DisposeAsync();
        await Task.Delay(50);

        Assert.False(fired, "dispose 后计时器不触发（对齐 Cordis plugin-timer 回收语义）");
    }

    [Fact]
    public async Task SetInterval_fires_repeatedly()
    {
        var ctx = new ContextFacade("test");
        var count = 0;
        var handle = ctx.SetInterval(() =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(10));

        await Task.Delay(55);
        await handle.DisposeAsync();

        Assert.True(count >= 3, $"interval 应重复触发，实际 {count} 次");
    }

    [Fact]
    public async Task Effect_registration_makes_timer_quiesce_safe()
    {
        var ctx = new ContextFacade("test");
        var fired = false;
        ctx.SetTimeout(() =>
        {
            fired = true;
            return Task.CompletedTask;
        }, TimeSpan.FromSeconds(30));

        // 插件卸载 = quiesce：effect 逆序收敛 → 计时器取消（10 §4 N3）
        await ctx.DisposeEffectsAsync();
        await Task.Delay(50);

        Assert.False(fired);
    }

    [Fact]
    public async Task Debounce_coalesces_rapid_calls()
    {
        var ctx = new ContextFacade("test");
        var count = 0;
        var handle = ctx.Debounce(() =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(40));

        for (var i = 0; i < 5; i++)
        {
            handle.Trigger();
            await Task.Delay(5);
        }

        await Task.Delay(100); // 防抖窗口过后触发一次
        await handle.DisposeAsync();

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Throttle_limits_to_one_per_window()
    {
        var ctx = new ContextFacade("test");
        var count = 0;
        var handle = ctx.Throttle(() =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(50));

        for (var i = 0; i < 5; i++)
        {
            handle.Trigger();
            await Task.Delay(5);
        }

        await Task.Delay(150);
        await handle.DisposeAsync();

        Assert.Equal(1, count); // 窗口内多次触发仅首次执行（节流语义）
    }
}

public class ManifestSchemaValidatorTests
{
    [Fact]
    public void Valid_manifest_passes()
    {
        var manifest = new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            "fs", "1.0.0", "Fs.cs", ["cordis-runtime"], ["fs"], [],
            Skills: ["skill://git-workflow/SKILL.md"]);

        ManifestSchemaValidator.Validate(manifest); // 不应抛
    }

    [Fact]
    public void Invalid_skill_uri_fails_fast()
    {
        var manifest = new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            "fs", "1.0.0", "Fs.cs", ["cordis-runtime"], ["fs"], [],
            Skills: ["not-a-skill-uri"]);

        var exception = Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => ManifestSchemaValidator.Validate(manifest));

        Assert.Equal(Keystone.Core.Errors.ErrorCode.ConfigValidationFailed, exception.Code);
    }

    [Fact]
    public void Missing_required_fields_fail()
    {
        var manifest = new Keystone.Runtime.Plugins.Manifest.PluginManifest(
            "fs", "", "Fs.cs", ["cordis-runtime"], ["fs"], []);

        Assert.Throws<Keystone.Core.Errors.KeystoneException>(() => ManifestSchemaValidator.Validate(manifest));
    }
}
