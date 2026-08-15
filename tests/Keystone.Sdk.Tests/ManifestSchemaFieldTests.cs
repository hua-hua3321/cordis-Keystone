using Keystone.Runtime.Plugins.Manifest;
using Keystone.Sdk.Manifest;

namespace Keystone.Sdk.Tests;

/// <summary>
/// DC-17（17-doc-compliance-audit / 10 §6）：manifest configSchema 字段 + semver/依赖白名单校验。
/// 修复前：缺 configSchema 字段；version 只查非空；依赖不校验白名单。
/// 兑现：PluginManifest.ConfigSchema（10 §6 schema 声明）；version 合法 = 语义化版本
/// （MAJOR.MINOR.PATCH，可选预发布/构建元数据）；dependencies ⊆ 白名单
/// （cordis-runtime/cordis-contracts + Keystone.* 程序集）——越界 fail-fast。
/// </summary>
public class ManifestSchemaFieldTests
{
    private static PluginManifest Manifest(
        string version = "1.0.0",
        IReadOnlyList<string>? dependencies = null,
        string? configSchema = null)
        => new("p1", version, "P1.cs", dependencies ?? ["cordis-runtime"], [], [], ConfigSchema: configSchema);

    [Fact]
    public void Valid_manifest_with_config_schema_passes()
    {
        var manifest = Manifest(dependencies: ["cordis-runtime", "cordis-contracts"], configSchema: "fs-plugin-config");

        ManifestSchemaValidator.Validate(manifest); // 含 configSchema + 合法 semver + 白名单依赖 → 不抛
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("0.1.0")]
    [InlineData("10.20.30")]
    [InlineData("1.0.0-alpha")]
    [InlineData("1.0.0-alpha.1")]
    [InlineData("1.0.0+x.7")]
    public void Semantic_versions_pass(string version)
    {
        ManifestSchemaValidator.Validate(Manifest(version: version));
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("1.0.0.0")]
    [InlineData("latest")]
    [InlineData("1.x.0")]
    public void Non_semantic_versions_fail_fast(string version)
    {
        var exception = Assert.Throws<Keystone.Core.Errors.KeystoneException>(
            () => ManifestSchemaValidator.Validate(Manifest(version: version)));

        Assert.Equal(Keystone.Core.Errors.ErrorCode.ConfigValidationFailed, exception.Code);
    }

    [Theory]
    [InlineData("cordis-runtime")]
    [InlineData("cordis-contracts")]
    [InlineData("Keystone.Core")]
    [InlineData("Keystone.Runtime")]
    [InlineData("Keystone.Sdk")]
    [InlineData("Microsoft.Extensions.Logging.Abstractions")]
    public void Whitelisted_dependencies_pass(string dependency)
    {
        ManifestSchemaValidator.Validate(Manifest(dependencies: [dependency]));
    }

    [Fact]
    public void Non_whitelisted_dependency_fails_fast()
    {
        var manifest = Manifest(dependencies: ["cordis-runtime", "System.Reflection.Emit"]);

        var exception = Assert.Throws<Keystone.Core.Errors.KeystoneException>(
            () => ManifestSchemaValidator.Validate(manifest));

        Assert.Contains("System.Reflection.Emit", exception.Message); // 精确指出越界依赖
    }

    [Fact]
    public void Config_schema_is_optional_and_preserved()
    {
        // configSchema 可选（null = 无 schema 声明，原始 config 直传——G-C1 语义）
        var without = Manifest();
        var with = Manifest(configSchema: "fs-plugin-config");

        ManifestSchemaValidator.Validate(without);
        ManifestSchemaValidator.Validate(with);
        Assert.Equal("fs-plugin-config", with.ConfigSchema);
        Assert.Null(without.ConfigSchema);
    }
}
