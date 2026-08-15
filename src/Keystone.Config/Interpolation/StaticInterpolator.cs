using Keystone.Core.Errors;

namespace Keystone.Config.Interpolation;

/// <summary>
/// 静态插值（ADR-0012）：<c>!!env:NAME</c> 环境变量替换、<c>!!file:path</c> 文件内容引入
/// （读取后递归插值，visited 检测引用环）。加载期确定性变换，非代码求值（规则 0 第 4 条）。
/// </summary>
public sealed class StaticInterpolator
{
    private readonly Func<string, string?> _env;
    private readonly Func<string, string?> _file;

    public StaticInterpolator(Func<string, string?> env, Func<string, string?> file)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(file);
        _env = env;
        _file = file;
    }

    public string Interpolate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return InterpolateCore(value, new HashSet<string>(StringComparer.Ordinal));
    }

    private string InterpolateCore(string value, HashSet<string> visited)
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

            var content = _file(path);
            if (content is null)
            {
                return value;
            }

            return InterpolateCore(content, visited); // 文件内容递归插值（环检测）
        }

        return value;
    }
}
