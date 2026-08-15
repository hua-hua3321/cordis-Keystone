using Keystone.Core.Errors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Keystone.Runtime.Plugins.Loading;

/// <summary>
/// Roslyn 内存编译（ADR-0002 刻意例外区：插件加载层）。
/// 引用集 = 运行时 BCL（TRUSTED_PLATFORM_ASSEMBLIES）+ 宿主白名单（Keystone.Runtime 等），
/// 与插件 ALC 的运行解析集同源（02 §5 清单 #5）。
/// </summary>
public static class RoslynCompiler
{
    public static byte[] Compile(string assemblyName, string sourceCode, IReadOnlyList<MetadataReference> references)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
        ArgumentNullException.ThrowIfNull(sourceCode);
        ArgumentNullException.ThrowIfNull(references);

        var tree = CSharpSyntaxTree.ParseText(sourceCode, new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [tree],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString());
            throw new KeystoneException(
                ErrorCode.LifecycleLoadFailed,
                $"plugin '{assemblyName}' failed to compile:\n{string.Join("\n", errors)}");
        }

        return stream.ToArray();
    }

    /// <summary>默认引用集：BCL（TPA）+ 宿主 Keystone.Runtime（IPlugin 等白名单）。</summary>
    public static IReadOnlyList<MetadataReference> CreateDefaultReferences()
    {
        var references = new List<MetadataReference>();
        var tpa = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        foreach (var path in tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            references.Add(MetadataReference.CreateFromFile(path));
        }

        var runtimeDll = Path.Combine(AppContext.BaseDirectory, "Keystone.Runtime.dll");
        if (File.Exists(runtimeDll))
        {
            references.Add(MetadataReference.CreateFromFile(runtimeDll));
        }

        return references;
    }
}
