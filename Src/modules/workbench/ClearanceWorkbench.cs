using ComputerSimulator.Modules.Benchmark;
using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Inspection;
using ComputerSimulator.Modules.Simulation;
using Godot;

namespace ComputerSimulator.Modules.Workbench;

public partial class ClearanceWorkbench : PanelContainer
{
    /// <summary>请求返回模式选择（由组合根接管场景切换）。</summary>
    public event Action? BackRequested;

    /// <summary>首箱模式：固定前置 360、4K、均衡风道、内存套件 A2/B2，仅开放机箱/显卡/电源选择。</summary>
    public bool CrateMode { get; init; }

    private static readonly int[] CrateSlots = [1, 3];
    private ClearanceRules? _rules;
    private readonly BenchViewport _viewport = new()
    {
        SizeFlagsVertical = SizeFlags.ExpandFill,
        CustomMinimumSize = new Vector2(0, 180),
    };

    private readonly SectionDrawing _section = new()
    {
        SizeFlagsVertical = SizeFlags.ExpandFill,
        CustomMinimumSize = new Vector2(0, 100),
    };

    private readonly OptionButton _case = new(), _memory = new();
    private readonly OptionButton _scenario = new(), _gpu = new();
    private readonly VBoxContainer _air = new(), _water = new();
    private readonly Label _goal = new(), _hardwareName = new();
    private readonly Label _crateInventory = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _crateResult = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly List<CheckBox> _slots = [];
    private readonly HBoxContainer _slotsRow = new();
    private readonly HSlider _offset = new() { Step = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Label _offsetText = new(), _status = new(), _metrics = new(), _messages = new();
    private VBoxContainer _scenarioField = null!, _memoryField = null!, _slotsField = null!, _conditionsField = null!;
    private readonly VBoxContainer _power = new();
    private readonly HBoxContainer _conditions = new();
    private readonly OptionButton _psu = new(), _workload = new(), _airflow = new();
    private readonly TabContainer _views = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly BenchmarkReportView _report = new() { Name = "负载报告" };
    private readonly Button _runLoad = new() { Text = "运行整机负载测试" };
    private readonly Label _powerResult = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button _submit = new() { Text = "提交首箱评分", TooltipText = "通过一次当前配置的测试后可提交" };
    private readonly Button _replay = new() { Text = "再次挑战", TooltipText = "保留本次访问最佳并回到初始选择" };
    private readonly Button _reset = new() { Text = "还原初始配置" };
    private PanelContainer _statusChip = null!;
    private PanelContainer _loadPanel = null!;
    private HBoxContainer _sectionCell = null!;
    private GridContainer _stripGrid = null!;
    private BenchmarkReport? _lastReport;
    private float? _bestScore;
    private string _bestGrade = "";
    private bool _submitted;
    private bool _hasTestResult;
    private bool _resetting;

    public override void _Ready()
    {
        Theme = WorkbenchStyle.CreateTheme();
        AddThemeStyleboxOverride("panel", WorkbenchStyle.Box(
            WorkbenchStyle.Canvas, WorkbenchStyle.Canvas, 14, 12, 0, 0));
        _rules = new ClearanceRules();
        var catalog = _rules.Catalog;
        if (CrateMode)
        {
            _crateInventory.Text =
                $"首箱清单（机箱/显卡/电源各选 1 件）：机箱 {string.Join(" / ", catalog.Cases.Select(item => item.Name))}；" +
                $"显卡 {string.Join(" / ", catalog.Graphics.Select(item => item.Name))}；" +
                $"电源 {string.Join(" / ", catalog.PowerSupplies.Select(item => item.Name))}。\n" +
                $"固定件：{catalog.Liquid.Name} 前置 360、{catalog.Ram[0].Name} ×2（A2 + B2）、" +
                $"{catalog.Benchmark.Workloads[1].Name}、{catalog.Benchmark.Airflows[0].Name}。";
        }

        var page = new VBoxContainer { Name = "工作台页面" };
        page.AddThemeConstantOverride("separation", 10);
        AddChild(page);
        page.AddChild(BuildMasthead());
        page.AddChild(WorkbenchStyle.Rule(WorkbenchStyle.Border));

        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 14);
        page.AddChild(columns);
        BuildStage();
        columns.AddChild(_views);
        columns.AddChild(BuildInspector(catalog));

        _views.Resized += UpdateSectionVisibility;
        Reset();
        UpdateSectionVisibility();
    }

