using Keystone.Runtime.Context;

namespace Keystone.Runtime.Pipeline;

/// <summary>
/// 管道构建器：运行期插入中间件（H2 动态组合入口）→ <see cref="Build"/> 冻结快照 →
/// 执行。内部组合用形状 B 闭包（List 反向包装，04 §2 分工澄清）。
/// </summary>
public sealed class PipelineBuilder
{
    private readonly List<IMiddleware> _middlewares = [];
    private RequestDelegate? _terminal;

    /// <summary>运行期插入中间件（动态管道组合：插入 → Build 组合 → 执行）。</summary>
    public PipelineBuilder AddMiddleware(IMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _middlewares.Add(middleware);
        return this;
    }

    /// <summary>设置内置执行器（管道终点；缺省为空操作）。</summary>
    public PipelineBuilder SetTerminal(RequestDelegate terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        _terminal = terminal;
        return this;
    }

    /// <summary>冻结当前中间件快照为不可变管道（此后 AddMiddleware 不影响已构建实例）。</summary>
    public IPipeline Build()
    {
        return new Pipeline([.. _middlewares], _terminal ?? (_ => Task.CompletedTask));
    }

    private sealed class Pipeline : IPipeline
    {
        private readonly RequestDelegate _chain;

        public Pipeline(IReadOnlyList<IMiddleware> middlewares, RequestDelegate terminal)
        {
            // Order 升序（LINQ OrderBy 稳定：相同 Order 保持注册序），然后反向包装成链
            var ordered = middlewares.OrderBy(m => m.Order).ToList();
            RequestDelegate next = terminal;
            for (var i = ordered.Count - 1; i >= 0; i--)
            {
                var middleware = ordered[i];
                var inner = next;
                next = ctx => middleware.InvokeAsync(ctx, inner); // 形状 B 内部组合
            }

            _chain = next;
        }

        public Task InvokeAsync(IPluginContext context) => _chain(context);
    }
}
