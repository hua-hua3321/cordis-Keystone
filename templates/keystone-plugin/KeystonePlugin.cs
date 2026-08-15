using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Keystone.Runtime.Context;
using Keystone.Runtime.Plugins.Lifecycle;

namespace KeystonePlugin;

/// <summary>
/// Keystone 插件骨架：实现 IPlugin（InitializeAsync/DisposeAsync）。
/// 配置经 schema 校验后注入（config 默认值已补齐）。
/// </summary>
public sealed class KeystonePlugin : IPlugin
{
    public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
    {
        // 提供服务：context.Provide("my-service", instance);
        // 订阅事件：context.Subscribe<MyEvent>(e => ...);
        // 计时器：context.SetTimeout(() => Task.CompletedTask, TimeSpan.FromSeconds(1));
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // 摘除自己注册的东西；回收由宿主 quiesce 收敛（ADR-0005）
        return Task.CompletedTask;
    }
}
