using Keystone.Config.AgileConfig;

namespace Keystone.Config.Tests;

public class KeystoneConfigBuilderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("keystone-builder-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // watcher 占用可忽略
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void CreateDefault_without_yaml_loads_empty()
    {
        // 默认组合（ADR-0014）= 仅可选本地 keystone.yml；无文件 → 空配置，不阻塞启动
        var config = KeystoneConfigBuilder.CreateDefault().Build();

        Assert.Null(config["keystone:anything"]);
    }

    [Fact]
    public void Later_source_overrides_earlier_source()
    {
        var yamlPath = Path.Combine(_directory, "keystone.yml");
        File.WriteAllText(yamlPath, "app:\n  name: local\n");

        var client = new FakeAgileConfigClient();
        client.Data["app:name"] = "remote";

        var config = new KeystoneConfigBuilder()
            .AddYamlFile(yamlPath)
            .AddAgileConfig(client) // 预留源显式追加：M.E.C 后添加者优先（未来配置中心启用时沿用此语义）
            .Build();

        Assert.Equal("remote", config["app:name"]);
    }

    [Fact]
    public void Yaml_only_composition_keeps_local_values()
    {
        var yamlPath = Path.Combine(_directory, "keystone.yml");
        File.WriteAllText(yamlPath, "app:\n  name: local\n");

        var config = new KeystoneConfigBuilder()
            .AddYamlFile(yamlPath)
            .Build();

        Assert.Equal("local", config["app:name"]);
    }

    private sealed class FakeAgileConfigClient : IAgileConfigClient
    {
        public Dictionary<string, string> Data { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsInitialized { get; private set; }

        public event EventHandler? ConfigChanged;

        public string? GetValue(string key) => Data.TryGetValue(key, out var value) ? value : null;

        public IReadOnlyDictionary<string, string> GetAll() => Data;

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
