using System.Runtime.CompilerServices;

namespace Keystone.Runtime.Effects;

/// <summary>
/// effect 注册表实现：AsyncLocal 栈维护"当前执行中的 effect"，回调内注册挂为子节点；
/// 逆序收敛（后注册先执行），DisposeAll 幂等。
/// </summary>
public sealed class EffectRegistry : IEffectRegistry
{
    private static readonly AsyncLocal<EffectNode?> Current = new();
    private readonly Lock _lock = new();
    private readonly List<EffectNode> _roots = [];

    public IDisposable Register(Func<Task> disposer, string? label = null, [CallerMemberName] string? callerMember = null)
    {
        ArgumentNullException.ThrowIfNull(disposer);

        var node = new EffectNode(disposer, label, callerMember);
        var parent = Current.Value;
        if (parent is null)
        {
            lock (_lock)
            {
                _roots.Add(node);
            }
        }
        else
        {
            lock (parent.Children)
            {
                parent.Children.Add(node);
            }
        }

        return new Registration(node);
    }

    public IReadOnlyList<EffectMeta> GetEffects()
    {
        List<EffectNode> snapshot;
        lock (_lock)
        {
            snapshot = [.. _roots];
        }

        return snapshot.Select(ToMeta).ToList();
    }

    public async Task DisposeAllAsync()
    {
        List<EffectNode> snapshot;
        lock (_lock)
        {
            snapshot = [.. _roots]; // 诊断树保留（GetEffects 可追溯已执行的 effect）；Disposed 标记防重复执行
        }

        for (var i = snapshot.Count - 1; i >= 0; i--)
        {
            await RunDisposerAsync(snapshot[i]).ConfigureAwait(false);
        }
    }

    private static EffectMeta ToMeta(EffectNode node)
        => new(node.Label, node.CallerMember, node.Children.Select(ToMeta).ToList());

    private static async Task RunDisposerAsync(EffectNode node)
    {
        if (!node.TryMarkDisposed()) // Interlocked：手动 Dispose 与 DisposeAll 并发时恰执行一次
        {
            return;
        }
        var previous = Current.Value;
        Current.Value = node; // 回调内注册 → 挂本节点子列表
        try
        {
            await node.Disposer().ConfigureAwait(false);
        }
        finally
        {
            Current.Value = previous;
        }

        List<EffectNode> children;
        lock (node.Children)
        {
            children = [.. node.Children];
        }

        for (var i = children.Count - 1; i >= 0; i--)
        {
            await RunDisposerAsync(children[i]).ConfigureAwait(false);
        }
    }

    private sealed class EffectNode
    {
        private int _disposed;

        public EffectNode(Func<Task> disposer, string? label, string? callerMember)
        {
            Disposer = disposer;
            Label = label ?? "effect";
            CallerMember = callerMember;
        }

        public Func<Task> Disposer { get; }

        public string Label { get; }

        public string? CallerMember { get; }

        public List<EffectNode> Children { get; } = [];

        public bool Disposed => Volatile.Read(ref _disposed) != 0;

        /// <summary>CAS 置位：true = 本次调用赢得执行权（幂等保障）。</summary>
        public bool TryMarkDisposed() => Interlocked.Exchange(ref _disposed, 1) == 0;
    }

    private sealed class Registration : IDisposable
    {
        private readonly EffectNode _node;
        private bool _disposed;

        public Registration(EffectNode node)
        {
            _node = node;
        }

        /// <summary>
        /// D-9（19 号审计 CF-7，对齐 Cordis fiber.ts:427-442）：句柄 Dispose = **执行** disposer 一次
        /// （`using var h = ctx.Effect(cleanup)` 的 C# 惯例），而非仅取消。幂等——
        /// 与 DisposeAllAsync 竞争时 Interlocked 保证恰执行一次。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            RunDisposerAsync(_node).GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }
    }
}
