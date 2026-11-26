using System.Text;
using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Inspection;
using ComputerSimulator.Modules.Menu;
using ComputerSimulator.Modules.Simulation;
using Godot;

namespace ComputerSimulator.Modules.Diagnosis;

/// <summary>
/// 故障诊断 04-01：一台能通过装配检查、却在显卡瞬时脉冲下保护断电的前置 360 整机。
/// 配置固定，玩家只有一次替换电源的额度；所有结论都来自现有 Inspect / TestLoad 规则。
/// </summary>
public partial class DiagnosisCaseScreen : Control
{
    /// <summary>请求返回模式选择（由组合根接管场景切换）。</summary>
    public event Action? BackRequested;

    private const int CaseIndex = 1;
    private const int FaultyPsuIndex = 2;

    // 宽体机箱 + 前置 360 + 刀法精工 X3 + 双通道 32 GB + 原装烈士墙 Max 700。
    // 装配实测 OPTIMAL，但原始电源的短时承载不足以接住 X3 的瞬时脉冲。
    private static readonly BenchSetup FaultSetup =
        new BenchSetup(CaseIndex, 0, new[] { 1, 3 }, 0, true, 0, 0, 0, FaultyPsuIndex);

    private static readonly Color Muted = new("#a2b6c0");
    private static readonly Color Warn = new("#f5c877");
    private static readonly Color Bad = new("#ff9689");
    private static readonly Color Dim = new("#6e7f87");

    private ClearanceRules? _rules;
    private BenchCatalog _catalog = null!;
    private FitResult _fit = null!;
    private LoadResult _fault = null!;

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

    private int _installedPsuIndex = FaultyPsuIndex;
    private bool _replaced;
    private bool _finished;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = MenuPalette.CreateTheme();
        MouseFilter = MouseFilterEnum.Stop;

        _rules = new ClearanceRules();
        _catalog = _rules.Catalog;
        _fit = _rules.Inspect(FaultSetup);
        _fault = _rules.TestLoad(FaultSetup, FaultyPsuIndex);

