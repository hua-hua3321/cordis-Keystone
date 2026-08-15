using Keystone.Runtime.Context;
using Keystone.Runtime.Pipeline;

namespace Keystone.Runtime.Tests;

public class PipelineTests
{
    [Fact]
    public async Task Middlewares_run_in_order_with_before_after_wrapping()
    {
        var order = new List<string>();
        var builder = new PipelineBuilder();
        builder.AddMiddleware(new RecordingMiddleware("a", order, 1));
        builder.AddMiddleware(new RecordingMiddleware("b", order, 2));
        builder.SetTerminal(ctx => { order.Add("terminal"); return Task.CompletedTask; });

        var pipeline = builder.Build();
        await pipeline.InvokeAsync(new ContextFacade("test"));

        Assert.Equal(["a-before", "b-before", "terminal", "b-after", "a-after"], order);
    }

    [Fact]
    public async Task Order_sorts_by_declared_order_and_stays_stable()
    {
        var order = new List<string>();
        var builder = new PipelineBuilder();
        builder.AddMiddleware(new RecordingMiddleware("second", order, 2));
        builder.AddMiddleware(new RecordingMiddleware("first", order, 1));

        var pipeline = builder.Build();
        await pipeline.InvokeAsync(new ContextFacade("test"));

        Assert.Equal(["first-before", "second-before", "second-after", "first-after"], order);
    }

    [Fact]
    public async Task Short_circuit_skips_rest_of_chain_and_terminal()
    {
        var order = new List<string>();
        var builder = new PipelineBuilder();
        builder.AddMiddleware(new ShortCircuitMiddleware(order));
        builder.AddMiddleware(new RecordingMiddleware("never", order, 2));
        builder.SetTerminal(ctx => { order.Add("terminal"); return Task.CompletedTask; });

        var pipeline = builder.Build();
        await pipeline.InvokeAsync(new ContextFacade("test"));

        Assert.Equal(["short-circuit"], order); // 不调 next：短路
    }

    [Fact]
    public async Task Exception_propagates_and_after_not_run()
    {
        var order = new List<string>();
        var builder = new PipelineBuilder();
        builder.AddMiddleware(new ThrowingMiddleware(order));
        builder.SetTerminal(ctx => { order.Add("terminal"); return Task.CompletedTask; });

        var pipeline = builder.Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.InvokeAsync(new ContextFacade("test")));
        Assert.DoesNotContain("terminal", order); // 异常中断，terminal 不执行
    }

    [Fact]
    public async Task Dynamic_insertion_composes_at_runtime()
    {
        // H2 机制验收：运行期插入节点 → 组合 → 执行（编程式挂载的底层）
        var order = new List<string>();
        var builder = new PipelineBuilder();
        builder.AddMiddleware(new RecordingMiddleware("a", order, 1));
        builder.SetTerminal(ctx => { order.Add("terminal"); return Task.CompletedTask; });

        var first = builder.Build();
        await first.InvokeAsync(new ContextFacade("test"));
        Assert.Equal(["a-before", "terminal", "a-after"], order);

        order.Clear();
        builder.AddMiddleware(new RecordingMiddleware("b", order, 2)); // 运行期插入
        var second = builder.Build();
        await second.InvokeAsync(new ContextFacade("test"));

        Assert.Equal(["a-before", "b-before", "terminal", "b-after", "a-after"], order);
    }

    [Fact]
    public async Task Built_pipeline_is_immutable_snapshot()
    {
        var order = new List<string>();
        var builder = new PipelineBuilder();
        builder.AddMiddleware(new RecordingMiddleware("a", order, 1));
        var snapshot = builder.Build();

        builder.AddMiddleware(new RecordingMiddleware("b", order, 2)); // 已构建管道不受影响（快照冻结）
        await snapshot.InvokeAsync(new ContextFacade("test"));

        Assert.Equal(["a-before", "a-after"], order); // 旧管道只含 a（原子替换语义，ADR-0003）
    }

    private sealed class RecordingMiddleware : IMiddleware
    {
        private readonly string _name;
        private readonly List<string> _order;
        private readonly int _orderValue;

        public RecordingMiddleware(string name, List<string> order, int orderValue)
        {
            _name = name;
            _order = order;
            _orderValue = orderValue;
        }

        public string Id => _name;

        public int Order => _orderValue;

        public async Task InvokeAsync(IPluginContext ctx, RequestDelegate next)
        {
            _order.Add($"{_name}-before");
            await next(ctx);
            _order.Add($"{_name}-after");
        }
    }

    private sealed class ShortCircuitMiddleware : IMiddleware
    {
        private readonly List<string> _order;

        public ShortCircuitMiddleware(List<string> order)
        {
            _order = order;
        }

        public string Id => "short";

        public int Order => 0;

        public Task InvokeAsync(IPluginContext ctx, RequestDelegate next)
        {
            _order.Add("short-circuit");
            return Task.CompletedTask; // 不调 next：短路
        }
    }

    private sealed class ThrowingMiddleware : IMiddleware
    {
        private readonly List<string> _order;

        public ThrowingMiddleware(List<string> order)
        {
            _order = order;
        }

        public string Id => "throw";

        public int Order => 0;

        public Task InvokeAsync(IPluginContext ctx, RequestDelegate next)
        {
            _order.Add("throw-before");
            throw new InvalidOperationException("boom");
        }
    }
}
