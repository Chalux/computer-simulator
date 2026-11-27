using ComputerSimulator.Modules.Benchmark;
using ComputerSimulator.Modules.Commission;
using ComputerSimulator.Modules.Diagnosis;
using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Menu;
using ComputerSimulator.Modules.Persistence;
using ComputerSimulator.Modules.Workbench;
using Godot;
using KemoCard.Fixed.Godot;
using KemoCard.Frame.Audio;
using KemoCard.Frame.Logging;
using KemoCard.Frame.Mvc;
using KemoCard.Frame.Notification;
using KemoCard.Frame.UI;
using KemoCard.Frame.UI.Def;
using KemoCard.Frame.UI.States;

namespace ComputerSimulator;

/// <summary>
/// 组合根：创建应用级服务与会话，注册生产路由，并把路由意图翻译成 UIManager 的
/// OpenAsync / Close / BackAsync 调用。界面自身不再负责挂载、销毁或持有其它界面的引用。
/// </summary>
public partial class MainRoot : Control, IUiFacadeProvider
{
    private UIManager _ui = null!;
    private GameSaveService _saves = null!;
    private GameContent _content = null!;

    private MenuMod? _menuMod;
    private MenuController? _menuController;
    private CommissionController? _commissionController;
    private BenchmarkController? _commissionBenchmark;
    private WorkbenchController? _assemblyController;
    private BenchmarkController? _assemblyBenchmark;
    private DiagnosisController? _diagnosisController;

    private readonly WorkbenchUiFacade _workbenchFacade = new();
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

        var registry = new UIRuntimeRegistry();
        foreach (UIRegistration registration in AppUiRegistration.Create())
        {
            registry.Register(registration.ToRuntimeEntry());
        }

        _ui = new UIManager();
        AddChild(_ui);
        _ui.Init(new UIManagerInitOpt
        {
            StageRoot = this,
            Registry = registry,
            FacadeProvider = this,
            KnownOwnerModIds =
            [
                MenuMod.ModIdValue,
                WorkbenchUiFacade.ModId,
                CommissionMod.FeatureId,
                DiagnosisMod.ModIdValue,
            ],
            Layers = [EUILayer.Win, EUILayer.Dlg, EUILayer.Loading, EUILayer.Pop],
            TopLayers = [EUILayer.Debug, EUILayer.Notice, EUILayer.Guide],
        });
        _ui.EventDispatcher.On(UIEvent.Close, OnUiClosed, this);

        RedDotService.Configure();
        RedDotService.SetupUpdateNode(GetTree());

        _saves = new GameSaveService(ProjectSettings.GlobalizePath("user://saves"));
        _content = GameContent.Load(_saves.EnabledModIds);

        _menuMod = new MenuMod();
        _menuController = new MenuController(_menuMod, _content);
        _menuMod.InternalBus.On(MenuEvents.QuitRequested, (_, _) => GetTree().Quit(), this);
        _menuController.Initialize();

