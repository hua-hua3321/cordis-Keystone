using Keystone.Runtime.Plugins.Manifest;
using Microsoft.Agents.AI;

namespace Keystone.AI.Skills;

/// <summary>
/// 技能注册表（ADR-0008 决策 3）：manifest skills（skill:// URI）→ MAF AgentSkillsSource
/// （AgentInMemorySkillsSource 承载），供 AgentMcpSkillsSource 等价消费。
/// </summary>
public static class SkillRegistry
{
    /// <summary>从 manifest 构建技能源（skills 字段 → AgentSkill 列表）。</summary>
    public static AgentSkillsSource FromManifest(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var skills = (manifest.Skills ?? []).Select(uri => new KeystoneSkill(uri)).ToList();
        return new AgentInMemorySkillsSource(skills);
    }
}
