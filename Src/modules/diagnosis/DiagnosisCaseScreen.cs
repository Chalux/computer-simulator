using System.Text;
using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Inspection;
using ComputerSimulator.Modules.Menu;
using ComputerSimulator.Modules.Simulation;
using Godot;
using KemoCard.Frame.Mvc;
using KemoCard.Frame.UI;
using KemoCard.Frame.UI.Base;

namespace ComputerSimulator.Modules.Diagnosis;

/// <summary>
/// 故障诊断页面：布局与持久控件在 <c>diagnosis_case.tscn</c>，
/// 本类只渲染注入内容与 Controller 快照、播放动效并转发意图。
/// 状态订阅由框架按归属门面自动绑定到 <see cref="EDiagnosisEvent.StateChanged"/>。
/// </summary>
public partial class DiagnosisCaseScreen : BaseWin
{
    public event Action? BackRequested;

    private static readonly Color Muted = new("#a2b6c0");
    private static readonly Color Warn = new("#f5c877");
    private static readonly Color Bad = new("#ff9689");
    private static readonly Color Dim = new("#6e7f87");

    private DiagnosisController _controller = null!;
    private DiagnosisSnapshot _state = null!;
    private bool _syncingSelection;

    private Control _page = null!;
    private StatusDot _statusDot = null!;
    private Label _statusTag = null!;
    private OptionButton _psuChoice = null!;
    private Label _candidatePreview = null!;
    private Label _allowance = null!;
    private Button _install = null!;
    private Button _retest = null!;
    private Button _reset = null!;
    private Label _repairState = null!;
    private Label _retestResult = null!;

    private Label _configCase = null!, _configBoard = null!, _configCooler = null!;
    private Label _configGpu = null!, _configRam = null!, _configPsu = null!;
    private Label _fitStatus = null!;
    private StatusDot _fitDot = null!;
    private Label _fitGpu = null!, _fitPsu = null!, _fitHeight = null!, _fitMemory = null!, _fitIssues = null!;
    private SectionDrawing _section = null!;
    private Label _sectionCaption = null!;
    private Label _faultStatus = null!, _faultLoad = null!, _faultPsu = null!, _faultWorkload = null!;
    private Label _faultCurve = null!, _faultMessage = null!, _faultClue = null!;

    public override string UIId => UiRoutes.Diagnosis;
    public override string UIDir => UiRoutes.DiagnosisDir;

    protected override void OnCreate()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = MenuPalette.CreateTheme();
        MouseFilter = MouseFilterEnum.Stop;

        _controller = Facade<DiagnosisController>();
        _state = _controller.State;

        CacheNodes();
        StylePage();
        PopulateCatalog();

