using KemoCard.Frame.Mvc;

namespace ComputerSimulator.Modules.Commission;

/// <summary>委托功能的内部总线事件。载荷与枚举成员一一对应。</summary>
public enum ECommissionEvent
{
    [EventPayload(typeof(CommissionSnapshot))]
    StateChanged,
}

/// <summary>委托功能的强类型事件键，供视图绑定 <see cref="CommissionMod.InternalBus"/>。</summary>
public static class CommissionEvents
{
    public static readonly EventKey<CommissionSnapshot> StateChanged = new((int)ECommissionEvent.StateChanged);
}
