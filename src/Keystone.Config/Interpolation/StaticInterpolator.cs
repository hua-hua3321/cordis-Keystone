using Keystone.Core.Errors;

namespace Keystone.Config.Interpolation;

/// <summary>
/// 静态插值（ADR-0012/08 §4）：YAML 自定义 tag <c>!!env NAME</c>（环境变量替换）、
/// <c>!!file path</c>（文件内容引入，读取后递归插值，visited 检测引用环）。
/// 兼容字符串前缀形态 <c>!!env:NAME</c>/<c>!!file:path</c>（文本内容内引用）。
/// 加载期确定性变换，非代码求值（规则 0 第 4 条）。
/// </summary>
public sealed class StaticInterpolator
{
    /// <summary>YAML 短 tag <c>!!env</c> 展开后的完整 tag（YamlDotNet TagName.Value）。</summary>
    public const string EnvTag = "tag:yaml.org,2002:env";

    /// <summary>YAML 短 tag <c>!!file</c> 展开后的完整 tag。</summary>
    public const string FileTag = "tag:yaml.org,2002:file";

    private readonly Func<string, string?> _env;
    private readonly Func<string, string?> _file;

    public StaticInterpolator(Func<string, string?> env, Func<string, string?> file)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(file);
        _env = env;
        _file = file;
    }

    /// <summary>
    /// 字符串前缀形态插值（<c>!!env:NAME</c>/<c>!!file:path</c>）。
    /// 用于文本内容内的引用（文件内容、既有调用方）；顶层 YAML 走 <see cref="InterpolateTagged"/>。
    /// </summary>
    public string Interpolate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return InterpolateCore(value, new HashSet<string>(StringComparer.Ordinal));
    }

    /// <summary>
    /// YAML tag 形态插值（DC-8，ADR-0012）：<c>!!env NAME</c> → tag=EnvTag/value=NAME；
    /// <c>!!file path</c> → tag=FileTag/value=path。visited 跨整个解析共享（引用环检测）。
    /// 缺失的环境变量/文件保留原始标记（<c>!!env NAME</c>/<c>!!file path</c>，不静默替换）；
    /// 文件内容递归插值；环 → ConfigValidationFailed fail-fast。
    /// </summary>
    public string InterpolateTagged(string tag, string value, ISet<string> visited)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(visited);

        if (string.Equals(tag, EnvTag, StringComparison.Ordinal))
        {
            return _env(value) ?? $"!!env {value}";
        }

        if (string.Equals(tag, FileTag, StringComparison.Ordinal))
        {
            if (!visited.Add(value))
            {
                throw new KeystoneException(
                    ErrorCode.ConfigValidationFailed,
                    $"cyclic file reference in config interpolation: {value}");
            }

            try
            {
                var content = _file(value);
                if (content is null)
                {
                    return $"!!file {value}";
                }

                return InterpolateCore(content, visited); // 文件内容递归插值（环检测）
            }
            finally
            {
                visited.Remove(value); // 展开栈退出（同一文件多处引用非环）
            }
        }

        return value;
    }

    private string InterpolateCore(string value, ISet<string> visited)
    {
        if (value.StartsWith("!!env:", StringComparison.Ordinal))
        {
            var name = value["!!env:".Length..];
            return _env(name) ?? value; // 环境变量缺失：保留原值（不 fail-fast，允许运行时注入）
        }

        if (value.StartsWith("!!file:", StringComparison.Ordinal))
        {
            var path = value["!!file:".Length..];
            if (!visited.Add(path))
            {
                throw new KeystoneException(
                    ErrorCode.ConfigValidationFailed,
                    $"cyclic file reference in config interpolation: {path}");
            }

            try
            {
                var content = _file(path);
                if (content is null)
                {
                    return value;
                }

                return InterpolateCore(content, visited);
            }
            finally
            {
                visited.Remove(path);
            }
        }

        return value;
    }
}
