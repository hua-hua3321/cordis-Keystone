using System.Reflection;
using Keystone.AI.Skills;
using Keystone.AI.Workflows;
using Keystone.Core.Contracts;
using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.AI.Tests;

public class ArchitectureTests
{
    public static TheoryData<Assembly> CoreAssemblies => new()
    {
        typeof(Keystone.Core.KeystoneSettings).Assembly,
        typeof(Keystone.Config.Entries.EntryOptions).Assembly,
        typeof(Keystone.Runtime.Context.IContext).Assembly,
        typeof(Keystone.Hosting.KeystoneHost).Assembly,
        typeof(Keystone.Sdk.Timers.ITimerHandle).Assembly,
    };

    [Theory]
    [MemberData(nameof(CoreAssemblies))]
    public void Core_assemblies_do_not_reference_MAF(Assembly assembly)
    {
        // 验收 1：单向依赖（ADR-0008 决策 1）——核心程序集绝不引用 Microsoft.Agents
        var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name);
        var mafRefs = referenced.Where(n => n?.StartsWith("Microsoft.Agents", StringComparison.Ordinal) == true).ToList();

        Assert.Empty(mafRefs);
    }
}

public class WorkflowBridgeTests
{
    private static TaskEnvelope Envelope(Guid? parent = null) => new()
    {
        TaskId = Guid.NewGuid(),
        ParentTaskId = parent,
        Capability = "fs",
        Operation = "read",
        PayloadBytes = [],
    };

    [Fact]
    public async Task FanOut_preserves_task_and_parent_ids_across_branches()
    {
        // O2 验收：fan-out 分支收到 TaskId/ParentTaskId 原样（层级不稀释）
        var bridge = new WorkflowBridge();
        var original = Envelope(parent: Guid.NewGuid());
        var seen = new List<(Guid TaskId, Guid? Parent)>();
        var branches = Enumerable.Range(0, 3).Select(_ =>
            (Func<TaskEnvelope, Task<TaskResultEnvelope>>)(request =>
            {
                seen.Add((request.TaskId, request.ParentTaskId));
                return Task.FromResult(new TaskResultEnvelope { TaskId = request.TaskId, Succeeded = true, Type = TaskResultType.Completed });
            })).ToList();

        var results = await bridge.FanOutAsync(original, branches, CancellationToken.None);

        Assert.Equal(3, results.Count);
        Assert.All(seen, s =>
        {
            Assert.Equal(original.TaskId, s.TaskId);        // TaskId 贯穿
            Assert.Equal(original.ParentTaskId, s.Parent);  // 父层级保留
        });
    }

    [Fact]
    public async Task FanIn_all_success_aggregates_to_success()
    {
        var bridge = new WorkflowBridge();
        var original = Envelope();
        var results = new List<TaskResultEnvelope>
        {
            new() { TaskId = original.TaskId, Succeeded = true, Type = TaskResultType.Completed },
            new() { TaskId = original.TaskId, Succeeded = true, Type = TaskResultType.Completed },
        };

        var aggregated = bridge.FanInAsync(original, results);

        Assert.True(aggregated.Succeeded);
        Assert.Equal(original.TaskId, aggregated.TaskId);          // 不稀释
        Assert.Equal(original.ParentTaskId, aggregated.ParentTaskId); // 父层级保留在结果
    }

    [Fact]
    public async Task FanIn_any_failure_fails_parent_with_first_error()
    {
        var bridge = new WorkflowBridge();
        var original = Envelope();
        var results = new List<TaskResultEnvelope>
        {
            new() { TaskId = original.TaskId, Succeeded = true, Type = TaskResultType.Completed },
            new() { TaskId = original.TaskId, Succeeded = false, Type = TaskResultType.Failed, ErrorCode = "KS:TEST:E1" },
        };

        var aggregated = bridge.FanInAsync(original, results);

        Assert.False(aggregated.Succeeded);
        Assert.Equal("KS:TEST:E1", aggregated.ErrorCode);
        Assert.Equal(original.TaskId, aggregated.TaskId);
    }
}

public class SkillRegistryTests
{
    [Fact]
    public async Task Manifest_skills_become_agent_skills()
    {
        // 验收 3：manifest skills（skill:// URI）→ MAF skills source → 技能可加载可调用
        // （GetSkillsAsync 的 AgentSkillsSourceContext 需要真实 AIAgent——MAF host 构建过深，
        // 此处经反射读取 AgentInMemorySkillsSource 内部技能表 + 直接调用 KeystoneSkill）
        var manifest = new PluginManifest(
            "git", "1.0.0", "Git.cs", ["cordis-runtime"], [], [],
            Skills: ["skill://git-workflow/SKILL.md", "skill://code-review/SKILL.md"]);

        var source = SkillRegistry.FromManifest(manifest);

        Assert.IsType<Microsoft.Agents.AI.AgentInMemorySkillsSource>(source);
        var skills = ReadSkills(source).Cast<object>().ToList();
        Assert.Equal(2, skills.Count);
        Assert.All(skills, s => Assert.IsType<KeystoneSkill>(s));
        Assert.Contains(skills, s => ((KeystoneSkill)s).Uri.EndsWith("git-workflow/SKILL.md", StringComparison.Ordinal));

        // 可调用：技能内容可读
        var content = await ((KeystoneSkill)skills[0]).GetContentAsync();
        Assert.Contains("SEP-2640", content);
    }

    [Fact]
    public async Task Empty_skills_yield_empty_source()
    {
        var manifest = new PluginManifest("solo", "1.0.0", "Solo.cs", ["cordis-runtime"], [], []);

        var source = SkillRegistry.FromManifest(manifest);

        Assert.IsType<Microsoft.Agents.AI.AgentInMemorySkillsSource>(source);
        Assert.Empty(ReadSkills(source));
    }

    private static System.Collections.IList ReadSkills(object source)
    {
        var field = source.GetType().GetField("_skills", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("AgentInMemorySkillsSource._skills not found");
        return (System.Collections.IList)field.GetValue(source)!;
    }
}
