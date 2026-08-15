using Keystone.Core.Contracts;

namespace Keystone.Runtime.Actors;

/// <summary>
/// 跨域请求消息（TaskId 贯穿，06 §1/ADR-0004）：信封 + 调用方取消令牌（DC-14，06 §1 取消贯穿全链）。
/// CT 不入信封（TaskEnvelope 是可序列化 DTO，CT 属运行态）；本地消息按引用传递，
/// 远程化演进时 CT 需换为超时预算（跨进程序列化边界）。
/// </summary>
public sealed record DomainRequest(TaskEnvelope Envelope, CancellationToken CancellationToken = default);
