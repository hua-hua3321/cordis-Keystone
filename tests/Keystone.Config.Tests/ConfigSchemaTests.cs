using Keystone.Config.Validation;
using Keystone.Core.Errors;

namespace Keystone.Config.Tests;

public class ConfigSchemaTests
{
    [Fact]
    public void Missing_required_field_fails_with_precise_error()
    {
        var schema = new ConfigSchema([
            new ConfigField("root", Required: true, Default: null),
            new ConfigField("mode", Required: false, Default: "read-only"),
        ]);

        var result = schema.Validate(new Dictionary<string, object?>());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("root", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Unknown_field_fails_fast()
    {
        var schema = new ConfigSchema([new ConfigField("root", Required: true, Default: null)]);

        var result = schema.Validate(new Dictionary<string, object?> { ["unknown"] = 1 });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ApplyDefaults_fills_missing_optional_fields()
    {
        var schema = new ConfigSchema([
            new ConfigField("root", Required: true, Default: null),
            new ConfigField("mode", Required: false, Default: "read-only"),
        ]);

        var applied = (Dictionary<string, object?>)schema.ApplyDefaults(new Dictionary<string, object?> { ["root"] = "/data" })!;

        Assert.Equal("read-only", applied["mode"]);
    }

    [Fact]
    public void Valid_config_passes()
    {
        var schema = new ConfigSchema([new ConfigField("root", Required: true, Default: null)]);

        Assert.True(schema.Validate(new Dictionary<string, object?> { ["root"] = "/data" }).IsValid);
    }
}

public class ConfigResolverTests
{
    [Fact]
    public async Task Resolver_runs_filters_then_validates_and_applies_defaults()
    {
        var order = new List<string>();
        var resolver = new ConfigResolver();
        var schema = new ConfigSchema([
            new ConfigField("root", Required: true, Default: null),
            new ConfigField("mode", Required: false, Default: "read-only"),
        ]);
        var filter = new RecordingFilter(order);

        var resolved = await resolver.ResolveAsync(
            new Dictionary<string, object?> { ["root"] = "/data" },
            schema,
            [filter]);
        var result = (Dictionary<string, object?>)resolved!;

        Assert.Equal(["filter-in", "filter-out"], order); // 过滤器包裹（M3 管线：raw → 过滤器 → 校验 → 默认值）
        Assert.Equal("read-only", result["mode"]);
    }

    [Fact]
    public async Task Resolver_fails_fast_on_invalid_config()
    {
        var resolver = new ConfigResolver();
        var schema = new ConfigSchema([new ConfigField("root", Required: true, Default: null)]);

        var exception = await Assert.ThrowsAsync<KeystoneException>(async () =>
            await resolver.ResolveAsync(new Dictionary<string, object?>(), schema, []));

        Assert.Equal(ErrorCode.ConfigValidationFailed, exception.Code);
    }

    [Fact]
    public async Task Filter_can_veto_config()
    {
        var resolver = new ConfigResolver();
        var schema = new ConfigSchema([new ConfigField("root", Required: true, Default: null)]);
        var veto = new VetoFilter();

        await Assert.ThrowsAsync<KeystoneException>(async () =>
            await resolver.ResolveAsync(new Dictionary<string, object?> { ["root"] = "/data" }, schema, [veto]));
    }

    private sealed class RecordingFilter : IConfigFilter
    {
        private readonly List<string> _order;

        public RecordingFilter(List<string> order)
        {
            _order = order;
        }

        public Task OnConfigAsync(object? raw, Func<object?, Task> next, CancellationToken ct)
        {
            _order.Add("filter-in");
            return next(raw).ContinueWith(_ => _order.Add("filter-out"), ct);
        }
    }

    private sealed class VetoFilter : IConfigFilter
    {
        public Task OnConfigAsync(object? raw, Func<object?, Task> next, CancellationToken ct)
            => throw new KeystoneException(ErrorCode.PipelineMiddlewareRejected, "vetoed by filter");
    }
}
