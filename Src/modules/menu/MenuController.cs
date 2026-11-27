using ComputerSimulator.Modules.Hardware;
using KemoCard.Frame.Mvc;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 主菜单功能的 Controller：唯一读写 <see cref="MenuMod"/> 的入口。
/// 设置加载 / 应用 / 落盘分别委托给 <see cref="MenuSettingsStore"/> 与 <see cref="MenuSettingsApplier"/>；
/// 视图只通过本类公开的意图方法改变状态，应用退出也在这里中转。
/// 模式目录来自注入的 <see cref="GameContent"/>，不再使用静态文案。
/// </summary>
public sealed class MenuController : BaseController<MenuMod>
{
    private readonly MenuSettingsStore _settingsStore = new();
    private readonly MenuSettingsApplier _settingsApplier = new();
    private readonly GameContent _content;
    private bool _initialized;

    /// <summary>组合根使用的构造：接真实配置存储、应用副作用与已加载内容。</summary>
    internal MenuController(MenuMod model, GameContent content) : base(model)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        Model.Modes = GameModeCatalog.Parse(content.GameModes);
    }

    /// <summary>是否已完成首次加载与应用。</summary>
    public bool IsInitialized => _initialized;

    /// <summary>当前设置快照，供视图渲染初始值。</summary>
    public MenuSettings Settings => Model.Settings;

    /// <summary>最近选择的模式；由组合根在模式选择后调用 <see cref="SelectMode"/> 记录。</summary>
    public GameMode? LastSelectedMode => Model.LastSelectedMode;
    internal IReadOnlyList<GameModeInfo> Modes => Model.Modes;
    internal GameModeInfo GetMode(GameMode mode) => Modes.First(info => info.Mode == mode);

    /// <summary>内容包是否开放该模式；目录中缺失或标记为未开放都按关闭处理。</summary>
    public bool IsModeAvailable(GameMode mode)
    {
        foreach (GameModeInfo info in Modes)
        {
            if (info.Mode == mode) return info.Available;
        }

        return false;
    }

    /// <summary>已加载内容（模式目录之外的数据由各功能 Controller 直接持有）。</summary>
    internal GameContent Content => _content;

    /// <summary>
    /// 首次调用时从 <c>user://menu.cfg</c> 加载并应用音量 / 显示设置；重复调用为无操作，
    /// 因此菜单界面每次打开都可以安全调用。
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        MenuSettings settings = _settingsStore.Load();
        Model.Settings = settings;
        _settingsApplier.ApplyVolume(settings.MasterVolume);
        _settingsApplier.ApplyDisplay(settings);
        InternalBus.Send(MenuEvents.SettingsChanged, settings);
    }

    /// <summary>调整主音量并即时生效；不落盘（拖动结束后由 <see cref="SaveSettings"/> 统一保存）。</summary>
    public void SetMasterVolume(float value)
    {
        MenuSettings settings = Model.Settings with { MasterVolume = Math.Clamp(value, 0f, 1f) };
        Commit(settings, applyVolume: true, applyDisplay: false, save: false);
    }

    /// <summary>切换全屏并即时应用、落盘。</summary>
    public void SetFullscreen(bool value)
    {
        MenuSettings settings = Model.Settings with { Fullscreen = value };
        Commit(settings, applyVolume: false, applyDisplay: true, save: true);
    }

    /// <summary>切换垂直同步并即时应用、落盘。</summary>
    public void SetVsync(bool value)
    {
        MenuSettings settings = Model.Settings with { Vsync = value };
        Commit(settings, applyVolume: false, applyDisplay: true, save: true);
    }

    /// <summary>切换帧率显示并落盘（该选项本身不触发引擎副作用）。</summary>
    public void SetShowFps(bool value)
    {
        MenuSettings settings = Model.Settings with { ShowFps = value };
        Commit(settings, applyVolume: false, applyDisplay: false, save: true);
    }

    /// <summary>把当前设置写入配置文件；菜单弹层关闭时调用。</summary>
    public void SaveSettings()
    {
        _settingsStore.Save(Model.Settings);
    }

    /// <summary>记录最近选择的模式，供下次进入模式选择时恢复焦点。</summary>
    public void SelectMode(GameMode mode)
    {
        Model.LastSelectedMode = mode;
        InternalBus.Send(MenuEvents.LastModeChanged, mode);
    }

    /// <summary>
    /// 发布退出意图，由组合根结束应用。
    /// </summary>
    public void RequestQuit() => InternalBus.Send(MenuEvents.QuitRequested, default);

    private void Commit(MenuSettings settings, bool applyVolume, bool applyDisplay, bool save)
    {
        Model.Settings = settings;
        if (applyVolume) _settingsApplier.ApplyVolume(settings.MasterVolume);
        if (applyDisplay) _settingsApplier.ApplyDisplay(settings);
        if (save) _settingsStore.Save(settings);
        InternalBus.Send(MenuEvents.SettingsChanged, settings);
    }
}
