using Keystone.Core.Errors;
using Keystone.Runtime.Context;

namespace Keystone.Runtime.Tests;

/// <summary>
/// KeyedServiceStore 值层组件（18 §2 CA-1 第 1 步）：
/// ConcurrentDictionary&lt;(name, realm), (value, ownerId)&gt; 热路径无锁读 + Lock 复合写（属主校验+写，G14 防 TOCTOU）
/// + 出锁批量通知（scope 合并，对齐 Cordis notify(names[])）+ 可用 = ContainsKey + Provide 返回删键 disposer。
/// </summary>
public class KeyedServiceStoreTests
{
    private static KeyedServiceStore NewStore() => new();

    [Fact]
    public void Provide_then_IsAvailable_and_TryGet()
    {
        var store = NewStore();
        using var disposer = store.Provide("fs", string.Empty, "value-a", "plugin-a");

        Assert.True(store.IsAvailable("fs", string.Empty));
        Assert.Equal("value-a", store.TryGet<string>("fs", string.Empty));
    }

    [Fact]
    public void Same_name_in_different_realms_coexist()
    {
        var store = NewStore();
        using var shared = store.Provide("fs", string.Empty, "shared-value", "plugin-a");
        using var group = store.Provide("fs", "#group-1", "group-value", "plugin-b");
        using var labeled = store.Provide("fs", "@label-x", "label-value", "plugin-c");

        Assert.Equal("shared-value", store.TryGet<string>("fs", string.Empty));
        Assert.Equal("group-value", store.TryGet<string>("fs", "#group-1"));
        Assert.Equal("label-value", store.TryGet<string>("fs", "@label-x"));
        Assert.False(store.IsAvailable("fs", "#group-2"));
        Assert.Null(store.TryGet<string>("fs", "#group-2"));
    }

    [Fact]
    public void Cross_owner_provide_rejected()
    {
        var store = NewStore();
        using var first = store.Provide("fs", string.Empty, "value", "plugin-a");

        var exception = Assert.Throws<KeystoneException>(
            () => store.Provide("fs", string.Empty, "other", "plugin-b"));
        Assert.Equal(ErrorCode.ServiceAlreadyRegistered, exception.Code);
        // 原值不被破坏
        Assert.Equal("value", store.TryGet<string>("fs", string.Empty));
    }

    [Fact]
    public void Same_owner_second_provide_throws_set_updates()
    {
        // D-6（P68，对齐 reflect.ts:289-291）：二次 Provide 报错；更新走 Set
        var store = NewStore();
        using var disposer = store.Provide("fs", string.Empty, "old", "plugin-a");

        Assert.Throws<KeystoneException>(() => store.Provide("fs", string.Empty, "new", "plugin-a"));
        Assert.Equal("old", store.TryGet<string>("fs", string.Empty)); // 原值保持

        store.Set("fs", string.Empty, "new", "plugin-a");
        Assert.Equal("new", store.TryGet<string>("fs", string.Empty));
    }

    [Fact]
    public void Remove_requires_owner()
    {
        var store = NewStore();
        using var disposer = store.Provide("fs", "#g", "value", "plugin-a");

        Assert.Throws<KeystoneException>(() => store.Remove("fs", "#g", "plugin-b"));
        Assert.True(store.IsAvailable("fs", "#g"));

        store.Remove("fs", "#g", "plugin-a");
        Assert.False(store.IsAvailable("fs", "#g"));
    }

    [Fact]
    public void Remove_absent_key_is_idempotent_and_silent()
    {
        var store = NewStore();
        var changes = new List<ServiceKey>();
        using var subscription = store.Subscribe(changes.AddRange);

        store.Remove("never-provided", string.Empty, "plugin-a");

        Assert.Empty(changes);
    }

    [Fact]
    public void Get_missing_throws_GatingServiceNotFound()
    {
        var store = NewStore();
        var exception = Assert.Throws<KeystoneException>(() => store.Get<string>("fs", string.Empty));
        Assert.Equal(ErrorCode.GatingServiceNotFound, exception.Code);
    }

    [Fact]
    public void Provide_disposer_removes_and_notifies()
    {
        var store = NewStore();
        var changes = new List<ServiceKey>();
        using var subscription = store.Subscribe(changes.AddRange);

        var disposer = store.Provide("fs", string.Empty, "value", "plugin-a");
        disposer.Dispose();

        Assert.False(store.IsAvailable("fs", string.Empty));
        Assert.Contains(new ServiceKey("fs", string.Empty), changes);
    }

    [Fact]
    public void Disposer_after_manual_remove_is_idempotent()
    {
        var store = NewStore();
        var changes = new List<ServiceKey>();
        using var subscription = store.Subscribe(changes.AddRange);

        var disposer = store.Provide("fs", string.Empty, "value", "plugin-a");
        store.Remove("fs", string.Empty, "plugin-a");
        changes.Clear();

        disposer.Dispose();

        Assert.Empty(changes);
    }

    [Fact]
    public void Notification_fires_outside_lock_cross_thread_read_succeeds()
    {
        var store = NewStore();
        var readCompleted = new ManualResetEventSlim(false);

        using var subscription = store.Subscribe(_ =>
        {
            // 跨线程读：若通知在锁内发出，此处持锁线程阻塞等待的 Task.Run 需要同一把锁 → 死锁 → 超时失败
            var ok = Task.Run(() => store.IsAvailable("fs", string.Empty)).Wait(TimeSpan.FromSeconds(5));
            if (ok)
            {
                readCompleted.Set();
            }
        });

        using var disposer = store.Provide("fs", string.Empty, "value", "plugin-a");

        Assert.True(readCompleted.Wait(TimeSpan.FromSeconds(5)), "回调内跨线程读必须不死锁（通知须出锁）");
    }

