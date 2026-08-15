using Keystone.Core.Errors;

namespace Keystone.Runtime.Plugins.Manifest;

/// <summary>
/// manifest 校验器（ADR-0007 决策 2 影响）：启动期 fail-fast——
/// inject 声明的服务必须被某插件提供（可达性）、依赖图无环、字段非空。
/// </summary>
public static class ManifestValidator
{
    public static void Validate(IReadOnlyList<PluginManifest> plugins)
    {
        ArgumentNullException.ThrowIfNull(plugins);

        foreach (var plugin in plugins)
        {
            if (string.IsNullOrWhiteSpace(plugin.Id)
                || string.IsNullOrWhiteSpace(plugin.Version)
                || string.IsNullOrWhiteSpace(plugin.Main))
            {
                throw new ArgumentException($"plugin manifest must have non-empty id/version/main: {plugin.Id}", nameof(plugins));
            }
        }

        ValidateReachability(plugins);
        ValidateNoCycles(plugins);
    }

    private static void ValidateReachability(IReadOnlyList<PluginManifest> plugins)
    {
        var provided = plugins.SelectMany(p => p.Provides).ToHashSet(StringComparer.Ordinal);
        foreach (var plugin in plugins)
        {
            foreach (var service in plugin.Inject)
            {
                if (!provided.Contains(service))
                {
                    throw new KeystoneException(
                        ErrorCode.GatingServiceNotFound,
                        $"plugin '{plugin.Id}' injects service '{service}' which is not provided by any plugin");
                }
            }
        }
    }

    /// <summary>Kahn 拓扑排序检测依赖环（inject 服务 → 提供方插件）。</summary>
    private static void ValidateNoCycles(IReadOnlyList<PluginManifest> plugins)
    {
        var providersByService = plugins
            .SelectMany(p => p.Provides.Select(s => (Service: s, Plugin: p)))
            .ToLookup(x => x.Service, x => x.Plugin, StringComparer.Ordinal);

        var dependencies = plugins.ToDictionary(
            p => p.Id,
            p => p.Inject
                .SelectMany(s => providersByService[s])
                .Where(provider => !string.Equals(provider.Id, p.Id, StringComparison.Ordinal))
                .Select(provider => provider.Id)
                .ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);

        // Kahn 拓扑排序：入度 = 该节点依赖的前置数；前置先出队
        var inDegree = dependencies.ToDictionary(kv => kv.Key, kv => kv.Value.Count, StringComparer.Ordinal);
        var queue = new Queue<string>(dependencies.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key));
        var visited = 0;
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            visited++;
            foreach (var dependent in dependencies.Where(kv => kv.Value.Contains(id)).Select(kv => kv.Key))
            {
                if (--inDegree[dependent] == 0)
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        if (visited != plugins.Count)
        {
            throw new KeystoneException(ErrorCode.GatingCircularDependency, "plugin dependency graph contains a cycle");
        }
    }
}
