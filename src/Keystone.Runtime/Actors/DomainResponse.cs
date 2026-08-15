using Keystone.Core.Contracts;

namespace Keystone.Runtime.Actors;

/// <summary>跨域响应消息（携带原 TaskId/ParentTaskId）。</summary>
public sealed record DomainResponse(TaskResultEnvelope Envelope);
