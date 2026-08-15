using System.Buffers.Binary;
using Keystone.Core.Serialization;

namespace Keystone.Runtime.Persistence;

/// <summary>
/// 本地文件事件存储（ADR-0009 默认实现）：帧格式 = 4 字节大端长度 + 序列化字节。
/// 序列化经 <see cref="IContractSerializer"/> 抽象（ADR-0004：默认 MessagePack，可注入 JSON 审计）。
/// append-only（FileMode.Append + 追加锁串行）；崩溃恢复 = 忽略损坏尾帧（完整前缀可恢复）；
/// 每次追加 FlushAsync（帧完整性）。
/// DC-18（ADR-0009 决策 3 保留策略）：Prune 配置 archivePath（构造参数）时被清事实
/// 先归档（同帧格式追加，可重放/审计）再从主文件移除；未配置 = 纯删除（原行为）。
/// </summary>
public sealed class FileEventStore : IEventStore
{
    private readonly string _path;
    private readonly string? _archivePath;
    private readonly IContractSerializer _serializer;
    private readonly SemaphoreSlim _appendLock = new(1, 1);
    private long _sequence;

    public FileEventStore(string path, IContractSerializer? serializer = null, string? archivePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _archivePath = archivePath;
        _serializer = serializer ?? new MessagePackContractSerializer();
        _sequence = LoadSequence();
    }

    public async Task<long> AppendAsync(StoredFact fact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fact);

        await _appendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _sequence++;
            var stored = fact with { Sequence = _sequence };
            var payload = _serializer.Serialize(stored);
            var header = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);

            await using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
            await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false); // 帧完整性（崩溃恢复前提）
            return _sequence;
        }
        finally
        {
            _appendLock.Release();
        }
    }

    public async IAsyncEnumerable<StoredFact> ReplayAsync(
        ReplayQuery query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!File.Exists(_path))
        {
            yield break;
        }

        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var header = new byte[4];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var headerRead = await stream.ReadAsync(header.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
            if (headerRead < 4)
            {
                yield break; // 完整帧边界
            }

            var length = BinaryPrimitives.ReadInt32BigEndian(header);
            if (length <= 0 || length > 64 * 1024 * 1024)
            {
                yield break; // 损坏帧（崩溃残留）
            }

            var payload = new byte[length];
            var payloadRead = await stream.ReadAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (payloadRead < length)
            {
                yield break; // 尾部损坏帧：丢弃（崩溃恢复语义）
            }

            var fact = _serializer.Deserialize<StoredFact>(payload);
            if (Match(query, fact))
            {
                yield return fact;
            }
        }
    }

    public Task<long> GetLastSequenceAsync(CancellationToken cancellationToken = default) => Task.FromResult(_sequence);

    public async Task<int> PruneAsync(RetentionPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var all = new List<StoredFact>();
        await foreach (var fact in ReplayAsync(new ReplayQuery(), cancellationToken).ConfigureAwait(false))
        {
            all.Add(fact);
        }

        var cutoff = policy.Ttl is { } ttl ? DateTimeOffset.UtcNow - ttl : (DateTimeOffset?)null;
        var kept = all.Where(f =>
            (!cutoff.HasValue || f.Timestamp >= cutoff.Value)
            && (policy.MaxEvents is not { } max || f.Sequence > _sequence - max)).ToList();
        var removed = all.Count - kept.Count;
        if (removed > 0)
        {
            // DC-18：归档被清事实（同帧格式追加——可重放/审计；配置 archivePath 才启用）
            if (_archivePath is not null)
            {
                var dropped = all.Where(f => !kept.Contains(f)).ToList();
                await using (var archive = new FileStream(_archivePath, FileMode.Append, FileAccess.Write, FileShare.None))
                {
                    foreach (var fact in dropped)
                    {
                        var archivePayload = _serializer.Serialize(fact);
                        var archiveHeader = new byte[4];
                        BinaryPrimitives.WriteInt32BigEndian(archiveHeader, archivePayload.Length);
                        await archive.WriteAsync(archiveHeader, cancellationToken).ConfigureAwait(false);
                        await archive.WriteAsync(archivePayload, cancellationToken).ConfigureAwait(false);
                    }

                    await archive.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            var tmp = _path + ".prune.tmp";
            await using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                foreach (var fact in kept)
                {
                    var payload = _serializer.Serialize(fact);
                    var header = new byte[4];
                    BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
                    await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
                    await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                }

                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tmp, _path, overwrite: true);
            _sequence = kept.Count > 0 ? kept.Max(f => f.Sequence) : 0;
        }

        return removed;
    }

    public async ValueTask DisposeAsync()
    {
        _appendLock.Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private long LoadSequence()
    {
        if (!File.Exists(_path))
        {
            return 0;
        }

        long last = 0;
        using (var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var header = new byte[4];
            while (true)
            {
                var headerRead = stream.Read(header, 0, 4);
                if (headerRead < 4)
                {
                    break;
                }

                var length = BinaryPrimitives.ReadInt32BigEndian(header);
                if (length <= 0 || length > 64 * 1024 * 1024)
                {
                    break;
                }

                var payload = new byte[length];
                var payloadRead = stream.Read(payload, 0, length);
                if (payloadRead < length)
                {
                    break; // 损坏尾（崩溃残留）：忽略
                }

                last = _serializer.Deserialize<StoredFact>(payload).Sequence;
            }
        }

        return last;
    }

    private static bool Match(ReplayQuery query, StoredFact fact)
        => (!query.TaskId.HasValue || fact.TaskId == query.TaskId)
            && (query.Capability is null || string.Equals(fact.Capability, query.Capability, StringComparison.Ordinal))
            && fact.Sequence > query.AfterSequence
            && (!query.From.HasValue || fact.Timestamp >= query.From)
            && (!query.To.HasValue || fact.Timestamp <= query.To);
}
