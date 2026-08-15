using Keystone.Runtime.Plugins.Manifest;
using Keystone.Core.Errors;

namespace Keystone.Runtime.Tests;

public class ManifestValidatorTests
{
    [Fact]
    public void Acyclic_dependency_graph_passes()
    {
        var plugins = new[]
        {
            new PluginManifest("a", "1.0.0", "A.cs", ["cordis-runtime"], ["fs"], []),
            new PluginManifest("b", "1.0.0", "B.cs", ["cordis-runtime"], ["llm"], ["fs"]),
        };

        ManifestValidator.Validate(plugins); // 不应抛
    }

    [Fact]
    public void Cyclic_dependency_graph_fails_fast()
    {
        var plugins = new[]
        {
            new PluginManifest("a", "1.0.0", "A.cs", ["cordis-runtime"], ["fs"], ["llm"]),
            new PluginManifest("b", "1.0.0", "B.cs", ["cordis-runtime"], ["llm"], ["fs"]),
        };

        var exception = Assert.Throws<KeystoneException>(() => ManifestValidator.Validate(plugins));

        Assert.Equal(ErrorCode.GatingCircularDependency, exception.Code);
    }

    [Fact]
    public void Unreachable_inject_fails_fast()
    {
        var plugins = new[]
        {
            new PluginManifest("a", "1.0.0", "A.cs", ["cordis-runtime"], [], ["missing-service"]),
        };

        var exception = Assert.Throws<KeystoneException>(() => ManifestValidator.Validate(plugins));

        Assert.Equal(ErrorCode.GatingServiceNotFound, exception.Code);
        Assert.Contains("missing-service", exception.Message);
    }

    [Fact]
    public void Empty_provides_and_inject_are_valid()
    {
        var plugins = new[]
        {
            new PluginManifest("solo", "1.0.0", "Solo.cs", ["cordis-runtime"], [], []),
        };

        ManifestValidator.Validate(plugins); // 不应抛
    }
}
