using System.Runtime.CompilerServices;
using Keystone.Runtime.Effects;
using Keystone.Runtime.Events;
using Microsoft.Extensions.Logging;

namespace Keystone.Runtime.Context;

/// <summary>
/// 运行时上下文面（03）：作用域链（Parent/Root）、事件总线、服务存储、Effect、日志、拦截器。
/// 一个能力域 = 一个 actor = 一个 context（03 §1）。
/// </summary>
public interface IContext
{
    /// <summary>上下文名（诊断/日志 category 缺省）。</summary>
    string Name { get; }

    /// <summary>父 context（scope 链；事件过滤按此链判定祖先，03 §5）。</summary>
    IContext? Parent { get; }

    /// <summary>根 context（沿 Parent 链顶端）。</summary>
    IContext Root { get; }

    /// <summary>基础路径（L6；P6 配置层接线）。</summary>
    string BaseUrl { get; }

    /// <summary>事件总线（五分发模式，ADR-0006）。</summary>
    IEventBus Events { get; }

    /// <summary>服务存储（rebind/属主校验，03 §2.1/§2.3）。</summary>
    IServiceStore Services { get; }

    /// <summary>命名日志（M2；category 缺省 = 上下文名）。</summary>
    ILogger GetLogger(string? name = null);

    /// <summary>注册 effect disposer（M1；[CallerMemberName] 自动注入调用者信息）。</summary>
    IDisposable Effect(Func<Task> disposer, string? label = null, [CallerMemberName] string? callerMember = null);

    /// <summary>effect 诊断树（M1）。</summary>
    IReadOnlyList<EffectMeta> GetEffects();

    /// <summary>逆序收敛全部 effect disposer（quiesce 步骤，ADR-0005 决策 2）。</summary>
    Task DisposeEffectsAsync();

    /// <summary>注册门面拦截器（H3）。</summary>
    void AddInterceptor(IContextInterceptor interceptor);

    /// <summary>移除门面拦截器（H3）。</summary>
    void RemoveInterceptor(IContextInterceptor interceptor);
}
