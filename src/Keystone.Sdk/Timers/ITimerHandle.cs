namespace Keystone.Sdk.Timers;

/// <summary>
/// 计时器句柄（10 §4）：随插件生命周期回收（quiesce 时经 Effect 收敛自动取消）。
/// <see cref="Trigger"/> 对 Debounce/Throttle 有语义（可重复触发）；SetTimeout/SetInterval 为无操作。
/// </summary>
public interface ITimerHandle : IAsyncDisposable
{
    /// <summary>触发一次（防抖/节流窗口语义）。</summary>
    void Trigger();
}
