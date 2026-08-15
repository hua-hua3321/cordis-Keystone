namespace Keystone.Runtime.Plugins.Loading;

/// <summary>
/// 插件获取端抽象（DC-19，ADR-0001 决策 2）：按 manifest 获取插件源码。
/// 演进路径——本地文件（初始）→ 签名校验 → 远程分发：**只替换获取端实现，
/// 不改变编译/ALC/dispose 加载管线**（获取与加载解耦）。
/// </summary>
public interface IPluginSource
{
    /// <summary>获取插件源码单元（id + 源码文本）。</summary>
    Task<PluginSource> FetchAsync(Manifest.PluginManifest manifest, CancellationToken cancellationToken = default);
}