        // 题面文案由内容 Mod 提供；场景中的文本只作为编辑器预览的回退。
        GetNode<Label>("%Kicker").Text = $"DIAGNOSTICS / CASE {_controller.Case.Id}";
        GetNode<Label>("%Title").Text = _controller.Case.Title;
        GetNode<Label>("%Subtitle").Text = _controller.Case.Subtitle;
        GetNode<Label>("%Brief").Text = _controller.Case.CustomerBrief;
        GetNode<Label>("%RepairHint").Text =
            $"本案可用替换额度 {_controller.Case.ReplacementAllowance} 次。安装备件后运行复测，以实际负载结果验收。";
    }

    protected override void InitEvent()
    {
        OnClicks(GetNode<Button>("%BackButton"), RequestBack);
        Binder.OnItemSelected(_psuChoice, OnPsuSelected);
        Binder.OnPressed(_install, InstallSelected);
        Binder.OnPressed(_retest, RunRetest);
        Binder.OnPressed(_reset, ResetCase);
    }

    protected override void OnOpen()
    {
        _state = _controller.State;
        Render(_state);
        PlayIntro();
    }

    protected override void UpdateView()
    {
    }

    private void CacheNodes()
    {
        _page = GetNode<Control>("%Page");
        _statusTag = GetNode<Label>("%StatusTag");
        _psuChoice = GetNode<OptionButton>("%PsuChoice");
        _candidatePreview = GetNode<Label>("%CandidatePreview");
        _allowance = GetNode<Label>("%Allowance");
        _install = GetNode<Button>("%InstallButton");
        _retest = GetNode<Button>("%RetestButton");
        _reset = GetNode<Button>("%ResetButton");
        _repairState = GetNode<Label>("%RepairState");
        _retestResult = GetNode<Label>("%RetestResult");

        _configCase = GetNode<Label>("%ConfigCase");
        _configBoard = GetNode<Label>("%ConfigBoard");
        _configCooler = GetNode<Label>("%ConfigCooler");
        _configGpu = GetNode<Label>("%ConfigGpu");
        _configRam = GetNode<Label>("%ConfigRam");
        _configPsu = GetNode<Label>("%ConfigPsu");

        _fitStatus = GetNode<Label>("%FitStatusLabel");
        _fitGpu = GetNode<Label>("%FitGpu");
        _fitPsu = GetNode<Label>("%FitPsu");
        _fitHeight = GetNode<Label>("%FitHeight");
        _fitMemory = GetNode<Label>("%FitMemory");
        _fitIssues = GetNode<Label>("%FitIssues");
        _section = GetNode<SectionDrawing>("%Section");
        _sectionCaption = GetNode<Label>("%SectionCaption");

        _faultStatus = GetNode<Label>("%FaultStatus");
        _faultLoad = GetNode<Label>("%FaultLoad");
        _faultPsu = GetNode<Label>("%FaultPsu");
        _faultWorkload = GetNode<Label>("%FaultWorkload");
        _faultCurve = GetNode<Label>("%FaultCurve");
        _faultMessage = GetNode<Label>("%FaultMessage");
        _faultClue = GetNode<Label>("%FaultClue");

        var statusDotHost = GetNode<Control>("%StatusDotHost");
        _statusDot = new StatusDot
        {
            Tint = MenuPalette.Ash,
            Radius = 2f,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        statusDotHost.AddChild(_statusDot);

        var fitDotHost = GetNode<Control>("%FitDotHost");
        _fitDot = new StatusDot
        {
            Tint = MenuPalette.Ash,
            Radius = 2f,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        fitDotHost.AddChild(_fitDot);
    }

    private void StylePage()
    {
        StyleDefaultLabels(GetNode<Control>("%Page"));

        StyleLabel("%Kicker", 9, MenuPalette.Alpha(MenuPalette.Ash, 0.78f), MenuPalette.Mono);
        StyleLabel("%Title", 34, MenuPalette.Platinum);
        StyleLabel("%Subtitle", 13, MenuPalette.Alpha(MenuPalette.Ash, 0.86f));
        StyleLabel("%CustomerTitle", 15, MenuPalette.Platinum);
        StyleLabel("%Brief", 13, MenuPalette.Alpha(MenuPalette.Platinum, 0.86f));
        StyleLabel("%FixedTitle", 15, MenuPalette.Platinum);
        StyleLabel("%FitTitle", 15, MenuPalette.Platinum);
        StyleLabel("%FaultTitle", 16, MenuPalette.Platinum);
        StyleLabel("%RepairTitle", 16, MenuPalette.Platinum);
        StyleLabel("%FaultTag", 8, MenuPalette.Alpha(MenuPalette.Ash, 0.45f), MenuPalette.Mono);
        StyleLabel("%RepairTag", 8, MenuPalette.Alpha(MenuPalette.Ash, 0.45f), MenuPalette.Mono);
        StyleLabel("%CurveTitle", 12, MenuPalette.Alpha(MenuPalette.Platinum, 0.82f));
        StyleLabel("%RecordTitle", 12, MenuPalette.Alpha(MenuPalette.Platinum, 0.82f));
        StyleLabel("%FaultStatus", 14, Muted);
        StyleLabel("%FaultLoad", 19, MenuPalette.Platinum);
        StyleLabel("%FaultPsu", 11, Muted);
        StyleLabel("%FaultWorkload", 11, Muted);
        StyleLabel("%FaultCurve", 11, Muted);
        StyleLabel("%FaultMessage", 12, Bad);
        StyleLabel("%FaultClue", 12, MenuPalette.Alpha(MenuPalette.Platinum, 0.88f));
        StyleLabel("%Allowance", 11, MenuPalette.Alpha(MenuPalette.Signal, 0.9f));
        StyleLabel("%CandidatePreview", 11, Muted);
        StyleLabel("%RepairState", 12, Muted);
        StyleLabel("%RetestResult", 12, Muted);
        StyleLabel("%FooterNote", 10, MenuPalette.Alpha(MenuPalette.Ash, 0.48f));
        StyleLabel("%FooterEsc", 10, MenuPalette.Alpha(MenuPalette.Ash, 0.42f));
        StyleLabel("%SectionCaption", 11, MenuPalette.Alpha(MenuPalette.Ash, 0.7f));

        foreach (Label label in new[]
                 {
                     _candidatePreview, _repairState, _retestResult, _faultCurve, _faultMessage,
                     _faultClue, _sectionCaption, _fitIssues,
                 })
        {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }

        _psuChoice.AddThemeFontSizeOverride("font_size", 13);
        _psuChoice.FocusMode = FocusModeEnum.All;
        _psuChoice.CustomMinimumSize = new Vector2(0, 34);

        foreach (string gridName in new[] { "%ConfigGrid", "%FitGrid" })
        {
            int index = 0;
            foreach (Node child in GetNode<GridContainer>(gridName).GetChildren())
            {
                if (child is Label label && index % 2 == 0)
                {
                    label.AddThemeFontSizeOverride("font_size", 11);
                    label.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Ash, 0.72f));
                    label.CustomMinimumSize = new Vector2(64, 0);
                }
                index++;
            }
        }

        foreach (Button button in new[] { _install, _retest, _reset })
        {
            StyleActionButton(button);
        }

        StyleTextButton(GetNode<Button>("%BackButton"));

        foreach (string panel in new[] { "%FaultPanel", "%RepairPanel" })
        {
            PanelContainer node = GetNode<PanelContainer>(panel);
            StyleBoxFlat style = MenuPalette.Box(MenuPalette.Alpha(MenuPalette.Graphite, 0.96f),
                MenuPalette.Alpha(MenuPalette.Steel, 0.95f), 1);
            style.ContentMarginLeft = 16;
            style.ContentMarginRight = 16;
            style.ContentMarginTop = 12;
            style.ContentMarginBottom = 14;
            node.AddThemeStyleboxOverride("panel", style);
        }
    }

    private void StyleDefaultLabels(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is Label label)
            {
                label.AddThemeFontOverride("font", MenuPalette.Text);
                label.AddThemeFontSizeOverride("font_size", 12);
                label.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Platinum, 0.86f));
            }

            StyleDefaultLabels(child);
        }
    }

    private void StyleLabel(string name, int size, Color color, Font? font = null)
    {
        var label = GetNode<Label>(name);
        label.AddThemeFontOverride("font", font ?? MenuPalette.Text);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
    }

    private static void StyleActionButton(Button button)
    {
        button.FocusMode = FocusModeEnum.All;
        button.MouseDefaultCursorShape = CursorShape.PointingHand;
        button.CustomMinimumSize = new Vector2(0, 34);
        button.AddThemeFontOverride("font", MenuPalette.Text);
        button.AddThemeFontSizeOverride("font_size", 13);
        button.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Platinum, 0.88f));
        button.AddThemeColorOverride("font_hover_color", MenuPalette.Platinum);
        button.AddThemeColorOverride("font_pressed_color", MenuPalette.Signal);
        button.AddThemeColorOverride("font_focus_color", MenuPalette.Platinum);
        button.AddThemeColorOverride("font_disabled_color", MenuPalette.Alpha(MenuPalette.Ash, 0.36f));

        StyleBoxFlat normal = MenuPalette.Box(Colors.Transparent, MenuPalette.Alpha(MenuPalette.Steel, 0.95f), 1);
        StyleBoxFlat hover = MenuPalette.Box(new Color(1f, 1f, 1f, 0.04f), MenuPalette.Alpha(MenuPalette.Ash, 0.7f), 1);
        StyleBoxFlat pressedStyle = MenuPalette.Box(new Color(1f, 1f, 1f, 0.07f), MenuPalette.Alpha(MenuPalette.Platinum, 0.85f), 1);
        StyleBoxFlat focus = MenuPalette.Box(Colors.Transparent, MenuPalette.Alpha(MenuPalette.Signal, 0.75f), 1);
        StyleBoxFlat disabled = MenuPalette.Box(Colors.Transparent, MenuPalette.Alpha(MenuPalette.Steel, 0.5f), 1);

        foreach (StyleBoxFlat box in new[] { normal, hover, pressedStyle, focus, disabled })
        {
            box.ContentMarginLeft = 10;
            box.ContentMarginRight = 10;
            box.ContentMarginTop = 7;
            box.ContentMarginBottom = 7;
        }

        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressedStyle);
        button.AddThemeStyleboxOverride("focus", focus);
        button.AddThemeStyleboxOverride("disabled", disabled);
    }

    private static void StyleTextButton(Button button)
    {
        button.Flat = true;
        button.FocusMode = FocusModeEnum.All;
        button.MouseDefaultCursorShape = CursorShape.PointingHand;
        button.CustomMinimumSize = new Vector2(0, 24);
        button.AddThemeFontOverride("font", MenuPalette.Text);
        button.AddThemeFontSizeOverride("font_size", 11);
        button.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Ash, 0.65f));
        button.AddThemeColorOverride("font_hover_color", MenuPalette.Platinum);
        button.AddThemeColorOverride("font_pressed_color", MenuPalette.Signal);
        button.AddThemeColorOverride("font_focus_color", MenuPalette.Platinum);
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private void PopulateCatalog()
    {
        BenchCatalog catalog = _controller.Catalog;

        var chassis = catalog.Cases[_controller.Case.CaseIndex];
        var gpu = catalog.Graphics[_state.Setup.GpuIndex];
        var ram = catalog.Ram[_state.Setup.RamIndex];
        var psu = catalog.PowerSupplies[_controller.Case.FaultyPsuIndex];

        _configCase.Text = $"{chassis.Name} · 显卡限长 {chassis.GpuLimit} / 电源仓 {chassis.PsuLimit} mm";
        _configBoard.Text = catalog.Board.Name;
        _configCooler.Text = $"{catalog.Liquid.Name} · 前置 360（冷排 {catalog.Liquid.CoreThickness} + 风扇 {catalog.Liquid.FanThickness} mm）";
        _configGpu.Text = $"{gpu.Name} · {gpu.Dimensions[0]} mm";
        string slots = string.Join(
            " / ",
            _state.Setup.Slots
                .Where(index => index >= 0 && index < catalog.Board.Slots.Length)
                .Select(index => catalog.Board.Slots[index].Name));
        string slotNote = slots.Length > 0 ? $" · {slots}" : string.Empty;
        _configRam.Text =
            $"{ram.Name} × {_state.Setup.Slots.Length}{slotNote} · {ram.Dimensions[2]} mm 高马甲";
        _configPsu.Text = $"{psu.Name} · {psu.Length} mm · 持续 {psu.Continuous} / 短时 {psu.Pulse} W";

        _psuChoice.Clear();
        for (int i = 0; i < catalog.PowerSupplies.Length; i++)
        {
            PsuInfo candidate = catalog.PowerSupplies[i];
            string suffix = i == _controller.Case.FaultyPsuIndex ? "（同型号备件）" : string.Empty;
            _psuChoice.AddItem($"{candidate.Name}{suffix} · {candidate.Continuous} / {candidate.Pulse} W");
        }
    }

    #region 渲染

    private void Render(DiagnosisSnapshot state)
    {
        _state = state;
        RenderCommon(state);
        RenderDossier(state);

        switch (state.Outcome)
        {
            case DiagnosisOutcome.Pending:
                RenderPending();
                break;
            case DiagnosisOutcome.AwaitingRetest:
                RenderAwaitingRetest(state);
                break;
            default:
                RenderFinished(state);
                break;
        }
    }

    private void RenderCommon(DiagnosisSnapshot state)
    {
        // 仍有额度且未结案时继续允许选择；多额度案件可在复测前再次换件。
        _psuChoice.Disabled = !state.CanChoosePsu;

        if (_psuChoice.Selected != state.SelectedPsuIndex)
        {
            _syncingSelection = true;
            _psuChoice.Select(state.SelectedPsuIndex);
            _syncingSelection = false;
        }

        _install.Disabled = !state.CanInstall;
        _install.Text =
            $"更换为所选电源（剩余额度 {state.ReplacementAllowance} / {_controller.Case.ReplacementAllowance}）";
        _retest.Disabled = !state.CanRetest;
        _reset.Text = "重新开始本案";

        _allowance.Text = $"替换额度  {state.ReplacementAllowance} / {_controller.Case.ReplacementAllowance}";
        SetLabelColor(_allowance, state.ReplacementAllowance > 0
            ? MenuPalette.Alpha(MenuPalette.Signal, 0.9f)
            : Dim);

        DiagnosisCandidate candidate = _controller.EvaluateCandidate(state.SelectedPsuIndex);
        string candidateNote = candidate.SameAsOriginal
            ? "\n同型号备件只用于排除电源个体故障，不会增加承载余量。"
            : string.Empty;
        _candidatePreview.Text =
            $"持续 {candidate.Psu.Continuous} W / 短时 {candidate.Psu.Pulse} W / 建议上界 {candidate.Psu.Recommended} W\n" +
            $"{candidate.Psu.Cable} · 长 {candidate.Psu.Length} mm" + candidateNote;
    }

    private void RenderDossier(DiagnosisSnapshot state)
    {
        FitResult fit = state.Fit;
        BenchCatalog catalog = _controller.Catalog;
        PsuInfo originalPsu = catalog.PowerSupplies[_controller.Case.FaultyPsuIndex];
        PsuInfo installedPsu = catalog.PowerSupplies[state.InstalledPsuIndex];

        int psuReserve = (fit.PsuLimit ?? installedPsu.Length) - installedPsu.Length - (fit.PsuMargin ?? 0);
        string psuOccupancy = psuReserve > 0
            ? $"{installedPsu.Length} mm + 藏线 {psuReserve} mm ≤ 限长 {fit.PsuLimit} mm · 余量 {Signed(fit.PsuMargin)} mm"
            : $"{installedPsu.Length} mm ≤ 限长 {fit.PsuLimit} mm · 余量 {Signed(fit.PsuMargin)} mm";

        _fitStatus.Text = $"{fit.Status} · {FitStatusText(fit.Status)}";
        _fitStatus.AddThemeColorOverride("font_color", StateColor(fit.Status));
        _fitDot.Tint = StateColor(fit.Status);
        _fitDot.QueueRedraw();

        _fitGpu.Text = $"{catalog.Graphics[state.Setup.GpuIndex].Dimensions[0]} mm ≤ 有效 {fit.GpuLimit?.ToString() ?? "—"} mm · 余量 {Signed(fit.GpuMargin)} mm";
        _fitPsu.Text = psuOccupancy;
        _fitHeight.Text = $"限高 {fit.HeightLimit} mm · 余量 {Signed(fit.Headroom)} mm";
        _fitMemory.Text = $"{fit.Capacity} GB · {fit.Channels} 通道 · {fit.Bandwidth:0.0} GB/s";
        _fitIssues.Text = fit.Issues.Length == 0
            ? "无（未触发任何 BLOCKED 项）"
            : string.Join(" / ", fit.Issues.Select(issue => issue.Message));

        _section.Display(fit);
        _sectionCaption.Text = $"正交剖面 · 显卡与前置冷排之间仍有 {Signed(fit.GpuMargin)} mm 余量";

        LoadResult fault = state.Baseline;
        _faultStatus.Text = $"{fault.Status} · {LoadStatusText(fault.Status)}";
        _faultStatus.AddThemeColorOverride("font_color", StateColor(fault.Status));
        _faultLoad.Text = $"持续负载 {fault.Sustained} W   /   瞬时峰值 {fault.Peak} W";
        _faultPsu.Text =
            $"原装 {originalPsu.Name}：持续承载 {originalPsu.Continuous} W / 短时承载 {originalPsu.Pulse} W / 建议上界 {originalPsu.Recommended} W";
        string workload = catalog.Benchmark.Workloads[state.Setup.WorkloadIndex].Name;
        string airflow = catalog.Benchmark.Airflows[state.Setup.AirflowIndex].Name;
        _faultWorkload.Text = $"测试工况：{workload} · {airflow}";
        _faultCurve.Text = BuildCurve(fault, originalPsu);
        _faultMessage.Text = fault.Message;
        _faultClue.Text = BuildClue(originalPsu);
    }

    private void RenderPending()
    {
        SetStatusTag("故障待修", Muted);
        _repairState.Text = "未更换任何部件。先核对故障复现数据，再选择替换电源。";
        SetLabelColor(_repairState, Muted);
        _retestResult.Text = "尚未进行复测。更换电源后运行负载复测，以实际结果验收。";
        SetLabelColor(_retestResult, Muted);
    }

    private void RenderAwaitingRetest(DiagnosisSnapshot state)
    {
        PsuInfo installed = _controller.Catalog.PowerSupplies[state.InstalledPsuIndex];
        SetStatusTag("待复测", Warn);
        string note = state.InstalledPsuIndex == _controller.Case.FaultyPsuIndex
            ? " 同型号备件不会改变功率余量。"
            : string.Empty;

        bool blocked = string.Equals(state.Fit.Status, "BLOCKED", StringComparison.Ordinal);
        _repairState.Text = blocked
            ? $"已安装 {installed.Name}，但当前配置未通过装配检查，无法运行负载复测。{note}"
            : $"已安装 {installed.Name}（持续 {installed.Continuous} / 短时 {installed.Pulse} W）。下一步运行负载复测。{note}";
        SetLabelColor(_repairState, MenuPalette.Alpha(MenuPalette.Platinum, 0.9f));
        _retestResult.Text = blocked
            ? $"{string.Join(" / ", state.Fit.Issues.Select(issue => issue.Message))}\n请更换其他电源或重新开始本案。"
            : "尚未进行复测。更换电源后运行负载复测，以实际结果验收。";
        SetLabelColor(_retestResult, blocked ? Warn : Muted);
    }

    private void RenderFinished(DiagnosisSnapshot state)
    {
        LoadResult result = state.Retest!;
        PsuInfo installed = _controller.Catalog.PowerSupplies[state.InstalledPsuIndex];
        Color color = StateColor(result.Status);
        string verdict;
        string repairState;

        if (state.Outcome == DiagnosisOutcome.Failed)
        {
            verdict = "验收失败：复测仍触发保护断电。请重新开始本案。";
            repairState = "复测未通过。电源仍在瞬时脉冲下断电。";
            SetStatusTag("维修失败 · BLOCKED", color);
        }
        else
        {
            bool optimal = state.Outcome == DiagnosisOutcome.RepairedOptimal;
            verdict = optimal
                ? "验收通过：持续负载与瞬时峰值均在电源余量内。"
                : "验收通过：负载未断电，但峰值超过建议上界，瞬时余量偏低。";
            repairState = "复测完成。本案已结案。";
            SetStatusTag(optimal ? "修复完成 · OPTIMAL" : "修复完成 · DEGRADED", color);
        }

        _repairState.Text = repairState;
        SetLabelColor(_repairState, color);
        _retestResult.Text =
            $"复测结果  {result.Status} · {LoadStatusText(result.Status)}\n" +
            $"持续负载 {result.Sustained} W / 瞬时峰值 {result.Peak} W\n" +
            $"{installed.Name}：持续承载 {installed.Continuous} W / 短时承载 {installed.Pulse} W / 建议上界 {installed.Recommended} W\n" +
            $"{result.Message}\n{verdict}";
        SetLabelColor(_retestResult, color);
    }

    [EventSubscribe(EDiagnosisEvent.StateChanged)]
    private void OnStateChanged(DiagnosisSnapshot state) => Render(state);

    #endregion

    #region 行为

    private void OnPsuSelected(long index)
    {
        if (_syncingSelection) return;
        _controller.SelectPsu((int)index);
    }

    private void InstallSelected()
    {
        if (_controller.InstallSelected()) _retest.GrabFocus();
    }

    private void RunRetest()
    {
        if (_controller.RunRetest()) _reset.GrabFocus();
    }

    private void ResetCase()
    {
        _controller.ResetCase();
        _psuChoice.GrabFocus();
    }

    private void RequestBack() => BackRequested?.Invoke();

    #endregion

    #region 动效

    private void PlayIntro()
    {
        _page.Modulate = MenuPalette.Alpha(Colors.White, 0f);
        Tween tween = CreateTween();
        tween.TweenProperty(_page, "modulate:a", 1f, 0.42).SetEase(Tween.EaseType.Out);

        tween.TweenInterval(0.18);
        tween.TweenCallback(Callable.From(GrabInitialFocus));
    }

    private void GrabInitialFocus()
    {
        if (!IsInstanceValid(this) || _state.IsFinished) return;
        if (GetViewport().GuiGetFocusOwner() != null) return;
        _psuChoice.GrabFocus();
    }

    #endregion

    #region 文案与小工具

    private string BuildCurve(LoadResult result, PsuInfo psu)
    {
        LoadSample[] samples = result.Samples ?? Array.Empty<LoadSample>();
        if (samples.Length == 0) return "（规则未返回采样曲线）";

        var lines = new StringBuilder();
        foreach (LoadSample sample in samples)
        {
            int limit = sample.Pulse ? psu.Pulse : psu.Continuous;
            string kind = sample.Pulse ? "脉冲" : "持续";
            string over = sample.Watts > limit ? $"   ← 超过{kind}承载 {limit} W" : string.Empty;
            lines.Append(sample.Name).Append("   ").Append(sample.Watts).Append(" W");
            if (sample.Pulse) lines.Append("  [脉冲]");
            lines.Append(over).Append('\n');
        }

        return lines.ToString().TrimEnd('\n');
    }

    private string BuildClue(PsuInfo psu)
    {
        LoadResult fault = _state.Baseline;
        int sustainedGap = psu.Continuous - fault.Sustained;
        int peakOver = fault.Peak - psu.Pulse;
        if (fault.Status == "BLOCKED")
        {
            return "诊断线索：装配检查没有空间阻断；" +
                   $"持续 {fault.Sustained} W 距持续承载 {psu.Continuous} W 仅 {sustainedGap} W，" +
                   $"而瞬时峰值 {fault.Peak} W 比短时承载 {psu.Pulse} W 高出 {peakOver} W。" +
                   "故障发生在电源瞬时承载，不在机箱空间。";
        }

        return $"诊断线索：持续负载 {fault.Sustained} W / 峰值 {fault.Peak} W，" +
               $"对照 {psu.Name} 的 {psu.Continuous} / {psu.Pulse} W 承载判断余量。";
    }

    private static string FitStatusText(string status) => status switch
    {
        "OPTIMAL" => "装配通过",
        "DEGRADED" => "装配降级",
        "BLOCKED" => "装配阻断",
        _ => status,
    };

    private static string LoadStatusText(string status) => status switch
    {
        "OPTIMAL" => "负载通过",
        "DEGRADED" => "负载降级通过",
        "BLOCKED" => "电源保护断电",
        _ => status,
    };

    private static Color StateColor(string status) => new(status switch
    {
        "BLOCKED" => "#ff9689",
        "DEGRADED" => "#f5c877",
        _ => "#84dcb1",
    });

    private static string Signed(int? value) =>
        value.HasValue ? value.Value.ToString("+0;-0;0") : "—";

    private void SetStatusTag(string text, Color color)
    {
        _statusTag.Text = text;
        SetLabelColor(_statusTag, color);
        _statusDot.Tint = color;
        _statusDot.QueueRedraw();
    }

    private static void SetLabelColor(Label label, Color color)
    {
        label.AddThemeColorOverride("font_color", color);
    }

    #endregion
}
