using Keystone.Core.Contracts;

namespace Keystone.Runtime.Actors;

/// <summary>跨域请求消息（TaskId 贯穿，06 §1/ADR-0004）。</summary>
public sealed record DomainRequest(TaskEnvelope Envelope);