    #region 布局

    private Control BuildMasthead()
    {
        var masthead = new HBoxContainer { CustomMinimumSize = new Vector2(0, 44) };
        masthead.AddThemeConstantOverride("separation", 12);

        var titles = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        titles.AddThemeConstantOverride("separation", 1);
        var wordmark = new HBoxContainer();
        wordmark.AddThemeConstantOverride("separation", 6);
        wordmark.AddChild(WorkbenchStyle.MakeLabel("PC", 10, WorkbenchStyle.Mint, WorkbenchStyle.MonoFont));
        wordmark.AddChild(WorkbenchStyle.MakeLabel("ARCHITECT", 10, WorkbenchStyle.Muted, WorkbenchStyle.DisplayFont));
        titles.AddChild(wordmark);
        titles.AddChild(WorkbenchStyle.MakeLabel(CrateMode ? "神秘箱 03 · 首箱" : "沙盒工作台",
            24, WorkbenchStyle.Text, WorkbenchStyle.TextFont));
        masthead.AddChild(titles);
        masthead.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

        _status.AddThemeFontOverride("font", WorkbenchStyle.MonoFont);
        _status.AddThemeFontSizeOverride("font_size", 13);
        _status.AddThemeColorOverride("font_color", WorkbenchStyle.Text);
        _status.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _statusChip = WorkbenchStyle.SurfacePanel(WorkbenchStyle.Inset, WorkbenchStyle.Border, 10, 4, 2);
        _statusChip.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _statusChip.AddChild(_status);
        masthead.AddChild(_statusChip);

        var menuButton = new Button
        {
            Text = "模式选择",
            TooltipText = "返回模式选择（ESC）",
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(86, 28),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        WorkbenchStyle.ApplyGhost(menuButton, 28);
        menuButton.Pressed += () => BackRequested?.Invoke();
        masthead.AddChild(menuButton);
        return masthead;
    }

    private void BuildStage()
    {
        var visualPage = new MarginContainer { Name = "装配视图" };
        foreach (string side in new[] { "left", "right", "top", "bottom" })
            visualPage.AddThemeConstantOverride("margin_" + side, 10);
        _views.AddChild(visualPage);

        var visual = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        visual.AddThemeConstantOverride("separation", 8);
        visualPage.AddChild(visual);
        visual.AddChild(BuildViewToolbar());

        var stage = WorkbenchStyle.SurfacePanel(WorkbenchStyle.Inset, WorkbenchStyle.BorderSoft, 1, 1, 2);
        stage.SizeFlagsVertical = SizeFlags.ExpandFill;
        stage.AddChild(_viewport);
        visual.AddChild(stage);

        visual.AddChild(BuildInstrumentStrip());

        _report.ComparisonEnabled = !CrateMode;
        _report.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _report.SizeFlagsVertical = SizeFlags.ExpandFill;
        _views.AddChild(_report);
    }

    private Control BuildViewToolbar()
    {
        var tools = new HBoxContainer { CustomMinimumSize = new Vector2(0, 28) };
        tools.AddThemeConstantOverride("separation", 8);
        tools.AddChild(WorkbenchStyle.MakeLabel("视图", 11, WorkbenchStyle.Muted));

        var rail = WorkbenchStyle.SurfacePanel(WorkbenchStyle.Inset, WorkbenchStyle.BorderSoft, 3, 3, 2);
        rail.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 2);
        rail.AddChild(row);
        var group = new ButtonGroup();
        string[] views = ["轴测", "正视", "侧视", "俯视", "特写"];
        var buttons = new Button[views.Length];
        for (int i = 0; i < views.Length; i++)
        {
            int view = i;
            var button = new Button
            {
                Text = views[i],
                ToggleMode = true,
                ButtonGroup = group,
                FocusMode = FocusModeEnum.All,
                CustomMinimumSize = new Vector2(42, 26),
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            WorkbenchStyle.ApplySegment(button);
            button.Pressed += () => _viewport.SetView(view);
            row.AddChild(button);
            buttons[i] = button;
            if (i == 0) button.SetPressedNoSignal(true);
        }
        _viewport.ViewPresetChanged += active =>
        {
            for (int i = 0; i < buttons.Length; i++) buttons[i].SetPressedNoSignal(active == i);
        };
        tools.AddChild(rail);
        tools.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

        var sidePanel = new CheckBox
        {
            Text = "透明侧板",
            FocusMode = FocusModeEnum.All,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        sidePanel.Toggled += _viewport.ShowPanel;
        tools.AddChild(sidePanel);
        var rgb = new CheckBox
        {
            Text = "ARGB",
            ButtonPressed = true,
            TooltipText = "灯带与风扇导光环同步预览",
            FocusMode = FocusModeEnum.All,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        rgb.Toggled += _viewport.SetRgb;
        tools.AddChild(rgb);
        return tools;
    }

    private Control BuildInstrumentStrip()
    {
        var strip = WorkbenchStyle.SurfacePanel(WorkbenchStyle.Surface, WorkbenchStyle.BorderSoft, 10, 10, 2);
        strip.CustomMinimumSize = new Vector2(0, 160);
        var row = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("h_separation", 12);
        row.AddThemeConstantOverride("v_separation", 10);
        _stripGrid = row;
        strip.AddChild(row);

        _sectionCell = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(286, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _sectionCell.AddThemeConstantOverride("separation", 12);
        var sectionColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        sectionColumn.AddThemeConstantOverride("separation", 5);
        var sectionHead = new HBoxContainer();
        sectionHead.AddThemeConstantOverride("separation", 8);
        var sectionTitle = WorkbenchStyle.MakeLabel("正交剖面", 13, WorkbenchStyle.Muted);
        sectionTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        sectionHead.AddChild(sectionTitle);
        sectionHead.AddChild(WorkbenchStyle.MakeLabel("红色为干涉区域", 11, WorkbenchStyle.Coral));
        sectionColumn.AddChild(sectionHead);
        var sectionFrame = WorkbenchStyle.SurfacePanel(WorkbenchStyle.Inset, WorkbenchStyle.BorderSoft, 0, 0, 2);
        sectionFrame.SizeFlagsVertical = SizeFlags.ExpandFill;
        sectionFrame.AddChild(_section);
        sectionColumn.AddChild(sectionFrame);
        _sectionCell.AddChild(sectionColumn);
        _sectionCell.AddChild(new ColorRect
        {
            Color = WorkbenchStyle.Alpha(WorkbenchStyle.BorderSoft, .9f),
            CustomMinimumSize = new Vector2(1, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        row.AddChild(_sectionCell);

        var diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        diagnostics.AddThemeConstantOverride("separation", 5);
        diagnostics.AddChild(WorkbenchStyle.MakeLabel("装配诊断", 13, WorkbenchStyle.Muted));
        _metrics.AddThemeFontOverride("font", WorkbenchStyle.MonoFont);
        _metrics.AddThemeFontSizeOverride("font_size", 13);
        _metrics.AddThemeColorOverride("font_color", WorkbenchStyle.TextSoft);
        _metrics.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _metrics.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        diagnostics.AddChild(_metrics);
        _messages.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _messages.AddThemeFontSizeOverride("font_size", 14);
        _messages.AddThemeColorOverride("font_color", WorkbenchStyle.Text);
        _messages.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _messages.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _messages.SizeFlagsVertical = SizeFlags.ExpandFill;
        _messages.VerticalAlignment = VerticalAlignment.Top;
        _messages.MouseFilter = MouseFilterEnum.Pass;
        diagnostics.AddChild(_messages);
        row.AddChild(diagnostics);
        return strip;
    }

    private Control BuildInspector(BenchCatalog catalog)
    {
        var inspector = WorkbenchStyle.SurfacePanel(WorkbenchStyle.Surface, WorkbenchStyle.Border, 0, 0, 3);
        inspector.CustomMinimumSize = new Vector2(334, 0);
        inspector.SizeFlagsVertical = SizeFlags.ExpandFill;
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true,
        };
        inspector.AddChild(scroll);
        var pad = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        pad.AddThemeConstantOverride("margin_left", 14);
        pad.AddThemeConstantOverride("margin_right", 14);
        pad.AddThemeConstantOverride("margin_top", 12);
        pad.AddThemeConstantOverride("margin_bottom", 12);
        scroll.AddChild(pad);
        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 8);
        pad.AddChild(form);

        _goal.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _goal.AddThemeFontSizeOverride("font_size", 13);
        _goal.AddThemeColorOverride("font_color", WorkbenchStyle.TextSoft);
        _goal.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _goal.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        form.AddChild(_goal);

        _hardwareName.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _hardwareName.AddThemeFontSizeOverride("font_size", 11);
        _hardwareName.AddThemeColorOverride("font_color", WorkbenchStyle.Muted);
        _hardwareName.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hardwareName.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        form.AddChild(_hardwareName);

        _crateInventory.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _crateInventory.AddThemeFontSizeOverride("font_size", 11);
        _crateInventory.AddThemeColorOverride("font_color", WorkbenchStyle.Muted);
        _crateInventory.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _crateInventory.Visible = CrateMode;
        var inventory = WorkbenchStyle.SurfacePanel(WorkbenchStyle.Inset, WorkbenchStyle.BorderSoft, 10, 8, 2);
        inventory.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        inventory.Visible = CrateMode;
        inventory.AddChild(_crateInventory);
        form.AddChild(inventory);

        form.AddChild(WorkbenchStyle.Rule(WorkbenchStyle.BorderSoft));

        _scenario.AddItem("双塔风冷与内存");
        _scenario.AddItem("前置 360 冷排与显卡");
        StyleSelect(_scenario);
        _scenario.Visible = !CrateMode;
        _scenario.ItemSelected += _ => Reset();
        _scenarioField = Field(form, "装配方案", _scenario);
        _scenarioField.Visible = !CrateMode;

        foreach (var item in catalog.Cases)
            _case.AddItem($"{item.Name} · 高 {item.HeightLimit} / 长 {item.GpuLimit} mm");
        StyleSelect(_case);
        _case.ItemSelected += _ => Refresh();
        Field(form, "机箱", _case);

        _water.AddThemeConstantOverride("separation", 5);
        foreach (var card in catalog.Graphics)
            _gpu.AddItem($"{card.Name} · {card.Dimensions[0]} mm");
        StyleSelect(_gpu);
        _gpu.ItemSelected += _ => Refresh();
        Field(_water, "显卡型号", _gpu);
        form.AddChild(_water);

        foreach (var item in catalog.Ram)
            _memory.AddItem($"{item.Name} · {item.Dimensions[2]} mm");
        StyleSelect(_memory);
        _memory.Visible = !CrateMode;
        _memory.ItemSelected += _ => Refresh();
        _memoryField = Field(form, "内存型号", _memory);
        _memoryField.Visible = !CrateMode;

        _slotsRow.AddThemeConstantOverride("separation", 4);
        _slotsRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        foreach (var slot in catalog.Board.Slots)
        {
            var check = new CheckBox
            {
                Text = slot.Name,
                FocusMode = FocusModeEnum.All,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _slotsRow.AddChild(check);
            _slots.Add(check);
            check.Toggled += _ => Refresh();
        }
        _slotsRow.Visible = !CrateMode;
        _slotsField = Field(form, "已安装的内存插槽", _slotsRow);
        _slotsField.Visible = !CrateMode;

        _air.AddThemeConstantOverride("separation", 5);
        var offsetHead = new HBoxContainer();
        var offsetLabel = WorkbenchStyle.Field("前扇上移");
        offsetLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        offsetHead.AddChild(offsetLabel);
        _offsetText.AddThemeFontOverride("font", WorkbenchStyle.MonoFont);
        _offsetText.AddThemeFontSizeOverride("font_size", 11);
        _offsetText.AddThemeColorOverride("font_color", WorkbenchStyle.Mint);
        _offsetText.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        offsetHead.AddChild(_offsetText);
        _air.AddChild(offsetHead);
        _offset.MaxValue = catalog.Cooler.Adjustable_y_max;
        _offset.CustomMinimumSize = new Vector2(0, 22);
        _air.AddChild(_offset);
        _offset.ValueChanged += _ => Refresh();
        form.AddChild(_air);

        _loadPanel = WorkbenchStyle.SurfacePanel(WorkbenchStyle.SurfaceRaised, WorkbenchStyle.BorderSoft, 10, 10, 3);
        _loadPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _loadPanel.AddChild(_power);
        form.AddChild(_loadPanel);

        _power.AddThemeConstantOverride("separation", 6);
        _power.AddChild(WorkbenchStyle.Heading("整机负载测试"));
        foreach (var profile in catalog.Benchmark.Workloads) _workload.AddItem(profile.Name);
        foreach (var preset in catalog.Benchmark.Airflows) _airflow.AddItem(preset.Name);
        StyleSelect(_workload);
        StyleSelect(_airflow);
        _workload.ItemSelected += _ => Refresh();
        _airflow.ItemSelected += _ => Refresh();
        _conditions.AddThemeConstantOverride("separation", 6);
        _conditions.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _conditions.AddChild(_workload);
        _conditions.AddChild(_airflow);
        _conditions.Visible = !CrateMode;
        _conditionsField = Field(_power, "目标用途与通风工况", _conditions);
        _conditionsField.Visible = !CrateMode;

        foreach (var psu in catalog.PowerSupplies)
            _psu.AddItem($"{psu.Name} · 持续 {psu.Continuous} / 短时 {psu.Pulse} W");
        StyleSelect(_psu);
        _psu.ItemSelected += _ => Refresh();
        Field(_power, "测试电源", _psu);

        WorkbenchStyle.ApplyPrimary(_runLoad, 34);
        _runLoad.Pressed += RunLoad;
        _power.AddChild(_runLoad);

        _powerResult.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _powerResult.AddThemeFontSizeOverride("font_size", 12);
        _powerResult.AddThemeColorOverride("font_color", WorkbenchStyle.Text);
        _powerResult.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _power.AddChild(_powerResult);

        _crateResult.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _crateResult.AddThemeFontSizeOverride("font_size", 11);
        _crateResult.AddThemeColorOverride("font_color", WorkbenchStyle.Text);
        _crateResult.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _crateResult.Visible = CrateMode;

        WorkbenchStyle.ApplyPrimary(_submit, 34);
        _submit.Visible = CrateMode;
        _submit.Disabled = true;
        _submit.Pressed += SubmitCrate;
        _power.AddChild(_submit);

        _replay.CustomMinimumSize = new Vector2(0, 30);
        _replay.Visible = false;
        _replay.Pressed += ReplayCrate;
        _power.AddChild(_replay);
        _power.AddChild(_crateResult);

        _reset.CustomMinimumSize = new Vector2(0, 30);
        WorkbenchStyle.ApplyGhost(_reset, 30);
        _reset.Pressed += Reset;
        form.AddChild(_reset);
        return inspector;
    }

    private static VBoxContainer Field(Container parent, string caption, Control control)
    {
        var group = new VBoxContainer();
        group.AddThemeConstantOverride("separation", 3);
        group.AddChild(WorkbenchStyle.Field(caption));
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        group.AddChild(control);
        parent.AddChild(group);
        return group;
    }

    private static void StyleSelect(OptionButton option)
    {
        option.FitToLongestItem = false;
        option.ClipText = true;
        option.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        option.FocusMode = FocusModeEnum.All;
    }

    private void UpdateSectionVisibility()
    {
        if (_sectionCell == null) return;
        // 首次同步调用时 _views 尚未完成布局，Width 为 0；等 Resized 拿到真实宽度再切换列数，
        // 避免进入工作台时先闪一帧单列布局。
        if (_stripGrid != null && _views.Size.X > 0) _stripGrid.Columns = _views.Size.X < 560 ? 1 : 2;
    }

    #endregion

    #region 状态与流程

    private void Reset()
    {
        if (_submitted) return;
        var setup = _rules!.Catalog.Initial;
        _resetting = true;
        bool liquid = CrateMode || _scenario.Selected == 1;
        _air.Visible = !liquid;
        _water.Visible = liquid;
        _power.Visible = liquid;
        _loadPanel.Visible = liquid;
        _psu.Select(0);
        _hasTestResult = false;
        _lastReport = null;
        _report.InvalidateCurrent();
        _airflow.Select(0);
        _workload.Select(CrateMode ? 1 : 0);
        _views.CurrentTab = 0;
        _views.TabsVisible = liquid;
        _views.SetTabDisabled(1, true);
        if (CrateMode)
        {
            _goal.Text = "目标：从首箱配件中选出可点亮的组合，通过 4K 整机负载测试，提交综合评分。";
            _hardwareName.Text = $"{_rules.Catalog.Board.Name}  /  {_rules.Catalog.Liquid.Name}  /  {_rules.Catalog.Ram[0].Name}";
        }
        else
        {
            _goal.Text = liquid ? "目标：保留前置 360 冷排，让显卡装入机箱。" : "目标：保留 32 GB 双通道，让侧板顺利合上。";
            _hardwareName.Text = _rules.Catalog.Board.Name + "  /  " + (liquid ? _rules.Catalog.Liquid.Name : _rules.Catalog.Cooler.Name);
        }
        _case.Select(CrateMode ? 0 : setup.CaseIndex);
        _gpu.Select(0);
        _memory.Select(CrateMode ? 0 : setup.RamIndex);
        int[] slots = CrateMode ? CrateSlots : setup.Slots;
        for (int i = 0; i < _slots.Count; i++) _slots[i].SetPressedNoSignal(slots.Contains(i));
        _offset.Value = CrateMode ? 0 : setup.Offset;
        if (CrateMode)
        {
            _case.Disabled = false;
            _gpu.Disabled = false;
            _psu.Disabled = false;
            _reset.Disabled = false;
            _submit.Visible = true;
            _replay.Visible = false;
        }
        _resetting = false;
        Refresh();
    }

    private BenchSetup Selection()
    {
        if (CrateMode)
            return new BenchSetup(_case.Selected, 0, CrateSlots, 0, true, _gpu.Selected, 0, 1, _psu.Selected);
        var slots = Enumerable.Range(0, _slots.Count).Where(i => _slots[i].ButtonPressed).ToArray();
        return new BenchSetup(_case.Selected, _memory.Selected, slots, (int)_offset.Value, _scenario.Selected == 1, _gpu.Selected, _airflow.Selected, _workload.Selected, _psu.Selected);
    }

    private void RunLoad()
    {
        if (_rules == null || _runLoad.Disabled) return;
        var setup = Selection();
        var result = _rules.TestLoad(setup, _psu.Selected);
        _hasTestResult = result.Report != null;
        _lastReport = null;
        if (result.Report is { } report)
        {
            _report.Display(report, CrateMode ? null : CaptureRun(setup));
            _views.SetTabDisabled(1, false);
            _views.CurrentTab = 1;
            if (CrateMode)
            {
                _lastReport = report;
                _submit.Disabled = false;
                SetTextColor(_crateResult, WorkbenchStyle.Muted);
                _crateResult.Text = $"当前配置测试总分  {report.Total.Score:F1} · {report.Total.Grade}，可提交。";
            }
        }
        else
        {
            _report.InvalidateCurrent("本次测试未通过，复测通过后可继续。");
            _views.CurrentTab = 0;
            _views.SetTabDisabled(1, true);
            if (CrateMode)
            {
                _submit.Disabled = true;
                SetTextColor(_crateResult, StateColor("BLOCKED"));
                _crateResult.Text = "测试未通过，提交不可用。";
            }
        }
        _powerResult.Text = $"持续  {result.Sustained} W  /  峰值  {result.Peak} W\n{result.Message}";
        var state = StateColor(result.Status);
        SetTextColor(_powerResult, state);
        SetStatus(result.Status switch
        {
            "BLOCKED" => "BLOCKED    测试断电",
            "DEGRADED" => "DEGRADED    负载降级",
            _ => "OPTIMAL    负载通过"
        }, state);
    }

    private BenchmarkRunIdentity CaptureRun(BenchSetup setup)
    {
        var catalog = _rules!.Catalog;
        var slots = setup.Slots
            .Where(index => index >= 0 && index < catalog.Board.Slots.Length)
            .OrderBy(index => index)
            .Select(index => catalog.Board.Slots[index].Name);
        return new BenchmarkRunIdentity(
            catalog.Cases[setup.CaseIndex].Name,
            catalog.Graphics[setup.GpuIndex].Name,
            catalog.PowerSupplies[setup.PsuIndex].Name,
            catalog.Ram[setup.RamIndex].Name,
            string.Join(" + ", slots));
    }

    private void SubmitCrate()
    {
        if (!CrateMode || _submitted || _lastReport is not { } report) return;

        _submitted = true;
        if (_bestScore is not { } best || report.Total.Score > best)
        {
            _bestScore = report.Total.Score;
            _bestGrade = report.Total.Grade;
        }
        SetTextColor(_crateResult, StateColor("OPTIMAL"));
        _crateResult.Text = $"本次得分  {report.Total.Score:F1} · {report.Total.Grade}    " +
            $"本次访问最佳  {_bestScore!.Value:F1} · {_bestGrade}\n配置已锁定，点击“再次挑战”开始新一轮。";
        _submit.Disabled = true;
        _submit.Visible = false;
        _replay.Visible = true;
        _case.Disabled = true;
        _gpu.Disabled = true;
        _psu.Disabled = true;
        _runLoad.Disabled = true;
        _reset.Disabled = true;
    }

    private void ReplayCrate()
    {
        if (!CrateMode || !_submitted) return;

        _submitted = false;
        _replay.Visible = false;
        _submit.Visible = true;
        _case.Disabled = false;
        _gpu.Disabled = false;
        _psu.Disabled = false;
        _reset.Disabled = false;
        Reset();
    }

    private static void SetTextColor(Label label, Color color)
    {
        label.Modulate = Colors.White;
        label.AddThemeColorOverride("font_color", color);
    }

    private static Color StateColor(string status) => status switch
    {
        "BLOCKED" => WorkbenchStyle.Coral,
        "DEGRADED" => WorkbenchStyle.Amber,
        _ => WorkbenchStyle.Mint,
    };

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        SetTextColor(_status, color);
        WorkbenchStyle.ApplyStatusChip(_statusChip, color);
    }

    private void Refresh()
    {
        if (_resetting || _rules == null || _submitted) return;
        if (CrateMode)
        {
            _lastReport = null;
            _submit.Disabled = true;
            SetTextColor(_crateResult, WorkbenchStyle.Muted);
            _crateResult.Text = _bestScore is { } best
                ? $"本次访问最佳  {best:F1} · {_bestGrade}\n当前配置尚未通过测试。"
                : "完成一次当前配置的 4K 负载测试后可提交评分。";
        }
        var result = _rules.Inspect(Selection());
        if (_hasTestResult) _report.MarkStale();
        _runLoad.Disabled = result.Status == "BLOCKED";
        _powerResult.Text = _runLoad.Disabled ? "请先解除装配阻断。" : _hasTestResult ? "配置已变更，待复测。" : "等待测试。";
        SetTextColor(_powerResult, WorkbenchStyle.Muted);
        _viewport.Display(result);
        _section.Display(result);
        _offsetText.Text = $"{_offset.Value:0} mm";
        _metrics.Text = $"侧板余量  {result.Headroom:+0;-0;0} mm\n{result.Capacity} GB    /    {result.Channels} 通道    /    {result.Bandwidth:0.0} GB/s";
        if (result.GpuLimit.HasValue)
        {
            var kit = _rules.Catalog.Liquid;
            _metrics.Text = $"冷排 {kit.CoreThickness} + 风扇 {kit.FanThickness} mm\n显卡有效限长  {result.GpuLimit} mm    /    余量  {result.GpuMargin:+0;-0;0} mm";
            if (result.PsuMargin.HasValue)
                _metrics.Text += $"\n电源仓余量  {result.PsuMargin:+0;-0;0} mm";
        }
        var state = StateColor(result.Status);
        SetStatus(result.Status switch
        {
            "BLOCKED" => "BLOCKED    装配阻断",
            "DEGRADED" => "DEGRADED    带宽降级",
            _ => "OPTIMAL    装配通过"
        }, state);
        SetTextColor(_messages, state);
        _messages.Text = result.Issues.Length > 0 ? string.Join("\n", result.Issues.Select(issue => issue.Message))
            : result.GpuLimit.HasValue ? "装配目标达成。显卡和前置冷排均可安装。"
            : result.Capacity == 32 ? "装配目标达成。双通道已启用，侧板可闭合。" : "装配通过。请保留两条内存以满足 32 GB 目标。";
        _messages.TooltipText = _messages.Text;
        SyncOptionTooltip(_scenario);
        SyncOptionTooltip(_case);
        SyncOptionTooltip(_gpu);
        SyncOptionTooltip(_memory);
        SyncOptionTooltip(_workload);
        SyncOptionTooltip(_airflow);
        SyncOptionTooltip(_psu);
    }

    private static void SyncOptionTooltip(OptionButton option)
    {
        int index = option.Selected;
        option.TooltipText = index >= 0 && index < option.ItemCount ? option.GetItemText(index) : "";
    }

    #endregion

    public override void _ExitTree()
    {
        _rules?.Dispose();
        _rules = null;
    }
}