    [Fact]
    public void Provide_without_scope_notifies_each_provide()
    {
        var store = NewStore();
        var changes = new List<ServiceKey>();
        using var subscription = store.Subscribe(changes.AddRange);

        using var d1 = store.Provide("fs", string.Empty, "a", "p1");
        using var d2 = store.Provide("cache", string.Empty, "b", "p1");

        Assert.Equal(2, changes.Count);
        Assert.Contains(new ServiceKey("fs", string.Empty), changes);
        Assert.Contains(new ServiceKey("cache", string.Empty), changes);
    }

    [Fact]
    public void Notify_scope_coalesces_provides_into_single_change()
    {
        var store = NewStore();
        var changeSets = new List<IReadOnlyList<ServiceKey>>();
        using var subscription = store.Subscribe(changeSets.Add);

        IDisposable d1, d2, d3;
        using (store.BeginNotifyScope())
        {
            d1 = store.Provide("fs", string.Empty, "a", "p1");
            d2 = store.Provide("cache", string.Empty, "b", "p1");
            d3 = store.Provide("llm", "@label-x", "c", "p1");
        }

        var single = Assert.Single(changeSets);
        Assert.Equal(3, single.Count);
        d1.Dispose(); d2.Dispose(); d3.Dispose();
        Assert.Contains(new ServiceKey("fs", string.Empty), single);
        Assert.Contains(new ServiceKey("cache", string.Empty), single);
        Assert.Contains(new ServiceKey("llm", "@label-x"), single);
    }

    [Fact]
    public void Nested_scopes_merge_into_outer()
    {
        var store = NewStore();
        var changeSets = new List<IReadOnlyList<ServiceKey>>();
        using var subscription = store.Subscribe(changeSets.Add);

        IDisposable d1, d2, d3;
        using (var outer = store.BeginNotifyScope())
        {
            d1 = store.Provide("fs", string.Empty, "a", "p1");
            using (var inner = store.BeginNotifyScope())
            {
                d2 = store.Provide("cache", string.Empty, "b", "p1");
                inner.Dispose();
                Assert.Empty(changeSets); // 内层 dispose 不发（并入外层）
            }

            d3 = store.Provide("llm", string.Empty, "c", "p1");
        } // 外层 dispose → 一次性发全部

        var single = Assert.Single(changeSets);
        Assert.Equal(3, single.Count);
        d1.Dispose(); d2.Dispose(); d3.Dispose();
    }

    [Fact]
    public void Remove_inside_scope_is_also_coalesced()
    {
        var store = NewStore();
        var changeSets = new List<IReadOnlyList<ServiceKey>>();
        using var subscription = store.Subscribe(changeSets.Add);
        using var d1 = store.Provide("fs", string.Empty, "a", "p1");
        changeSets.Clear();

        using (store.BeginNotifyScope())
        {
            store.Remove("fs", string.Empty, "p1");
        }

        var single = Assert.Single(changeSets);
        Assert.Contains(new ServiceKey("fs", string.Empty), single);
    }

    [Fact]
    public void AvailableServices_partitions_by_realm()
    {
        var store = NewStore();
        using var d1 = store.Provide("fs", string.Empty, "a", "p1");
        using var d2 = store.Provide("cache", string.Empty, "b", "p1");
        using var d3 = store.Provide("fs", "#group-1", "c", "p2");
        using var d4 = store.Provide("llm", "@label-x", "d", "p3");

        Assert.Equal(["cache", "fs"], store.AvailableServices(string.Empty).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        Assert.Equal(["fs"], store.AvailableServices("#group-1"));
        Assert.Equal(["llm"], store.AvailableServices("@label-x"));
        Assert.Empty(store.AvailableServices("#other"));
    }

    [Fact]
    public void Subscribe_dispose_stops_delivery()
    {
        var store = NewStore();
        var changes = new List<ServiceKey>();
        var subscription = store.Subscribe(changes.AddRange);
        subscription.Dispose();

        using var disposer = store.Provide("fs", string.Empty, "value", "plugin-a");

        Assert.Empty(changes);
    }

    [Fact]
    public void Concurrent_provides_from_many_owners_are_serialized_correctly()
    {
        var store = NewStore();
        const int concurrency = 16;

        var results = new List<Exception>();
        using var barrier = new Barrier(concurrency);
        var threads = Enumerable.Range(0, concurrency).Select(i => new Thread(() =>
        {
            barrier.SignalAndWait();
            try
            {
                store.Provide("hot-service", string.Empty, $"value-{i}", $"owner-{i}"); // 不即时删：让属主校验真正竞写
            }
            catch (Exception ex)
            {
                lock (results)
                {
                    results.Add(ex);
                }
            }
        })).ToArray();

        foreach (var t in threads)
        {
            t.Start();
        }

        foreach (var t in threads)
        {
            t.Join();
        }

        // 并发竞写：恰好 1 个成功属主，其余 ServiceAlreadyRegistered；最终值属于唯一成功者
        Assert.Equal(concurrency - 1, results.Count);
        Assert.All(results, ex => Assert.Equal(ErrorCode.ServiceAlreadyRegistered, ((KeystoneException)ex).Code));
        var winner = store.TryGet<string>("hot-service", string.Empty);
        Assert.StartsWith("value-", winner);
        Assert.True(store.IsAvailable("hot-service", string.Empty));
    }
}
