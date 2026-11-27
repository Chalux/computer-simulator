using KemoCard.Frame.Mvc;

namespace ComputerSimulator.Modules.Menu;

/// <summary>菜单功能的内部事件枚举；成员上的载荷类型同时供装饰器订阅解析。</summary>
public enum EMenuEvent
{
    [EventPayload(typeof(MenuSettings))]
    SettingsChanged,

    [EventPayload(typeof(GameMode))]
    LastModeChanged,

    [EventPayload(typeof(MenuQuitRequest))]
    QuitRequested,
}

/// <summary>“请求退出应用”的无载荷事件体，仅用于在总线上区分事件类型。</summary>
public readonly struct MenuQuitRequest
{
}

/// <summary>
/// 菜单功能的内部事件键，挂在 <see cref="MenuMod.InternalBus"/> 上。
/// 视图不直接订阅；由组合根/后续功能按需监听退出等意图。
/// </summary>
public static class MenuEvents
{
    /// <summary>设置快照发生变化（加载完成或任一选项被修改）。</summary>
    public static readonly EventKey<MenuSettings> SettingsChanged = new((int)EMenuEvent.SettingsChanged);

    /// <summary>最近选择的模式发生变化。</summary>
    public static readonly EventKey<GameMode> LastModeChanged = new((int)EMenuEvent.LastModeChanged);

    /// <summary>应用退出意图。存在监听者时由监听者接管退出流程。</summary>
    public static readonly EventKey<MenuQuitRequest> QuitRequested = new((int)EMenuEvent.QuitRequested);
}
