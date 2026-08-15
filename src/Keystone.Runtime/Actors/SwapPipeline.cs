using Keystone.Runtime.Pipeline;

namespace Keystone.Runtime.Actors;

/// <summary>
/// 管道原子替换指令（DC-10，ADR-0003 决策 2 / 04 §8）：actor 消息循环内重建管道缓存——
/// 串行语义保证在途请求（已捕获旧链委托）走旧管道完成后，后续请求走新链；保留 actor/context。
/// </summary>
internal sealed record SwapPipeline(IReadOnlyList<IMiddleware> Middlewares);
