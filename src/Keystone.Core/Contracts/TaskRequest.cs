namespace Keystone.Core.Contracts;

/// <summary>
/// 进入管道的任务（接口层形状，doc 06 §1）：运行态对象，含取消传播贯穿全链。
/// 跨域序列化使用 <see cref="TaskEnvelope"/>（载荷以字节承载，具体类型由能力域契约序列化，
/// 规则 0 第 3 条：源生成友好契约）。
/// </summary>
public sealed record TaskRequest(
    TaskId TaskId,
    TaskId? ParentTaskId,
    string Capability,
    string Operation,
    object? Payload,
    CancellationToken CancellationToken);
