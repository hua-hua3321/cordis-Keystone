namespace Keystone.Core.Errors;

/// <summary>
/// 框架级错误码表（M6 定稿，doc 12 §8）：格式 <c>KS:{CATEGORY}:{NAME}</c>。
/// 与 <see cref="Contracts.TaskResult.ErrorCode"/>（doc 06 §1）和
/// <see cref="KeystoneException.Code"/> 共用；业务代码不得依赖消息文本做控制流。
/// </summary>
public static class ErrorCode
{
    // ── CORE：通用 ──
    public const string Generic = "KS:CORE:GENERIC";
    public const string InvalidArgument = "KS:CORE:INVALID_ARGUMENT";
    public const string InvalidOperation = "KS:CORE:INVALID_OPERATION";
    public const string Unsupported = "KS:CORE:UNSUPPORTED";
    public const string Internal = "KS:CORE:INTERNAL";
    public const string ServiceAlreadyRegistered = "KS:CORE:SERVICE_ALREADY_REGISTERED";

    // ── LIFECYCLE：插件生命周期（ADR-0005）──
    public const string LifecycleInvalidState = "KS:LIFECYCLE:INVALID_STATE";
    public const string LifecycleLoadFailed = "KS:LIFECYCLE:LOAD_FAILED";
    public const string LifecycleUnloadFailed = "KS:LIFECYCLE:UNLOAD_FAILED";
    public const string LifecycleQuiesceTimeout = "KS:LIFECYCLE:QUIESCE_TIMEOUT";

    // ── GATING：依赖门控（ADR-0007）──
    public const string GatingDependencyTimeout = "KS:GATING:DEPENDENCY_TIMEOUT";
    public const string GatingCircularDependency = "KS:GATING:CIRCULAR_DEPENDENCY";
    public const string GatingServiceNotFound = "KS:GATING:SERVICE_NOT_FOUND";

    // ── CONFIG：配置层（ADR-0013/0014）──
    public const string ConfigValidationFailed = "KS:CONFIG:VALIDATION_FAILED";
    public const string ConfigProviderFailed = "KS:CONFIG:PROVIDER_FAILED";
    public const string ConfigFileNotFound = "KS:CONFIG:FILE_NOT_FOUND";

    // ── PIPELINE：管道执行（ADR-0006）──
    public const string PipelineExecutionFailed = "KS:PIPELINE:EXECUTION_FAILED";
    public const string ReliabilityCircuitOpen = "KS:RELIABILITY:CIRCUIT_OPEN";
    public const string PipelineMiddlewareRejected = "KS:PIPELINE:MIDDLEWARE_REJECTED";
    public const string PipelineCancelled = "KS:PIPELINE:CANCELLED";

    /// <summary>码表全量（供校验/诊断/回写 doc 12 §8 M6）。</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Generic,
        InvalidArgument,
        InvalidOperation,
        Unsupported,
        Internal,
        ServiceAlreadyRegistered,
        LifecycleInvalidState,
        LifecycleLoadFailed,
        LifecycleUnloadFailed,
        LifecycleQuiesceTimeout,
        GatingDependencyTimeout,
        GatingCircularDependency,
        GatingServiceNotFound,
        ConfigValidationFailed,
        ConfigProviderFailed,
        ConfigFileNotFound,
        PipelineExecutionFailed,
        PipelineMiddlewareRejected,
        PipelineCancelled,
        ReliabilityCircuitOpen,
    ];

    /// <summary>码是否在表中（未知码 = 编码错误，fail-fast）。</summary>
    public static bool IsKnown(string code) => All.Contains(code, StringComparer.Ordinal);
}
