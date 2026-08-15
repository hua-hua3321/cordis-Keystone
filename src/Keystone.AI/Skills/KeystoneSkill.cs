using Microsoft.Agents.AI;

namespace Keystone.AI.Skills;

/// <summary>
/// SEP-2640 技能（ADR-0008 决策 3）：以 skill:// URI 承载的 MAF AgentSkill。
/// Frontmatter 携带技能名与描述（从 URI 派生），资源按需实现。
/// </summary>
public sealed class KeystoneSkill : AgentSkill
{
    private readonly AgentSkillFrontmatter _frontmatter;

    public KeystoneSkill(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        Uri = uri;
        var stem = (uri.Split('/').LastOrDefault() ?? "skill").Split('.')[0];
        var name = new string(stem.ToLowerInvariant().Where(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c)).ToArray());
        if (name.Length == 0)
        {
            name = "skill";
        }

        _frontmatter = new AgentSkillFrontmatter(
            name.Length > 64 ? name[..64] : name,
            $"SEP-2640 skill package: {uri}");
    }

    /// <summary>skill:// URI（10-plugin-sdk §6 manifest skills 字段）。</summary>
    public string Uri { get; }

    public override AgentSkillFrontmatter Frontmatter => _frontmatter;

    /// <summary>技能内容（SEP-2640 SKILL.md 文本；URI 承载时返回占位描述）。</summary>
    public override ValueTask<string> GetContentAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult($"# {Frontmatter.Name}\n\nSEP-2640 skill package: {Uri}");

    public override ValueTask<AgentSkillResource?> GetResourceAsync(string name, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<AgentSkillResource?>(null);
}