        Build();
        ResetState();
        PlayIntro();
    }

    public override void _ExitTree()
    {
        _rules?.Dispose();
        _rules = null;
    }

    #region 布局

    private void Build()
    {
        var backdrop = new TechBackdrop();
        AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 56);
        margin.AddThemeConstantOverride("margin_right", 56);
        margin.AddThemeConstantOverride("margin_top", 44);
        margin.AddThemeConstantOverride("margin_bottom", 26);
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _page = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _page.AddThemeConstantOverride("separation", 0);
        margin.AddChild(_page);

        _page.AddChild(BuildHeader());
        _page.AddChild(new TechLine { CustomMinimumSize = new Vector2(0, 1), Strength = 0.55f });

        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 22);
        _page.AddChild(content);

        content.AddChild(BuildDossier());
        content.AddChild(new ColorRect
        {
            Color = MenuPalette.Alpha(MenuPalette.Steel, 0.45f),
            CustomMinimumSize = new Vector2(1, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        content.AddChild(BuildRepairColumn());

        _page.AddChild(BuildFooter());
    }

    private Control BuildHeader()
    {
        var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 84) };
        header.AddThemeConstantOverride("separation", 18);

        var titles = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        titles.AddThemeConstantOverride("separation", 2);

        var kicker = new HBoxContainer { CustomMinimumSize = new Vector2(0, 13) };
        kicker.AddThemeConstantOverride("separation", 10);
        kicker.AddChild(new ColorRect
        {
            Color = MenuPalette.Signal,
            CustomMinimumSize = new Vector2(2, 11),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        kicker.AddChild(new TrackedText
        {
            Content = "DIAGNOSTICS / CASE 04-01",
            FontSize = 9,
            Tracking = 2.2f,
            Tint = MenuPalette.Alpha(MenuPalette.Ash, 0.78f),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        });
        titles.AddChild(kicker);
        titles.AddChild(Text("满载黑屏", 34, MenuPalette.Platinum));
        titles.AddChild(Text("故障诊断 · 固定液冷整机 · 唯一替换额度", 13,
            MenuPalette.Alpha(MenuPalette.Ash, 0.86f)));
        header.AddChild(titles);

        var actions = new VBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        actions.AddThemeConstantOverride("separation", 5);

        var tagRow = new HBoxContainer();
        tagRow.AddThemeConstantOverride("separation", 8);
        tagRow.AddChild(new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        _statusDot = new StatusDot
        {
            Tint = MenuPalette.Ash,
            Radius = 2f,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        tagRow.AddChild(_statusDot);
        _statusTag = Text("故障待修", 12, Muted);
        tagRow.AddChild(_statusTag);
        actions.AddChild(tagRow);

        var back = TextButton("返回模式选择   ESC", RequestBack);
        back.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        actions.AddChild(back);
        header.AddChild(actions);
        return header;
    }

    private Control BuildDossier()
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(column);

        column.AddChild(Text("客户描述", 15, MenuPalette.Platinum));
        var brief = Text(
            "客户送来一台前置 360 水冷整机：桌面待机正常，一进入高负载游戏就整机黑屏并自动重启。" +
            "整机没有摔碰或进水，客户只希望查明原因并一次修好，不接受用降级显卡换稳定。",
            13, MenuPalette.Alpha(MenuPalette.Platinum, 0.86f));
        brief.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(brief);

        column.AddChild(new TechLine { CustomMinimumSize = new Vector2(0, 1), Strength = 0.45f });
        column.AddChild(Text("固定整机配置", 15, MenuPalette.Platinum));

        var chassis = _catalog.Cases[CaseIndex];
        var card = _catalog.Graphics[FaultSetup.GpuIndex];
        var ram = _catalog.Ram[FaultSetup.RamIndex];
        var psu = _catalog.PowerSupplies[FaultyPsuIndex];

        var config = NewGrid();
        column.AddChild(config);
        KeyValue(config, "机箱", $"{chassis.Name} · 显卡限长 {chassis.GpuLimit} / 电源仓 {chassis.PsuLimit} mm");
        KeyValue(config, "主板", _catalog.Board.Name);
        KeyValue(config, "散热", $"{_catalog.Liquid.Name} · 前置 360（冷排 {_catalog.Liquid.CoreThickness} + 风扇 {_catalog.Liquid.FanThickness} mm）");
        KeyValue(config, "显卡", $"{card.Name} · {card.Dimensions[0]} mm");
        KeyValue(config, "内存", $"{ram.Name} × 2 · A2 / B2 · {ram.Dimensions[2]} mm 高马甲");
        KeyValue(config, "电源", $"{psu.Name} · {psu.Length} mm · 持续 {psu.Continuous} / 短时 {psu.Pulse} W");

        column.AddChild(new TechLine { CustomMinimumSize = new Vector2(0, 1), Strength = 0.45f });
        column.AddChild(Text("装配检查 · 规则实测", 15, MenuPalette.Platinum));

        var fitStatus = new HBoxContainer();
        fitStatus.AddThemeConstantOverride("separation", 8);
        fitStatus.AddChild(new StatusDot
        {
            Tint = StateColor(_fit.Status),
            Radius = 2f,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        });
        fitStatus.AddChild(Text($"{_fit.Status} · {FitStatusText(_fit.Status)}", 13, StateColor(_fit.Status)));
        column.AddChild(fitStatus);

        var fit = NewGrid();
        column.AddChild(fit);
        int psuReserve = (_fit.PsuLimit ?? psu.Length) - psu.Length - (_fit.PsuMargin ?? 0);
        string psuOccupancy = psuReserve > 0
            ? $"{psu.Length} mm + 藏线 {psuReserve} mm ≤ 限长 {_fit.PsuLimit} mm · 余量 {Signed(_fit.PsuMargin)} mm"
            : $"{psu.Length} mm ≤ 限长 {_fit.PsuLimit} mm · 余量 {Signed(_fit.PsuMargin)} mm";
        KeyValue(fit, "显卡限长", $"{card.Dimensions[0]} mm ≤ 有效 {_fit.GpuLimit?.ToString() ?? "—"} mm · 余量 {Signed(_fit.GpuMargin)} mm");
        KeyValue(fit, "电源仓", psuOccupancy);
        KeyValue(fit, "侧板余量", $"限高 {_fit.HeightLimit} mm · 余量 {Signed(_fit.Headroom)} mm");
        KeyValue(fit, "内存", $"{_fit.Capacity} GB · {_fit.Channels} 通道 · {_fit.Bandwidth:0.0} GB/s");
        KeyValue(fit, "物理干涉", _fit.Issues.Length == 0
            ? "无（未触发任何 BLOCKED 项）"
            : string.Join(" / ", _fit.Issues.Select(issue => issue.Message)));

        var section = new SectionDrawing
        {
            CustomMinimumSize = new Vector2(0, 122),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        section.Display(_fit);
        column.AddChild(section);
        column.AddChild(Text($"正交剖面 · 显卡与前置冷排之间仍有 {Signed(_fit.GpuMargin)} mm 余量", 11,
            MenuPalette.Alpha(MenuPalette.Ash, 0.7f)));

        return scroll;
    }

    private Control BuildRepairColumn()
    {
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(420, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(column);

        column.AddChild(BuildFaultPanel());
        column.AddChild(BuildRepairPanel());
        return scroll;
    }

    private Control BuildFaultPanel()
    {
        (PanelContainer panel, VBoxContainer body) = SectionPanel("故障复现", "LOAD TEST / ORIGINAL");
        var psu = _catalog.PowerSupplies[FaultyPsuIndex];

        body.AddChild(Text($"{_fault.Status} · {LoadStatusText(_fault.Status)}", 14, StateColor(_fault.Status)));
        body.AddChild(Text($"持续负载 {_fault.Sustained} W   /   瞬时峰值 {_fault.Peak} W", 19,
            MenuPalette.Platinum));
        body.AddChild(Text(
            $"原装 {psu.Name}：持续承载 {psu.Continuous} W / 短时承载 {psu.Pulse} W / 建议上界 {psu.Recommended} W",
            11, Muted));
        string workload = _catalog.Benchmark.Workloads[FaultSetup.WorkloadIndex].Name;
        string airflow = _catalog.Benchmark.Airflows[FaultSetup.AirflowIndex].Name;
        body.AddChild(Text($"测试工况：{workload} · {airflow}", 11, Muted));

        body.AddChild(new TechLine { CustomMinimumSize = new Vector2(0, 1), Strength = 0.45f });
        body.AddChild(Text("负载曲线实测", 12, MenuPalette.Alpha(MenuPalette.Platinum, 0.82f)));
        var curve = Text(BuildCurve(_fault, psu), 11, Muted);
        curve.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(curve);

        body.AddChild(new TechLine { CustomMinimumSize = new Vector2(0, 1), Strength = 0.45f });
        body.AddChild(Text("规则记录", 12, MenuPalette.Alpha(MenuPalette.Platinum, 0.82f)));
        var message = Text(_fault.Message, 12, Bad);
        message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(message);

        var clue = Text(BuildClue(psu), 12, MenuPalette.Alpha(MenuPalette.Platinum, 0.88f));
        clue.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(clue);
        return panel;
    }

    private Control BuildRepairPanel()
    {
        (PanelContainer panel, VBoxContainer body) = SectionPanel("维修台", "REPAIR BAY");

        var hint = Text("本案只允许更换一次电源。安装备件后运行复测，以规则引擎实际结果结案；复测不得断电。", 11, Muted);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(hint);

        _allowance = Text("替换额度  1 / 1", 11, MenuPalette.Alpha(MenuPalette.Signal, 0.9f));
        body.AddChild(_allowance);

        _psuChoice = new OptionButton
        {
            CustomMinimumSize = new Vector2(0, 34),
            FocusMode = FocusModeEnum.All,
        };
        _psuChoice.AddThemeFontSizeOverride("font_size", 13);
        for (int i = 0; i < _catalog.PowerSupplies.Length; i++)
        {
            PsuInfo candidate = _catalog.PowerSupplies[i];
            string suffix = i == FaultyPsuIndex ? "（同型号备件）" : string.Empty;
            _psuChoice.AddItem($"{candidate.Name}{suffix} · {candidate.Continuous} / {candidate.Pulse} W");
        }
        _psuChoice.ItemSelected += _ => RefreshCandidate();
        body.AddChild(_psuChoice);

        _candidatePreview = Text("", 11, Muted);
        _candidatePreview.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(_candidatePreview);

        _install = ActionButton("更换为所选电源（消耗唯一额度）", InstallSelected);
        body.AddChild(_install);
        _retest = ActionButton("运行负载复测", RunRetest);
        body.AddChild(_retest);

        body.AddChild(new TechLine { CustomMinimumSize = new Vector2(0, 1), Strength = 0.45f });
        _repairState = Text("", 12, Muted);
        _repairState.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(_repairState);

        _retestResult = Text("", 12, Muted);
        _retestResult.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(_retestResult);

        body.AddChild(new TechLine { CustomMinimumSize = new Vector2(0, 1), Strength = 0.45f });
        _reset = ActionButton("重新开始本案", ResetCase);
        body.AddChild(_reset);
        return panel;
    }

    private Control BuildFooter()
    {
        var footer = new HBoxContainer { CustomMinimumSize = new Vector2(0, 22) };
        footer.AddThemeConstantOverride("separation", 12);
        footer.AddChild(Text("验收口径 · 复测不得触发保护断电", 10,
            MenuPalette.Alpha(MenuPalette.Ash, 0.48f)));
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        footer.AddChild(Text("ESC  返回模式选择", 10, MenuPalette.Alpha(MenuPalette.Ash, 0.42f)));
        return footer;
    }

    #endregion

    #region 行为

    private void RefreshCandidate()
    {
        if (_psuChoice == null) return;
        int index = Mathf.Clamp(_psuChoice.Selected, 0, _catalog.PowerSupplies.Length - 1);
        PsuInfo psu = _catalog.PowerSupplies[index];
        string note = index == FaultyPsuIndex
            ? "\n同型号备件只用于排除电源个体故障，不会增加承载余量。"
            : string.Empty;
        _candidatePreview.Text =
            $"持续 {psu.Continuous} W / 短时 {psu.Pulse} W / 建议上界 {psu.Recommended} W\n" +
            $"{psu.Cable} · 长 {psu.Length} mm" + note;
    }

    private void InstallSelected()
    {
        if (_rules == null || _replaced || _finished) return;

        int index = Mathf.Clamp(_psuChoice.Selected, 0, _catalog.PowerSupplies.Length - 1);
        PsuInfo psu = _catalog.PowerSupplies[index];
        _replaced = true;
        _installedPsuIndex = index;

        _psuChoice.Disabled = true;
        _install.Disabled = true;
        _retest.Disabled = false;
        SetLabelColor(_allowance, Dim);
        _allowance.Text = "替换额度  0 / 1";
        string note = index == FaultyPsuIndex ? " 同型号备件不会改变功率余量。" : string.Empty;
        _repairState.Text = $"已安装 {psu.Name}（持续 {psu.Continuous} / 短时 {psu.Pulse} W）。下一步运行负载复测。{note}";
        SetLabelColor(_repairState, MenuPalette.Alpha(MenuPalette.Platinum, 0.9f));
        SetStatusTag("待复测", Warn);
        _retest.GrabFocus();
    }

    private void RunRetest()
    {
        if (_rules == null || !_replaced || _finished) return;

        PsuInfo psu = _catalog.PowerSupplies[_installedPsuIndex];
        LoadResult result = _rules.TestLoad(FaultSetup, _installedPsuIndex);
        _finished = true;

        _psuChoice.Disabled = true;
        _install.Disabled = true;
        _retest.Disabled = true;
        _reset.GrabFocus();

        bool passed = result.Status != "BLOCKED";
        Color color = StateColor(result.Status);
        string verdict;
        if (passed)
        {
            verdict = result.Status == "OPTIMAL"
                ? "验收通过：持续负载与瞬时峰值均在电源余量内。"
                : "验收通过：负载未断电，但峰值超过建议上界，瞬时余量偏低。";
            SetStatusTag(result.Status == "OPTIMAL" ? "修复完成 · OPTIMAL" : "修复完成 · DEGRADED", color);
            _repairState.Text = "复测完成。本案已结案。";
        }
        else
        {
            verdict = "验收失败：复测仍触发保护断电。请重新开始本案。";
            SetStatusTag("维修失败 · BLOCKED", color);
            _repairState.Text = "复测未通过。电源仍在瞬时脉冲下断电。";
        }

        SetLabelColor(_repairState, color);
        _retestResult.Text =
            $"复测结果  {result.Status} · {LoadStatusText(result.Status)}\n" +
            $"持续负载 {result.Sustained} W / 瞬时峰值 {result.Peak} W\n" +
            $"{psu.Name}：持续承载 {psu.Continuous} W / 短时承载 {psu.Pulse} W / 建议上界 {psu.Recommended} W\n" +
            $"{result.Message}\n{verdict}";
        SetLabelColor(_retestResult, color);
    }

    private void ResetCase()
    {
        ResetState();
        _psuChoice.GrabFocus();
    }

    private void ResetState()
    {
        _replaced = false;
        _finished = false;
        _installedPsuIndex = FaultyPsuIndex;

        _psuChoice.Disabled = false;
        // 默认停在当前装着的故障电源上，替换方案必须由玩家自己选出。
        _psuChoice.Select(FaultyPsuIndex);
        _install.Disabled = false;
        _retest.Disabled = true;
        _reset.Text = "重新开始本案";

        SetLabelColor(_allowance, MenuPalette.Alpha(MenuPalette.Signal, 0.9f));
        _allowance.Text = "替换额度  1 / 1";
        _repairState.Text = "未更换任何部件。先核对故障复现数据，再选择替换电源。";
        SetLabelColor(_repairState, Muted);
        _retestResult.Text = "尚未进行复测。更换电源后运行负载复测，以实际结果验收。";
        SetLabelColor(_retestResult, Muted);
        SetStatusTag("故障待修", Muted);
        RefreshCandidate();
    }

    private void RequestBack()
    {
        BackRequested?.Invoke();
    }

    #endregion

    #region 动效

    private void PlayIntro()
    {
        _page.Modulate = MenuPalette.Alpha(Colors.White, 0f);
        Tween tween = CreateTween();
        tween.TweenProperty(_page, "modulate:a", 1f, 0.42).SetEase(Tween.EaseType.Out);

        // 等黑场退去后再抓焦点，避免转场期间按住确认键误触更换或复测。
        tween.TweenInterval(0.18);
        tween.TweenCallback(Callable.From(GrabInitialFocus));
    }

    private void GrabInitialFocus()
    {
        if (!IsInstanceValid(this) || _finished) return;
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
        int sustainedGap = psu.Continuous - _fault.Sustained;
        int peakOver = _fault.Peak - psu.Pulse;
        if (_fault.Status == "BLOCKED")
        {
            return "诊断线索：装配检查没有空间阻断；" +
                   $"持续 {_fault.Sustained} W 距持续承载 {psu.Continuous} W 仅 {sustainedGap} W，" +
                   $"而瞬时峰值 {_fault.Peak} W 比短时承载 {psu.Pulse} W 高出 {peakOver} W。" +
                   "故障发生在电源瞬时承载，不在机箱空间。";
        }

        return $"诊断线索：持续负载 {_fault.Sustained} W / 峰值 {_fault.Peak} W，" +
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

    private static GridContainer NewGrid()
    {
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 18);
        grid.AddThemeConstantOverride("v_separation", 4);
        return grid;
    }

    private static void KeyValue(GridContainer grid, string key, string value)
    {
        var keyLabel = Text(key, 11, MenuPalette.Alpha(MenuPalette.Ash, 0.72f));
        keyLabel.CustomMinimumSize = new Vector2(64, 0);
        grid.AddChild(keyLabel);
        var valueLabel = Text(value, 12, MenuPalette.Alpha(MenuPalette.Platinum, 0.86f));
        valueLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        valueLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        grid.AddChild(valueLabel);
    }

    private static (PanelContainer Panel, VBoxContainer Body) SectionPanel(string title, string tag)
    {
        StyleBoxFlat style = MenuPalette.Box(
            MenuPalette.Alpha(MenuPalette.Graphite, 0.96f),
            MenuPalette.Alpha(MenuPalette.Steel, 0.95f), 1);
        style.ContentMarginLeft = 16;
        style.ContentMarginRight = 16;
        style.ContentMarginTop = 12;
        style.ContentMarginBottom = 14;

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", style);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        panel.AddChild(body);

        var head = new HBoxContainer();
        body.AddChild(head);
        var titleLabel = Text(title, 16, MenuPalette.Platinum);
        titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(titleLabel);
        head.AddChild(new TrackedText
        {
            Content = tag,
            FontSize = 8,
            Tracking = 1.6f,
            Tint = MenuPalette.Alpha(MenuPalette.Ash, 0.45f),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        });
        body.AddChild(new TechLine
        {
            CustomMinimumSize = new Vector2(0, 1),
            Tint = MenuPalette.Steel,
            Strength = 0.55f,
        });
        return (panel, body);
    }

    private static Button ActionButton(string text, Action pressed)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(0, 34),
        };
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
        button.Pressed += pressed;
        return button;
    }

    private static Button TextButton(string text, Action pressed)
    {
        var button = new Button
        {
            Text = text,
            Flat = true,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(0, 24),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        button.AddThemeFontOverride("font", MenuPalette.Text);
        button.AddThemeFontSizeOverride("font_size", 11);
        button.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Ash, 0.65f));
        button.AddThemeColorOverride("font_hover_color", MenuPalette.Platinum);
        button.AddThemeColorOverride("font_pressed_color", MenuPalette.Signal);
        button.AddThemeColorOverride("font_focus_color", MenuPalette.Platinum);
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());

        var focus = new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = MenuPalette.Alpha(MenuPalette.Signal, 0.55f),
            BorderWidthBottom = 1,
            ContentMarginLeft = 0,
            ContentMarginRight = 0,
            ContentMarginTop = 0,
            ContentMarginBottom = 0,
        };
        button.AddThemeStyleboxOverride("focus", focus);

        button.Pressed += pressed;
        return button;
    }

    private static Label Text(string text, int size, Color color)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", MenuPalette.Text);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

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
