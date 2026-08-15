using System.Text.RegularExpressions;
using Keystone.Core.Errors;
using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.Sdk.Manifest;

/// <summary>
/// manifest 字段级校验（10 §6 + DC-17）：id/version/main 非空、version 合法（语义化版本）、
/// skills 为 SEP-2640 skill:// URI 或 MCP 资源、dependencies ⊆ 编译白名单（越界 fail-fast——
/// 规则 0：插件不可编译进反射_emit/动态程序集等宿主禁用依赖）。
/// 依赖图校验（无环/可达）复用 Runtime ManifestValidator（多插件场景，启动期调用）。
/// </summary>
public static partial class ManifestSchemaValidator
{
    /// <summary>
    /// 程序集编译白名单（02 §2 接口白名单的程序集面）：框架程序集 + 官方兼容 BCL/扩展包。
    /// 越界依赖（如 System.Reflection.Emit）= 配置错误 fail-fast。
    /// </summary>
    public static readonly IReadOnlySet<string> AssemblyWhitelist = new HashSet<string>(StringComparer.Ordinal)
    {
        "cordis-runtime",
        "cordis-contracts",
        "Keystone.Core",
        "Keystone.Config",
        "Keystone.Runtime",
        "Keystone.Hosting",
        "Keystone.Sdk",
        "Microsoft.Extensions.Logging.Abstractions",
    };

    [GeneratedRegex(
        @"^(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)(?:-(?:(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*)?$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex SemverRegex();

    public static void Validate(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (string.IsNullOrWhiteSpace(manifest.Id)
            || string.IsNullOrWhiteSpace(manifest.Version)
            || string.IsNullOrWhiteSpace(manifest.Main))
        {
            throw new KeystoneException(
                ErrorCode.ConfigValidationFailed,
                $"manifest must have non-empty id/version/main: {manifest.Id}");
        }

        // DC-17：version 合法 = 语义化版本（10 §6 "version 必填，语义化版本"）
        if (!SemverRegex().IsMatch(manifest.Version))
        {
            throw new KeystoneException(
                ErrorCode.ConfigValidationFailed,
                $"manifest version must be semantic (MAJOR.MINOR.PATCH[-prerelease][+build]): '{manifest.Version}' of '{manifest.Id}'");
        }

        // DC-17：依赖编译白名单（越界 fail-fast，规则 0）
        foreach (var dependency in manifest.Dependencies)
        {
            if (!AssemblyWhitelist.Contains(dependency))
            {
                throw new KeystoneException(
                    ErrorCode.ConfigValidationFailed,
                    $"manifest dependency '{dependency}' of '{manifest.Id}' is not in the assembly whitelist");
            }
        }

        foreach (var skill in manifest.Skills ?? [])
        {
            if (!skill.StartsWith("skill://", StringComparison.Ordinal)
                && !skill.StartsWith("mcp:", StringComparison.Ordinal))
            {
                throw new KeystoneException(
                    ErrorCode.ConfigValidationFailed,
                    $"manifest skill must be a SEP-2640 skill:// URI or mcp: resource: {skill}");
            }
        }
    }
}
