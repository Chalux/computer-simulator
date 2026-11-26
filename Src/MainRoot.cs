using ComputerSimulator.Modules.Diagnosis;
using ComputerSimulator.Modules.Menu;
using ComputerSimulator.Modules.Workbench;
using Godot;
using KemoCard.Fixed.Godot;
using KemoCard.Frame.Audio;
using KemoCard.Frame.Logging;
using KemoCard.Frame.Mvc;
using KemoCard.Frame.Notification;
using KemoCard.Frame.UI;
using KemoCard.Frame.UI.Def;

namespace ComputerSimulator;

public partial class MainRoot : Control
{
    private UIManager? _ui;
    private MainMenuScreen? _menu;
    private ModeSelectScreen? _modeSelect;
    private ModeTeaserScreen? _modePage;
    private ClearanceWorkbench? _workbench;
    private DiagnosisCaseScreen? _diagnosis;
    private GameMode? _lastMode;
    private ColorRect? _fade;
    private bool _transitioning;

    public override void _Ready()
    {
        var log = new GodotAppLog();
        AppLog.Configure(log);
        EventDispatcher.Configure(new GodotEventDispatcherLogger(log));

        var sound = new SoundManager();
        AddChild(sound);
        Sound.Configure(sound);

        _ui = new UIManager();
        AddChild(_ui);
        _ui.Init(new UIManagerInitOpt
        {
            StageRoot = this,
            Registry = new UIRuntimeRegistry(),
            Layers = [EUILayer.Win, EUILayer.Dlg, EUILayer.Loading, EUILayer.Pop],
            TopLayers = [EUILayer.Debug, EUILayer.Notice, EUILayer.Guide],
        });

        RedDotService.Configure();
        RedDotService.SetupUpdateNode(GetTree());

        ShowMenu();
    }

    public override void _ExitTree()
    {
        Sound.Configure(NullSoundService.Instance);
        GlobalEvents.Bus.OffAll();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_transitioning || !@event.IsActionPressed("ui_cancel")) return;

        // 模式内（简报页、故障诊断或装配工作台）先回模式选择，模式选择再回主菜单。
        if (_workbench != null || _diagnosis != null || _modePage != null)
        {
            GetViewport().SetInputAsHandled();
            Transition(ShowModeSelect);
            return;
        }

        if (_modeSelect != null)
        {
            GetViewport().SetInputAsHandled();
            Transition(ShowMenu);
        }
    }

    private void ShowMenu()
    {
        if (_menu != null) return;

        ClearScreens();
        var menu = new MainMenuScreen();
        AddChild(menu);
        menu.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        menu.ModesRequested += () => Transition(ShowModeSelect);
        _menu = menu;

        KeepOverlayOnTop();
    }

    private void ShowModeSelect()
    {
        if (_modeSelect != null) return;

        ClearScreens();
        var select = new ModeSelectScreen { InitialFocus = _lastMode };
        AddChild(select);
        select.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        select.ModeSelected += EnterMode;
        select.BackRequested += () => Transition(ShowMenu);
        _modeSelect = select;

        KeepOverlayOnTop();
    }

    /// <summary>沙盒与神秘箱共用现有工作台，故障诊断进入 04-01 专用案例，其余三个模式进入简报页。</summary>
    private void EnterMode(GameMode mode)
    {
        if (_transitioning) return;

        _lastMode = mode;
        if (mode is GameMode.Sandbox or GameMode.MysteryCrate)
        {
            bool crate = mode == GameMode.MysteryCrate;
            Transition(() => StartSandbox(crate));
            return;
        }

        if (mode == GameMode.Diagnosis)
        {
            Transition(StartDiagnosis);
            return;
        }

        Transition(() => ShowModeBrief(mode));
    }

    private void ShowModeBrief(GameMode mode)
    {
        if (_modePage != null) return;

        ClearScreens();
        var page = new ModeTeaserScreen { Mode = mode };
        AddChild(page);
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        page.BackRequested += () => Transition(ShowModeSelect);
        _modePage = page;

        KeepOverlayOnTop();
    }

    private void StartSandbox(bool crate = false)
    {
        if (_workbench != null) return;

        ClearScreens();
        var workbench = new ClearanceWorkbench { CrateMode = crate };
        AddChild(workbench);
        workbench.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        workbench.BackRequested += () => Transition(ShowModeSelect);
        _workbench = workbench;

        KeepOverlayOnTop();
    }

    private void StartDiagnosis()
    {
        if (_diagnosis != null) return;

        ClearScreens();
        var diagnosis = new DiagnosisCaseScreen();
        AddChild(diagnosis);
        diagnosis.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        diagnosis.BackRequested += () => Transition(ShowModeSelect);
        _diagnosis = diagnosis;

        KeepOverlayOnTop();
    }

    /// <summary>退出当前模式时立即解除引用，节点在本帧末释放并触发工作台/诊断案例的规则环境清理。</summary>
    private void ClearScreens()
    {
        _menu?.QueueFree();
        _menu = null;
        _modeSelect?.QueueFree();
        _modeSelect = null;
        _modePage?.QueueFree();
        _modePage = null;
        _workbench?.QueueFree();
        _workbench = null;
        _diagnosis?.QueueFree();
        _diagnosis = null;
    }

    /// <summary>
    /// 黑场过渡：先压黑，切换内容，再淡出。切换点由调用方提供，避免加载时闪一帧旧画面。
    /// </summary>
    private void Transition(Action midpoint)
    {
        if (_transitioning) return;
        _transitioning = true;

        // 转场期间释放焦点，避免按住确认键或连按在淡出阶段重复触发旧页面。
        GetViewport().GuiGetFocusOwner()?.ReleaseFocus();

        ColorRect fade = EnsureFade();
        fade.Visible = true;
        fade.MoveToFront();

        Tween tween = CreateTween();
        tween.TweenProperty(fade, "color:a", 1f, 0.20).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(() =>
        {
            midpoint();
            KeepOverlayOnTop();
        }));
        tween.TweenInterval(0.04);
        tween.TweenProperty(fade, "color:a", 0f, 0.30).SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() =>
        {
            _transitioning = false;
            if (_fade != null) _fade.Visible = false;
        }));
    }

    private ColorRect EnsureFade()
    {
        if (_fade != null) return _fade;

        _fade = new ColorRect
        {
            Color = new Color(0.008f, 0.020f, 0.028f, 0f),
            MouseFilter = MouseFilterEnum.Stop,
            Visible = false,
        };
        AddChild(_fade);
        _fade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return _fade;
    }

    /// <summary>
    /// 保持绘制顺序：UI 框架层在内容之上，黑场在最后。
    /// UI 层是普通 Control（非 CanvasLayer），不显式前移会被后加入的工作台盖住。
    /// </summary>
    private void KeepOverlayOnTop()
    {
        // UIManager 本身是普通 Node，只能前移它挂载的 CanvasItem 根节点。
        if (_ui != null)
        {
            foreach (Node child in _ui.GetChildren())
            {
                if (child is CanvasItem canvasItem) canvasItem.MoveToFront();
            }
        }
        _fade?.MoveToFront();
    }
}
