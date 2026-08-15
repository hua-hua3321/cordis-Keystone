using Keystone.Config.AgileConfig;
using Microsoft.Extensions.Configuration;

namespace Keystone.Config.Tests;

public class AgileConfigConfigurationProviderTests
{
    [Fact]
    public void Load_populates_from_client_snapshot()
    {
        var client = new FakeAgileConfigClient();
        client.Data["app:name"] = "remote";

        var config = Build(client);

        Assert.Equal("remote", config["app:name"]);
    }

    [Fact]
    public void Center_push_reloads_configuration()
    {
        var client = new FakeAgileConfigClient();
        client.Data["app:name"] = "v1";
        var config = Build(client);

        client.PushChange("app:name", "v2");

        Assert.Equal("v2", config["app:name"]);
    }

    [Fact]
    public void Failed_reload_keeps_last_good_data()
    {
        var client = new FakeAgileConfigClient();
        client.Data["app:name"] = "v1";
        var config = Build(client);
        Assert.Equal("v1", config["app:name"]);

        client.ThrowOnGetAll = true;
        client.PushChange("app:name", "v2");

        Assert.Equal("v1", config["app:name"]);
    }

    [Fact]
    public void Optional_failure_loads_empty_without_throwing()
    {
        var client = new FakeAgileConfigClient { ThrowOnInitialize = true };

        var config = Build(client);

        Assert.Null(config["app:name"]);
    }

    private static IConfiguration Build(IAgileConfigClient client) =>
        new ConfigurationBuilder()
            .Add(new AgileConfigConfigurationSource { Client = client })
            .Build();

    private sealed class FakeAgileConfigClient : IAgileConfigClient
    {
        public Dictionary<string, string> Data { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsInitialized { get; private set; }

        public bool ThrowOnGetAll { get; set; }

        public bool ThrowOnInitialize { get; set; }

        public event EventHandler? ConfigChanged;

        public string? GetValue(string key) => Data.TryGetValue(key, out var value) ? value : null;

        public IReadOnlyDictionary<string, string> GetAll() =>
            ThrowOnGetAll ? throw new InvalidOperationException("center unavailable") : Data;

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (ThrowOnInitialize)
            {
                throw new InvalidOperationException("center unavailable");
            }

            IsInitialized = true;
            return Task.CompletedTask;
        }

        public void PushChange(string key, string value)
        {
            Data[key] = value;
            ConfigChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
        }
    }
}
