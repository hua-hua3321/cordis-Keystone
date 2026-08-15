using Keystone.Runtime.Persistence;

namespace Keystone.Hosting.Tests;

/// <summary>
/// DC-18 宿主接线：RetentionPolicy + PruneInterval → 宿主启动定时 Prune（随宿主启停）。
/// </summary>
public class HostRetentionTests
{
    private sealed class RecordingStore : IEventStore
    {
        public volatile int PruneCalls;

        public Task<long> AppendAsync(StoredFact fact, CancellationToken cancellationToken = default)
            => Task.FromResult(0L);

        public Task<long> GetLastSequenceAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0L);

        public IAsyncEnumerable<StoredFact> ReplayAsync(ReplayQuery query, CancellationToken cancellationToken = default)
            => ReplayCore(cancellationToken);

        private static async IAsyncEnumerable<StoredFact> ReplayCore(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield break;
        }

        public Task<int> PruneAsync(RetentionPolicy policy, CancellationToken cancellationToken = default)
        {
            PruneCalls++;
            return Task.FromResult(0);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Host_starts_scheduled_prune_and_stops_on_dispose()
    {
        var store = new RecordingStore();
        var host = new KeystoneHost(new KeystoneHostOptions
        {
            ManifestProvider = _ => new Keystone.Runtime.Plugins.Manifest.PluginManifest(
                "x", "1.0.0", "X.cs", [], [], []),
            SourceProvider = _ => new Keystone.Runtime.Plugins.Loading.PluginSource(
                "x", Keystone.Hosting.Tests.HostTestSources.DependentSource),
            EventStore = store,
            RetentionPolicy = new RetentionPolicy(MaxEvents: 10),
            PruneInterval = TimeSpan.FromMilliseconds(20),
        });
        await host.StartAsync("");

        var deadline = Task.Delay(TimeSpan.FromSeconds(5));
        while (store.PruneCalls < 2 && !deadline.IsCompleted)
        {
            await Task.Delay(20);
        }

        var callsAtStop = store.PruneCalls;
        await host.DisposeAsync();
        await Task.Delay(80); // 关闭后不再增长

        Assert.True(callsAtStop >= 2); // 宿主在跑 → 周期执行
        Assert.Equal(callsAtStop, store.PruneCalls); // 宿主停 → 调度停
    }
}
