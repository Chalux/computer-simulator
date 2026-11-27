using KemoCard.Frame.Mvc;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 主菜单功能的应用级 Model：持有跨界面存活的菜单状态（设置快照与最近选择的模式）。
/// 模式目录由已加载的内容包提供。
/// 所有写入入口都在 <see cref="MenuController"/> 内，视图不得直接修改。
/// </summary>
public sealed class MenuMod : BaseMod
{
    public const string ModIdValue = "menu";

    public MenuMod() : base(ModIdValue)
    {
    }

    /// <summary>当前生效的菜单设置快照；由 <see cref="MenuController"/> 读写。</summary>
    public MenuSettings Settings { get; internal set; } = MenuSettings.Default;
    internal IReadOnlyList<GameModeInfo> Modes { get; set; } = [];

    /// <summary>最近一次选择的模式，供下次进入模式选择时恢复焦点；为空表示尚未选择。</summary>
    public GameMode? LastSelectedMode { get; internal set; }
}
