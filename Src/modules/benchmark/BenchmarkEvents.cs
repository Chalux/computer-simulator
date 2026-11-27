using KemoCard.Frame.Mvc;

namespace ComputerSimulator.Modules.Benchmark;

/// <summary>
/// 负载报告功能内部事件 id。成员上的 <see cref="EventPayloadAttribute"/>
/// 同时供视图的 <see cref="EventSubscribeAttribute"/> 解析事件载荷类型。
/// </summary>
internal enum EBenchmarkEvent
{
    /// <summary>报告 / 对照状态快照发生变化。</summary>
    [EventPayload(typeof(BenchmarkViewState))]
    StateChanged = 0,
}

/// <summary>负载报告功能内部事件键；由 Controller 发布到 <see cref="BenchmarkMod.InternalBus"/>。</summary>
internal static class BenchmarkEvents
{
    internal static readonly EventKey<BenchmarkViewState> StateChanged =
        new((int)EBenchmarkEvent.StateChanged);
}
