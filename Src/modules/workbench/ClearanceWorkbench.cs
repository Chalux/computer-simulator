using ComputerSimulator.Modules.Benchmark;
using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Inspection;
using ComputerSimulator.Modules.Simulation;
using Godot;

namespace ComputerSimulator.Modules.Workbench;

public partial class ClearanceWorkbench : PanelContainer
{
    /// <summary>请求返回主菜单（由组合根接管场景切换）。</summary>
    public event Action? MenuRequested;

    private ClearanceRules? _rules;
    private readonly BenchViewport _viewport = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly SectionDrawing _section = new() { CustomMinimumSize = new Vector2(0, 155) };
    private readonly OptionButton _case = new(), _memory = new();
    private readonly OptionButton _scenario = new(), _gpu = new();
    private readonly VBoxContainer _air = new(), _water = new();
    private readonly Label _goal = new(), _hardwareName = new();
    private readonly List<CheckBox> _slots = [];
    private readonly HSlider _offset = new() { Step = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Label _offsetText = new(), _status = new(), _metrics = new(), _messages = new();
    private readonly VBoxContainer _power = new();
    private readonly OptionButton _psu = new(), _workload = new(), _airflow = new();
    private readonly TabContainer _views = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly BenchmarkReportView _report = new() { Name = "负载报告" };
    private readonly Button _runLoad = new() { Text = "运行整机负载测试" };
    private readonly Label _powerResult = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private bool _hasTestResult;
    private bool _resetting;
    private static readonly Color Muted = new("#a2b6c0");

    public override void _Ready()
    {
        Theme = new Theme
        {
            DefaultFont = new SystemFont { FontNames = ["Microsoft YaHei UI", "Noto Sans CJK SC", "Arial"] },
            DefaultFontSize = 15
        };
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("#10191f"),
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 12,
            ContentMarginBottom = 12
        });
        _rules = new ClearanceRules();
        var catalog = _rules.Catalog;
        var page = new VBoxContainer();
        AddChild(page);
        var header = new HBoxContainer();
        page.AddChild(header);
        var title = Caption(header, "PC Architect   /   装配工作台", 22);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(_status);
        header.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0) });
        var menuButton = new Button
        {
            Text = "主菜单",
            TooltipText = "返回主菜单（ESC）",
            CustomMinimumSize = new Vector2(78, 28),
            FocusMode = FocusModeEnum.None,
        };
        menuButton.AddThemeFontSizeOverride("font_size", 13);
        menuButton.AddThemeColorOverride("font_color", Muted);
        menuButton.AddThemeColorOverride("font_hover_color", new Color("#eef2f3"));
        // 中性描边，与主菜单的 Steel / Platinum 令牌一致，不引入第二种强调色。
        menuButton.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0),
            BorderColor = new Color("#1a2429"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
        });
        menuButton.AddThemeStyleboxOverride("hover", new StyleBoxFlat
        {
            BgColor = new Color(1f, 1f, 1f, 0.04f),
            BorderColor = new Color("#8b99a1"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
        });
        menuButton.AddThemeStyleboxOverride("pressed", new StyleBoxFlat
        {
            BgColor = new Color(1f, 1f, 1f, 0.07f),
            BorderColor = new Color("#eef2f3"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
        });
        menuButton.Pressed += () => MenuRequested?.Invoke();
        header.AddChild(menuButton);
        page.AddChild(new HSeparator());

        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 16);
        page.AddChild(columns);
        columns.AddChild(_views);
        var visual = new VBoxContainer { Name = "装配视图" };
        _views.AddChild(visual);
        _views.AddChild(_report);
        var tools = new HBoxContainer();
        visual.AddChild(tools);
        string[] views = ["轴测", "正视", "侧视", "俯视", "特写"];
        for (int i = 0; i < views.Length; i++)
        {
            int view = i;
            var button = new Button { Text = views[i] };
            button.Pressed += () => _viewport.SetView(view);
            tools.AddChild(button);
        }
        var panel = new CheckBox { Text = "透明侧板" };
        tools.AddChild(panel);
        panel.Toggled += _viewport.ShowPanel;
        var rgb = new CheckBox { Text = "ARGB", ButtonPressed = true, TooltipText = "灯带与风扇导光环同步预览" };
        tools.AddChild(rgb);
        rgb.Toggled += _viewport.SetRgb;
        visual.AddChild(_viewport);
        Caption(visual, "正交剖面   /   红色区域为实体干涉", 14, Muted);
        visual.AddChild(_section);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(342, 0),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        columns.AddChild(scroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(form);
        _scenario.AddItem("双塔风冷与内存");
        _scenario.AddItem("前置 360 冷排与显卡");
        form.AddChild(_scenario);
        form.AddChild(_goal);
        _goal.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hardwareName.Modulate = Muted;
        _hardwareName.AddThemeFontSizeOverride("font_size", 13);
        form.AddChild(_hardwareName);
        form.AddChild(new HSeparator());
        Caption(form, "机箱", 14);
        foreach (var item in catalog.Cases) _case.AddItem($"{item.Name}   ·   高 {item.HeightLimit} / 长 {item.GpuLimit} mm");
        form.AddChild(_case);
        form.AddChild(_water);
        Caption(_water, "显卡型号", 14);
        foreach (var card in catalog.Graphics) _gpu.AddItem($"{card.Name}   ·   {card.Dimensions[0]} mm");
        _water.AddChild(_gpu);
        _gpu.ItemSelected += _ => Refresh();
        Caption(form, "内存型号", 14);
        foreach (var item in catalog.Ram) _memory.AddItem($"{item.Name}   ·   {item.Dimensions[2]} mm");
        form.AddChild(_memory);
        Caption(form, "已安装的内存插槽", 14);
        var row = new HBoxContainer();
        form.AddChild(row);
        foreach (var slot in catalog.Board.Slots)
        {
            var check = new CheckBox { Text = slot.Name };
            row.AddChild(check);
            _slots.Add(check);
            check.Toggled += _ => Refresh();
        }
        form.AddChild(_air);
        var sliderLabel = new HBoxContainer();
        _air.AddChild(sliderLabel);
        Caption(sliderLabel, "前扇上移", 14).SizeFlagsHorizontal = SizeFlags.ExpandFill;
        sliderLabel.AddChild(_offsetText);
        _offset.MaxValue = catalog.Cooler.Adjustable_y_max;
        _offset.CustomMinimumSize = new Vector2(0, 22);
        _air.AddChild(_offset);
        _offset.ValueChanged += _ => Refresh();
        _case.ItemSelected += _ => Refresh();
        _memory.ItemSelected += _ => Refresh();
        form.AddChild(new HSeparator());
        Caption(form, "装配诊断", 18);
        form.AddChild(_metrics);
        _messages.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        form.AddChild(_messages);
        form.AddChild(_power);
        _power.AddChild(new HSeparator());
        Caption(_power, "整机负载测试", 18);
        Caption(_power, "目标用途与通风工况", 14, Muted);
        foreach (var profile in catalog.Benchmark.Workloads) _workload.AddItem(profile.Name);
        foreach (var preset in catalog.Benchmark.Airflows) _airflow.AddItem(preset.Name);
        var conditions = new HBoxContainer();
        _power.AddChild(conditions);
        conditions.AddChild(_workload);
        conditions.AddChild(_airflow);
        _workload.ItemSelected += _ => Refresh();
        _airflow.ItemSelected += _ => Refresh();
        Caption(_power, "测试电源", 14, Muted);
        foreach (var psu in catalog.PowerSupplies)
            _psu.AddItem($"{psu.Name}   ·   持续 {psu.Continuous} / 短时 {psu.Pulse} W");
        _power.AddChild(_psu);
        _power.AddChild(_runLoad);
        _power.AddChild(_powerResult);
        _psu.ItemSelected += _ => Refresh();
        _runLoad.Pressed += RunLoad;
        var reset = new Button { Text = "还原初始配置" };
        reset.Pressed += Reset;
        form.AddChild(reset);
        _scenario.ItemSelected += _ => Reset();
        Reset();
    }

    private void Reset()
    {
        var setup = _rules!.Catalog.Initial;
        _resetting = true;
        bool liquid = _scenario.Selected == 1;
        _air.Visible = !liquid;
        _water.Visible = liquid;
        _power.Visible = liquid;
        _psu.Select(0);
        _hasTestResult = false;
        _airflow.Select(0);
        _workload.Select(0);
        _views.CurrentTab = 0;
        _views.TabsVisible = liquid;
        _views.SetTabDisabled(1, true);
        _goal.Text = liquid ? "目标：保留前置 360 冷排，让显卡装入机箱。" : "目标：保留 32 GB 双通道，让侧板顺利合上。";
        _hardwareName.Text = _rules.Catalog.Board.Name + "  /  " + (liquid ? _rules.Catalog.Liquid.Name : _rules.Catalog.Cooler.Name);
        _case.Select(setup.CaseIndex);
        _gpu.Select(0);
        _memory.Select(setup.RamIndex);
        for (int i = 0; i < _slots.Count; i++) _slots[i].SetPressedNoSignal(setup.Slots.Contains(i));
        _offset.Value = setup.Offset;
        _resetting = false;
        Refresh();
    }

    private BenchSetup Selection()
    {
        var slots = Enumerable.Range(0, _slots.Count).Where(i => _slots[i].ButtonPressed).ToArray();
        return new BenchSetup(_case.Selected, _memory.Selected, slots, (int)_offset.Value, _scenario.Selected == 1, _gpu.Selected, _airflow.Selected, _workload.Selected, _psu.Selected);
    }

    private void RunLoad()
    {
        if (_rules == null || _runLoad.Disabled) return;
        var result = _rules.TestLoad(Selection(), _psu.Selected);
        _hasTestResult = result.Report != null;
        if (result.Report is { } report)
        {
            _report.Display(report);
            _views.SetTabDisabled(1, false);
            _views.CurrentTab = 1;
        }
        else
        {
            _views.CurrentTab = 0;
            _views.SetTabDisabled(1, true);
        }
        _powerResult.Text = $"持续  {result.Sustained} W  /  峰值  {result.Peak} W\n{result.Message}";
        _powerResult.Modulate = StateColor(result.Status);
        _status.Text = result.Status switch
        {
            "BLOCKED" => "BLOCKED    测试断电",
            "DEGRADED" => "DEGRADED    负载降级",
            _ => "OPTIMAL    负载通过"
        };
        _status.Modulate = _powerResult.Modulate;
    }

    private static Color StateColor(string status) => new(status switch
    {
        "BLOCKED" => "#ff9689",
        "DEGRADED" => "#f5c877",
        _ => "#84dcb1"
    });

    private void Refresh()
    {
        if (_resetting || _rules == null) return;
        var result = _rules.Inspect(Selection());
        if (_hasTestResult) _report.MarkStale();
        _runLoad.Disabled = result.Status == "BLOCKED";
        _powerResult.Text = _runLoad.Disabled ? "请先解除装配阻断。" : _hasTestResult ? "配置已变更，待复测。" : "等待测试。";
        _powerResult.Modulate = Muted;
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
        _status.Modulate = StateColor(result.Status);
        _status.Text = result.Status switch
        {
            "BLOCKED" => "BLOCKED    装配阻断",
            "DEGRADED" => "DEGRADED    带宽降级",
            _ => "OPTIMAL    装配通过"
        };
        _messages.Modulate = _status.Modulate;
        _messages.Text = result.Issues.Length > 0 ? string.Join("\n", result.Issues.Select(issue => issue.Message))
            : result.GpuLimit.HasValue ? "装配目标达成。显卡和前置冷排均可安装。"
            : result.Capacity == 32 ? "装配目标达成。双通道已启用，侧板可闭合。" : "装配通过。请保留两条内存以满足 32 GB 目标。";
    }

    private static Label Caption(Container parent, string text, int size, Color? color = null)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        if (color.HasValue) label.Modulate = color.Value;
        parent.AddChild(label);
        return label;
    }

    public override void _ExitTree()
    {
        _rules?.Dispose();
        _rules = null;
    }
}
