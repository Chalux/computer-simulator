using KemoCard.Frame.Mvc;

namespace ComputerSimulator.Modules.Diagnosis;

/// <summary>
/// 诊断功能内部事件 id。成员上的 <see cref="EventPayloadAttribute"/>
/// 同时供视图的 <see cref="EventSubscribeAttribute"/> 解析事件载荷类型。
/// </summary>
internal enum EDiagnosisEvent
{
    /// <summary>案件状态快照发生变化。</summary>
    [EventPayload(typeof(DiagnosisSnapshot))]
    StateChanged = 0,
}

/// <summary>
/// 诊断功能内部事件键；由 Controller 发布到 Model 的功能内部总线。
/// </summary>
internal static class DiagnosisEvents
{
    internal static readonly EventKey<DiagnosisSnapshot> StateChanged =
        new((int)EDiagnosisEvent.StateChanged);
}
