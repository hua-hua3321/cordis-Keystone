using Keystone.Core.Errors;
using Keystone.Runtime.Plugins.Loading;

namespace Keystone.Runtime.Tests;

public class RoslynCompilerTests
{
    [Fact]
    public void Compile_valid_source_returns_pe_bytes()
    {
        var pe = RoslynCompiler.Compile("test-plugin", """
            public sealed class Sample
            {
                public int Add(int a, int b) => a + b;
            }
            """, RoslynCompiler.CreateDefaultReferences());

        Assert.NotNull(pe);
        Assert.True(pe.Length > 0);
        Assert.Equal((byte)'M', pe[0]); // PE 头
        Assert.Equal((byte)'Z', pe[1]);
    }

    [Fact]
    public void Compile_invalid_source_throws_with_diagnostics()
    {
        var exception = Assert.Throws<KeystoneException>(() =>
            RoslynCompiler.Compile("broken-plugin", "public class {", RoslynCompiler.CreateDefaultReferences()));

        Assert.Equal(ErrorCode.LifecycleLoadFailed, exception.Code);
        Assert.Contains("error", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_plugin_implementing_ipugin_succeeds()
    {
        var pe = RoslynCompiler.Compile("ipugin-plugin", SampleSources.V1, RoslynCompiler.CreateDefaultReferences());

        Assert.NotNull(pe);
    }
}

/// <summary>测试插件源码（内嵌，编译进独立 ALC）。</summary>
public static class SampleSources
{
    public const string V1 = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class SamplePlugin : IPlugin
        {
            public const string Version = "v1";

            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;

    public const string V2 = """
        using System;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Keystone.Runtime.Context;
        using Keystone.Runtime.Plugins.Lifecycle;

        public sealed class SamplePlugin : IPlugin
        {
            public const string Version = "v2";

            public Task InitializeAsync(IPluginContext context, IReadOnlyDictionary<string, object?> config)
            {
                return Task.CompletedTask;
            }

            public Task DisposeAsync() => Task.CompletedTask;
        }
        """;
}
