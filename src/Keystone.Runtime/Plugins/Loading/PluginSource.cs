namespace Keystone.Runtime.Plugins.Loading;

/// <summary>插件源码单元（P5：内嵌/文件源码；编译进独立 ALC）。</summary>
public sealed record PluginSource(string Id, string Code);
