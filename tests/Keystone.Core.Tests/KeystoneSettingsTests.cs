using Microsoft.Extensions.Configuration;

namespace Keystone.Core.Tests;

public class KeystoneSettingsTests
{
    [Fact]
    public void Missing_section_uses_documented_defaults()
    {
        var settings = KeystoneSettings.Bind(new ConfigurationBuilder().Build());

        Assert.Equal("plugins", settings.PluginDirectory);
        Assert.Equal(TimeSpan.FromSeconds(30), settings.DependencyWaitTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), settings.QuiesceTimeout);
        Assert.Equal(1, settings.DefaultConcurrency);
        Assert.Equal("Information", settings.LogLevel);
    }

    [Fact]
    public void Config_values_override_defaults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["keystone:pluginDirectory"] = "custom-plugins",
                ["keystone:quiesceTimeout"] = "00:01:00",
                ["keystone:defaultConcurrency"] = "4",
            })
            .Build();

        var settings = KeystoneSettings.Bind(config);

        Assert.Equal("custom-plugins", settings.PluginDirectory);
        Assert.Equal(TimeSpan.FromMinutes(1), settings.QuiesceTimeout);
        Assert.Equal(4, settings.DefaultConcurrency);
        // 未提供的字段保持默认（配置驱动，不硬编码）
        Assert.Equal("Information", settings.LogLevel);
    }

    [Fact]
    public void Malformed_value_fails_fast()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["keystone:quiesceTimeout"] = "not-a-timespan",
            })
            .Build();

        Assert.Throws<FormatException>(() => KeystoneSettings.Bind(config));
    }
}
