using Microsoft.Extensions.Configuration;

namespace Keystone.Core.Tests;

public class KeystoneSettingsTests
{
    [Fact]
    public void Missing_section_uses_documented_defaults()
    {
        var settings = KeystoneSettings.Bind(new ConfigurationBuilder().Build());

        Assert.Equal(TimeSpan.FromSeconds(30), settings.DependencyWaitTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), settings.QuiesceTimeout);
    }

    [Fact]
    public void Config_values_override_defaults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["keystone:dependencyWaitTimeout"] = "00:00:05",
                ["keystone:quiesceTimeout"] = "00:01:00",
            })
            .Build();

        var settings = KeystoneSettings.Bind(config);

        Assert.Equal(TimeSpan.FromSeconds(5), settings.DependencyWaitTimeout);
        Assert.Equal(TimeSpan.FromMinutes(1), settings.QuiesceTimeout);
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
