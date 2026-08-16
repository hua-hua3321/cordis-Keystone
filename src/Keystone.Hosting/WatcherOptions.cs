namespace Keystone.Hosting;

/// <summary>文件监听防抖窗口（P71-T2 硬编码入配置面）：多次变更合并一次回调的合并窗口。</summary>
public sealed class WatcherOptions
{
    /// <summary>配置文件监听防抖（默认 100ms——编辑器连发保存合并一次重读；
    /// 想更快响应热重载可调小，写入频繁的环境可调大）。</summary>
    public TimeSpan ConfigFileDebounce { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>插件源文件监听防抖（默认 100ms）。</summary>
    public TimeSpan PluginFileDebounce { get; set; } = TimeSpan.FromMilliseconds(100);
}
