namespace Keystone.Runtime.Plugins.Loading;

/// <summary>
/// 本地文件获取端（DC-19，ADR-0001 决策 2 初始形态）：manifest.Main 相对根目录解析，
/// 缺省回退 {root}/{id}/{main} 约定布局（多插件仓库形态）。
/// </summary>
public sealed class LocalPluginSource(params string[] roots) : IPluginSource
{
    /// <summary>根目录列表（CA-2：插件源 watcher 监听面暴露）。</summary>
    public string[] Roots { get; } = [.. roots];

    public async Task<PluginSource> FetchAsync(Manifest.PluginManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var candidates = Roots
            .Select(root => Path.Combine(root, manifest.Main))
            .Concat(Roots.Select(root => Path.Combine(root, manifest.Id, manifest.Main)));

        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null)
        {
            throw new Keystone.Core.Errors.KeystoneException(
                Keystone.Core.Errors.ErrorCode.ConfigProviderFailed,
                $"plugin source not found for '{manifest.Id}' (main: {manifest.Main}) under any configured root");
        }

        var code = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return new PluginSource(manifest.Id, code);
    }
}