        _ = OpenInitialAsync();
    }

    public override void _ExitTree()
    {
        CloseWorkbenchSession();
        CloseDiagnosisSession();
        _commissionController?.Close();
        _commissionController = null;
        _commissionBenchmark?.Close();
        _commissionBenchmark = null;
        _workbenchFacade.Commission = null;
        _workbenchFacade.Report = null;

        _ui?.EventDispatcher.OffCaller(this);
        _menuController?.Dispose();
        _menuMod?.Dispose();
        Sound.Configure(NullSoundService.Instance);
        GlobalEvents.Bus.OffAll();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_transitioning || !@event.IsActionPressed("ui_cancel")) return;

        if (CloseTopDialog())
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_ui.NavStack.Count < 2) return;

        GetViewport().SetInputAsHandled();
        RequestBack();
    }

    #region IUiFacadeProvider

    public object? ResolveUiFacade(string ownerModId) => ownerModId switch
    {
        MenuMod.ModIdValue => _menuController,
        WorkbenchUiFacade.ModId => _workbenchFacade,
        CommissionMod.FeatureId => _commissionController,
        DiagnosisMod.ModIdValue => _diagnosisController,
        _ => null,
    };

    #endregion

    #region 路由

    private async Task OpenInitialAsync()
    {
        await OpenRouteAsync(UiRoutes.MainMenuId, default(EmptyPayload));
        KeepOverlayOnTop();
    }

    private void OnModesRequested()
    {
        Transition(() => OpenRouteAsync(UiRoutes.ModeSelectId, default(EmptyPayload)));
    }

    private void OnSettingsRequested()
    {
        if (_ui.GetUIVo(UiRoutes.MenuSettings) != null) return;
        _ = OpenDialogAsync(UiRoutes.MenuSettingsId);
    }

    private void OnCreditsRequested()
    {
        if (_ui.GetUIVo(UiRoutes.MenuCredits) != null) return;
        _ = OpenDialogAsync(UiRoutes.MenuCreditsId);
    }

    private void EnterMode(GameMode mode)
    {
        if (_transitioning) return;

        _menuController?.SelectMode(mode);

        // 内容包标记未开放的模式一律走简报页，即使代码中已有对应玩法。
        if (_menuController?.IsModeAvailable(mode) != true)
        {
            OpenModeTeaser(mode);
            return;
        }

        switch (mode)
        {
            case GameMode.Commission:
                Transition(ShowCommissionAsync);
                return;
            case GameMode.MysteryCrate:
                Transition(() => StartWorkbenchAsync(crate: true, commission: false));
                return;
            case GameMode.Sandbox:
                Transition(() => StartWorkbenchAsync(crate: false, commission: false));
                return;
            case GameMode.Diagnosis:
                Transition(StartDiagnosisAsync);
                return;
            default:
                OpenModeTeaser(mode);
                return;
        }
    }

    private void OpenModeTeaser(GameMode mode)
    {
        Transition(() => OpenRouteAsync(UiRoutes.ModeBriefId, new ModeBriefPayload(mode)));
    }

    private async Task ShowCommissionAsync()
    {
        EnsureCommission();
        await OpenRouteAsync(UiRoutes.CommissionId, default(EmptyPayload));
    }

    private async Task StartWorkbenchAsync(bool crate, bool commission)
    {
        if (commission)
        {
            EnsureCommission();
            CloseWorkbenchSession();
            _workbenchFacade.Commission = _commissionController;
            _workbenchFacade.Report = _commissionBenchmark;
        }
        else
        {
            CloseWorkbenchSession();
            _assemblyBenchmark = new BenchmarkController(new BenchmarkMod());
            _assemblyBenchmark.SetComparisonEnabled(!crate);
            _assemblyController = new WorkbenchController(
                new WorkbenchMod(), _assemblyBenchmark, crate, _content);
            _workbenchFacade.Assembly = _assemblyController;
            _workbenchFacade.Commission = null;
            _workbenchFacade.Report = _assemblyBenchmark;
        }

        await OpenRouteAsync(UiRoutes.WorkbenchId, new WorkbenchRoutePayload(commission));
    }

    private async Task StartDiagnosisAsync()
    {
        CloseDiagnosisSession();
        _diagnosisController = new DiagnosisController(new DiagnosisMod(), _content);
        await OpenRouteAsync(UiRoutes.DiagnosisId, default(EmptyPayload));
    }

    private void OnCommissionAccepted()
    {
        Transition(() => StartWorkbenchAsync(crate: false, commission: true));
    }

    private void OnCommissionDelivered()
    {
        // 工作台交付后回到仍处于下层、未销毁的委托简介；关闭回调会重新读取回执。
        RequestBack();
    }

    private void RequestBack()
    {
        if (_transitioning) return;
        if (_ui.NavStack.Count < 2) return;
        Transition(BackOneAsync);
    }

    private async Task BackOneAsync()
    {
        await _ui.BackAsync();
        KeepOverlayOnTop();
    }

    private Task<UIVo?> OpenRouteAsync<TPayload>(UiId<TPayload> id, TPayload payload)
    {
        return OpenRouteCoreAsync(id, payload, dialog: false);
    }

    private Task<UIVo?> OpenDialogAsync<TPayload>(UiId<TPayload> id, TPayload payload)
    {
        return OpenRouteCoreAsync(id, payload, dialog: true);
    }

    private Task<UIVo?> OpenDialogAsync(UiId<EmptyPayload> id)
    {
        return OpenDialogAsync(id, default(EmptyPayload));
    }

    private async Task<UIVo?> OpenRouteCoreAsync<TPayload>(
        UiId<TPayload> id, TPayload payload, bool dialog)
    {
        UIVo? vo = await _ui.OpenAsync(id, payload, new UIOpenOpt
        {
            CacheTime = 0,
            AnimType = EAnimType.None,
            HideBelow = !dialog,
            NoCover = false,
        });

        if (vo == null)
        {
            AppLog.Error($"路由打开失败：<{id.Value}>", "UI");
            return null;
        }

        AttachRoute(vo);
        KeepOverlayOnTop();
        return vo;
    }

    private void AttachRoute(UIVo vo)
    {
        switch (vo.Id)
        {
            case UiRoutes.MainMenu when vo.UI is MainMenuScreen menu:
                menu.ModesRequested -= OnModesRequested;
                menu.ModesRequested += OnModesRequested;
                menu.SettingsRequested -= OnSettingsRequested;
                menu.SettingsRequested += OnSettingsRequested;
                menu.CreditsRequested -= OnCreditsRequested;
                menu.CreditsRequested += OnCreditsRequested;
                break;

            case UiRoutes.ModeSelect when vo.UI is ModeSelectScreen select:
                select.ModeSelected -= EnterMode;
                select.ModeSelected += EnterMode;
                select.BackRequested -= RequestBack;
                select.BackRequested += RequestBack;
                break;

            case UiRoutes.ModeBrief when vo.UI is ModeTeaserScreen brief:
                brief.BackRequested -= RequestBack;
                brief.BackRequested += RequestBack;
                break;

            case UiRoutes.Commission when vo.UI is CommissionScreen commission:
                commission.Accepted -= OnCommissionAccepted;
                commission.Accepted += OnCommissionAccepted;
                commission.BackRequested -= RequestBack;
                commission.BackRequested += RequestBack;
                break;

            case UiRoutes.Workbench when vo.UI is ClearanceWorkbench workbench:
                workbench.BackRequested -= RequestBack;
                workbench.BackRequested += RequestBack;
                workbench.CommissionDelivered -= OnCommissionDelivered;
                workbench.CommissionDelivered += OnCommissionDelivered;
                break;

            case UiRoutes.Diagnosis when vo.UI is DiagnosisCaseScreen diagnosis:
                diagnosis.BackRequested -= RequestBack;
                diagnosis.BackRequested += RequestBack;
                break;
        }
    }

    private void OnUiClosed(UIClosePayload payload, IEventListener<UIClosePayload> _)
    {
        switch (payload.Vo.Id)
        {
            case UiRoutes.Workbench:
                bool commissionMode = _workbenchFacade.Commission is not null;
                CloseWorkbenchSession();
                // 与旧流程一致：离开委托工作台后回到委托简介，并重新读取回执/草案。
                if (commissionMode) _commissionController?.OpenBrief();
                break;
            case UiRoutes.Diagnosis:
                CloseDiagnosisSession();
                break;
            case UiRoutes.MenuSettings:
            case UiRoutes.MenuCredits:
                (_ui.GetUIVo(UiRoutes.MainMenu)?.UI as MainMenuScreen)?.FocusInitial();
                break;
        }
    }

    private bool CloseTopDialog()
    {
        foreach (string id in new[] { UiRoutes.MenuSettings, UiRoutes.MenuCredits })
        {
            if (_ui.GetUIVo(id)?.IsOpen != true) continue;
            _ui.Close(id);
            return true;
        }

        return false;
    }

    #endregion

    #region 会话

    private void EnsureCommission()
    {
        _commissionBenchmark ??= new BenchmarkController(new BenchmarkMod());
        _commissionController ??= new CommissionController(
            new CommissionMod(),
            _commissionBenchmark,
            _content,
            new CommissionReceiptStore(_saves));
    }

    private void CloseWorkbenchSession()
    {
        _assemblyController?.Close();
        _assemblyController = null;
        _assemblyBenchmark?.Close();
        _assemblyBenchmark = null;
        _workbenchFacade.Assembly = null;
        _workbenchFacade.Commission = null;
        _workbenchFacade.Report = null;
    }

    private void CloseDiagnosisSession()
    {
        _diagnosisController?.Close();
        _diagnosisController = null;
    }

    #endregion

    #region 转场与绘制顺序

    /// <summary>黑场过渡：压黑后执行异步路由动作，再淡出。</summary>
    private async void Transition(Func<Task> midpoint)
    {
        if (_transitioning) return;
        _transitioning = true;

        GetViewport().GuiGetFocusOwner()?.ReleaseFocus();

        ColorRect fade = EnsureFade();
        fade.Visible = true;
        fade.MoveToFront();

        Tween fadeIn = CreateTween();
        fadeIn.TweenProperty(fade, "color:a", 1f, 0.20).SetEase(Tween.EaseType.In);
        await ToSignal(fadeIn, Tween.SignalName.Finished);

        try
        {
            await midpoint();
        }
        catch (Exception ex)
        {
            AppLog.Error($"路由切换失败：{ex.Message}", "UI");
        }

        KeepOverlayOnTop();

        await ToSignal(GetTree().CreateTimer(0.04), SceneTreeTimer.SignalName.Timeout);

        Tween fadeOut = CreateTween();
        fadeOut.TweenProperty(fade, "color:a", 0f, 0.30).SetEase(Tween.EaseType.Out);
        await ToSignal(fadeOut, Tween.SignalName.Finished);

        if (_fade != null) _fade.Visible = false;
        _transitioning = false;
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
    /// UIManager 本身是普通 Node，只能前移它挂载的 CanvasItem 根节点。
    /// </summary>
    private void KeepOverlayOnTop()
    {
        if (_ui != null)
        {
            foreach (Node child in _ui.GetChildren())
            {
                if (child is CanvasItem canvasItem) canvasItem.MoveToFront();
            }
        }

        _fade?.MoveToFront();
    }

    #endregion
}
