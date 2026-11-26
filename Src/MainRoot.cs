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
    private ClearanceWorkbench? _workbench;
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
        // 装配工作台里按 ESC 回到主菜单（当前没有其他使用 ESC 的界面）。
        if (_workbench != null && @event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            Transition(ReturnToMenu);
        }
    }

    private void ShowMenu()
    {
        if (_menu != null) return;

        var menu = new MainMenuScreen();
        AddChild(menu);
        menu.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        menu.BuildRequested += () => Transition(StartBuild);
        _menu = menu;

        KeepOverlayOnTop();
    }

    private void StartBuild()
    {
        if (_workbench != null) return;

        var workbench = new ClearanceWorkbench();
        AddChild(workbench);
        workbench.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        workbench.MenuRequested += () => Transition(ReturnToMenu);
        _workbench = workbench;

        if (_menu != null)
        {
            _menu.QueueFree();
            _menu = null;
        }

        KeepOverlayOnTop();
    }

    private void ReturnToMenu()
    {
        if (_workbench == null) return;

        _workbench.QueueFree();
        _workbench = null;
        ShowMenu();
    }

    /// <summary>
    /// 黑场过渡：先压黑，切换内容，再淡出。切换点由调用方提供，避免加载时闪一帧旧画面。
    /// </summary>
    private void Transition(Action midpoint)
    {
        if (_transitioning) return;
        _transitioning = true;

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
