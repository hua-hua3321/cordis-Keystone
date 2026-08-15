using Microsoft.Extensions.Configuration;

namespace Keystone.Config.Tests;

public class YamlFileConfigurationProviderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("keystone-yaml-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // 文件仍被 watcher 占用：临时目录清理失败可忽略
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Flattens_nested_mappings_and_sequences()
    {
        var path = Write("config.yml", """
            app:
              name: demo
              features:
                tracing: true
              ports: [8080, 8081]
            """);

        var config = new ConfigurationBuilder()
            .AddYamlFile(path)
            .Build();

        Assert.Equal("demo", config["app:name"]);
        Assert.Equal("true", config["app:features:tracing"]); // 标量保持字符串形态（M.E.C 值均为字符串）
        Assert.Equal("8080", config["app:ports:0"]);
        Assert.Equal("8081", config["app:ports:1"]);
        Assert.Null(config["app:missing"]);
    }

    [Fact]
    public void Resolves_anchors_and_merge_keys()
    {
        var path = Write("anchors.yml", """
            defaults: &d
              retries: 3
            service:
              <<: *d
              timeout: 30
            """);

        var config = new ConfigurationBuilder()
            .AddYamlFile(path)
            .Build();

        Assert.Equal("3", config["service:retries"]);
        Assert.Equal("30", config["service:timeout"]);
    }

    [Fact]
    public void Optional_missing_file_loads_empty()
    {
        var config = new ConfigurationBuilder()
            .AddYamlFile(Path.Combine(_directory, "absent.yml"))
            .Build();

        Assert.Null(config["any:key"]);
    }

    [Fact]
    public void Non_optional_missing_file_throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            new ConfigurationBuilder()
                .AddYamlFile(Path.Combine(_directory, "absent.yml"), optional: false)
                .Build());
    }

    [Fact]
    public void Reload_picks_up_file_changes()
    {
        var path = Write("reload.yml", "value: v1");
        var config = new ConfigurationBuilder()
            .AddYamlFile(path, reloadOnChange: true)
            .Build();

        Assert.Equal("v1", config["value"]);

        File.WriteAllText(path, "value: v2");
        Assert.True(
            WaitUntil(() => config["value"] == "v2", TimeSpan.FromSeconds(5)),
            "watcher 触发防抖重载后应读到 v2");
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(50);
        }

        return condition();
    }
}
