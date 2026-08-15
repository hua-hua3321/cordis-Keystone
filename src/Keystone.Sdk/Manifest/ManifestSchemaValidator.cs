using Keystone.Core.Errors;
using Keystone.Runtime.Plugins.Manifest;

namespace Keystone.Sdk.Manifest;

/// <summary>
/// manifest 字段级校验（10 §6）：id/version/main 非空、skills 为 SEP-2640 skill:// URI 或 MCP 资源。
/// 依赖图校验（无环/可达）复用 Runtime ManifestValidator（多插件场景，启动期调用）。
/// </summary>
public static class ManifestSchemaValidator
{
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
