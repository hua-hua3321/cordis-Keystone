namespace Keystone.Runtime.Plugins.Manifest;

/// <summary>
/// 插件清单（02-plugin-model §1 / ADR-0007 决策 2 / 10 §6）：
/// <see cref="Dependencies"/>（程序集编译白名单）与 <see cref="Provides"/>/<see cref="Inject"/>
/// （服务级运行时依赖）是两个正交维度；<see cref="Skills"/> = SEP-2640 技能包（ADR-0008 决策 3）。
/// </summary>
public sealed record PluginManifest(
    string Id,
    string Version,
    string Main,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> Provides,
    IReadOnlyList<string> Inject,
    IReadOnlyList<string>? Skills = null);
