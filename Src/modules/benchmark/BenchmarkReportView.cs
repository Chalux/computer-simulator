using ComputerSimulator.Modules.Simulation;
using ComputerSimulator.Modules.Workbench;
using Godot;

namespace ComputerSimulator.Modules.Benchmark;

/// <summary>一次成功负载测试时实际选择的配置身份，供沙盒对照。</summary>
public sealed record BenchmarkRunIdentity(string CaseName, string GpuName, string PsuName, string RamName, string Slots);
public sealed record BenchmarkRunSnapshot(BenchmarkReport Report, BenchmarkRunIdentity Identity);

public partial class BenchmarkReportView : ScrollContainer
{
    private readonly Label _title = new(), _total = new(), _conditions = new(), _sensors = new(), _stale = new();
    private readonly ScoreRadar _radar = new() { CustomMinimumSize = new Vector2(0, 215), SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly Label _legend = WorkbenchStyle.MakeLabel("金色环  100 分基准", 11, WorkbenchStyle.Amber);
    private HBoxContainer _baselineRow = null!, _currentRow = null!, _summary = null!, _axisRow = null!;
    private Label _baselineText = null!, _currentText = null!, _compareHint = null!;
    private Label _totalCompare = null!, _totalDelta = null!, _priceCompare = null!, _priceDelta = null!;
    private Label _noiseCompare = null!, _noiseDelta = null!;
    private GridContainer _axisGrid = null!;
    private Button _pin = null!, _clear = null!;
    private readonly List<Label> _axisDelta = [];
    private BenchmarkRunSnapshot? _baseline, _current;
    private GradedScore[] _axes = [];
    private bool _currentFresh;
    private string _notice = "";

    /// <summary>仅沙盒报告创建对照界面；神秘箱保持原有报告。</summary>
    public bool ComparisonEnabled { get; set; }

    public override void _Ready()
    {
        HorizontalScrollMode = ScrollMode.Disabled;
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        foreach (string side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride("margin_" + side, 12);
        AddChild(margin);
        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 6);
        margin.AddChild(page);

        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 10);
        page.AddChild(heading);
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _title.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _title.AddThemeFontSizeOverride("font_size", 18);
        _title.AddThemeColorOverride("font_color", WorkbenchStyle.Text);
        _total.AddThemeFontOverride("font", WorkbenchStyle.MonoFont);
        _total.AddThemeFontSizeOverride("font_size", 18);
        _total.AddThemeColorOverride("font_color", WorkbenchStyle.Mint);
        _total.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        heading.AddChild(_title);
        heading.AddChild(_total);

        _conditions.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _conditions.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _conditions.AddThemeFontSizeOverride("font_size", 12);
        _conditions.AddThemeColorOverride("font_color", WorkbenchStyle.Muted);
        page.AddChild(_conditions);

        _stale.Text = "配置已变更 · 以下为上次测试";
        _stale.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _stale.AddThemeFontSizeOverride("font_size", 11);
        _stale.AddThemeColorOverride("font_color", WorkbenchStyle.Amber);
        page.AddChild(_stale);

        if (ComparisonEnabled) BuildComparison(page);

        page.AddChild(_radar);
        _legend.HorizontalAlignment = HorizontalAlignment.Center;
        _legend.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        page.AddChild(_legend);
        page.AddChild(WorkbenchStyle.Rule(WorkbenchStyle.BorderSoft));

        _sensors.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _sensors.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _sensors.AddThemeFontSizeOverride("font_size", 12);
        _sensors.AddThemeColorOverride("font_color", WorkbenchStyle.TextSoft);
        _sensors.AddThemeConstantOverride("line_spacing", 4);
        page.AddChild(_sensors);

        if (ComparisonEnabled) UpdateComparison();
    }

    public void Display(BenchmarkReport result, BenchmarkRunIdentity? identity = null)
    {
        _title.Text = result.Workload;
        _total.Text = $"总分  {result.Total.Score:F1} · {result.Total.Grade}";
        _conditions.Text = $"{result.Airflow}    /    室温 {result.Ambient:0.#} °C    /    配件参考总价 ¥{result.Price:N0}";
        _stale.Hide();
        _axes = result.Axes;
        _notice = "";
        // 先无条件刷新雷达；对照模式下的 UpdateComparison 会用基准环再覆盖一次。
        // 这样即使对照界面尚未构建（ComparisonEnabled 被延后设置），报告也不会显示空白雷达。
        _radar.Display(result.Axes);
        if (ComparisonEnabled)
        {
            _current = identity == null ? null : new BenchmarkRunSnapshot(result, identity);
            _currentFresh = _current != null;
            UpdateComparison();
        }
        _sensors.Text = string.Join("\n", SensorLine("CPU", result.Cpu), SensorLine("GPU", result.Gpu),
            $"整机噪声   {result.Noise:F1} dBA");
    }

    /// <summary>配置变更：当前结果失效，待复测；已保存的对照仍在。</summary>
    public void MarkStale()
    {
        _stale.Show();
        if (!ComparisonEnabled) return;
        _current = null;
        _currentFresh = false;
        _notice = "配置已变更，复测通过后可继续。";
        UpdateComparison();
    }

    /// <summary>测试失败或还原配置：当前结果作废，已保存的对照仍在。</summary>
    public void InvalidateCurrent(string? notice = null)
    {
        if (!ComparisonEnabled) return;
        _current = null;
        _currentFresh = false;
        _notice = notice ?? "";
        UpdateComparison();
    }

    private void BuildComparison(VBoxContainer page)
    {
        var panel = WorkbenchStyle.SurfacePanel(WorkbenchStyle.Inset, WorkbenchStyle.BorderSoft, 10, 8, 2);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        page.AddChild(panel);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 4);
        panel.AddChild(body);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 8);
        body.AddChild(head);
        head.AddChild(WorkbenchStyle.MakeLabel("对照比较", 12, WorkbenchStyle.Text));
        var caption = WorkbenchStyle.MakeLabel("变化 = 当前 − 对照", 10, WorkbenchStyle.Faint);
        caption.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(caption);
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        _pin = ComparisonButton("设为对照");
        _pin.Pressed += PinComparison;
        head.AddChild(_pin);
        _clear = ComparisonButton("清除对照");
        _clear.Pressed += ClearComparison;
        head.AddChild(_clear);

        var identities = new VBoxContainer();
        identities.AddThemeConstantOverride("separation", 2);
        body.AddChild(identities);
        _baselineRow = IdentityRow(identities, "对照", WorkbenchStyle.Amber, out _baselineText);
        _currentRow = IdentityRow(identities, "当前", WorkbenchStyle.Mint, out _currentText);

        _summary = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _summary.AddThemeConstantOverride("separation", 16);
        _summary.AddChild(Metric("总分", out _totalCompare, out _totalDelta));
        _summary.AddChild(Metric("价格", out _priceCompare, out _priceDelta));
        _summary.AddChild(Metric("噪声", out _noiseCompare, out _noiseDelta));
        body.AddChild(_summary);

        _axisRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _axisRow.AddThemeConstantOverride("separation", 8);
        var axisCaption = WorkbenchStyle.MakeLabel("分项 Δ（分）", 10, WorkbenchStyle.Faint);
        axisCaption.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _axisRow.AddChild(axisCaption);
        _axisGrid = new GridContainer { Columns = ScoreRadar.AxisNames.Length, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _axisGrid.AddThemeConstantOverride("h_separation", 6);
        for (int i = 0; i < ScoreRadar.AxisNames.Length; i++)
        {
            var delta = WorkbenchStyle.MakeLabel("", 11, WorkbenchStyle.Muted, WorkbenchStyle.MonoFont);
            delta.HorizontalAlignment = HorizontalAlignment.Center;
            delta.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            delta.MouseFilter = MouseFilterEnum.Pass;
            _axisGrid.AddChild(delta);
            _axisDelta.Add(delta);
        }
        _axisRow.AddChild(_axisGrid);
        body.AddChild(_axisRow);

        _compareHint = WorkbenchStyle.MakeLabel("", 10, WorkbenchStyle.Muted);
        _compareHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _compareHint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.AddChild(_compareHint);
    }

    private static Button ComparisonButton(string text)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(80, 26),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        WorkbenchStyle.ApplyGhost(button, 26);
        return button;
    }

    private static HBoxContainer IdentityRow(Container parent, string tag, Color color, out Label value)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var name = WorkbenchStyle.MakeLabel(tag, 11, color);
        name.CustomMinimumSize = new Vector2(24, 0);
        row.AddChild(name);
        value = WorkbenchStyle.MakeLabel("", 11, WorkbenchStyle.TextSoft);
        value.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        value.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(value);
        parent.AddChild(row);
        return row;
    }

    private static HBoxContainer Metric(string caption, out Label value, out Label delta)
    {
        var group = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        group.AddThemeConstantOverride("separation", 4);
        var name = WorkbenchStyle.MakeLabel(caption, 10, WorkbenchStyle.Faint);
        name.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        group.AddChild(name);
        value = WorkbenchStyle.MakeLabel("", 11, WorkbenchStyle.TextSoft, WorkbenchStyle.MonoFont);
        value.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        group.AddChild(value);
        delta = WorkbenchStyle.MakeLabel("", 11, WorkbenchStyle.Muted, WorkbenchStyle.MonoFont);
        delta.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        group.AddChild(delta);
        return group;
    }

    private void PinComparison()
    {
        if (!_currentFresh || _current == null) return;
        _baseline = _current;
        _notice = "";
        UpdateComparison();
    }

    private void ClearComparison()
    {
        if (_baseline == null) return;
        _baseline = null;
        _notice = "";
        UpdateComparison();
    }

    private void UpdateComparison()
    {
        if (!ComparisonEnabled || _pin == null) return;

        bool sameRun = _baseline != null && _current != null && ReferenceEquals(_baseline, _current);
        bool workloadMatches = _baseline != null && _current != null &&
            string.Equals(_baseline.Report.Workload, _current.Report.Workload, StringComparison.Ordinal);
        bool canCompare = _currentFresh && !sameRun && workloadMatches;
        bool workloadMismatch = _currentFresh && !sameRun && _baseline != null && _current != null && !workloadMatches;

        _baselineRow.Visible = _baseline != null;
        if (_baseline != null)
            _baselineText.Text = FormatIdentity(_baseline, sameRun);
        _currentRow.Visible = _current != null && !sameRun;
        if (_current != null && !sameRun)
            _currentText.Text = FormatIdentity(_current, false);

        _summary.Visible = canCompare;
        _axisRow.Visible = canCompare;
        if (canCompare) FillComparison(_baseline!, _current!);

        _radar.Display(_axes, canCompare ? _baseline!.Report.Axes : null);
        _legend.Text = canCompare ? "金色环  100 分基准    ·    金色虚线  对照" : "金色环  100 分基准";

        _pin.Disabled = !_currentFresh || _current == null;
        _pin.TooltipText = _pin.Disabled ? "配置已变更或没有有效测试，复测通过后才能设为对照"
            : _baseline == null ? "将本次通过的测试设为对照"
            : "用本次通过的测试替换当前对照";
        _clear.Disabled = _baseline == null;
        _clear.TooltipText = _baseline == null ? "当前没有对照" : "清除本次会话保存的对照";

        string hint;
        bool warn = false;
        if (workloadMismatch)
        {
            hint = $"工作负载不同，无法对比（对照 {_baseline!.Report.Workload} · 当前 {_current!.Report.Workload}）。" +
                "请切换回相同工作负载复测，或将本次结果设为对照替换基准。";
            warn = true;
        }
        else if (canCompare) hint = "";
        else if (sameRun) hint = "本次结果已设为对照；下次同工作负载测试将自动比较。";
        else if (_baseline == null && _current == null)
        {
            hint = _notice.Length > 0 ? _notice : "尚无对照。完成一次通过的测试后可设为对照。";
            warn = _notice.Length > 0;
        }
        else if (_baseline == null) hint = "尚无对照。可将本次通过的测试设为对照。";
        else if (_current == null)
        {
            hint = _notice.Length > 0 ? _notice : "对照已保留；完成一次通过的测试后可继续比较。";
            warn = _notice.Length > 0;
        }
        else hint = "对照已保留；完成一次通过的测试后可继续比较。";

        _compareHint.Text = hint;
        _compareHint.Visible = hint.Length > 0;
        _compareHint.AddThemeColorOverride("font_color", warn ? WorkbenchStyle.Amber : WorkbenchStyle.Muted);
    }

    private void FillComparison(BenchmarkRunSnapshot baseline, BenchmarkRunSnapshot current)
    {
        var b = baseline.Report;
        var c = current.Report;

        float total = c.Total.Score - b.Total.Score;
        _totalCompare.Text = $"{b.Total.Score:F1} {b.Total.Grade} → {c.Total.Score:F1} {c.Total.Grade}";
        SetDelta(_totalDelta, total, true, total.ToString("+0.0 分;-0.0 分;0.0 分"));

        int price = c.Price - b.Price;
        _priceCompare.Text = $"¥{b.Price:N0} → ¥{c.Price:N0}";
        SetDelta(_priceDelta, price, false, price.ToString("+¥#,##0;-¥#,##0;¥0"));

        float noise = c.Noise - b.Noise;
        _noiseCompare.Text = $"{b.Noise:F1} → {c.Noise:F1} dBA";
        SetDelta(_noiseDelta, noise, false, noise.ToString("+0.0 dBA;-0.0 dBA;0.0 dBA"));

        for (int i = 0; i < _axisDelta.Count; i++)
        {
            var label = _axisDelta[i];
            if (i >= b.Axes.Length || i >= c.Axes.Length)
            {
                label.Text = $"{ScoreRadar.AxisNames[i]} —";
                label.AddThemeColorOverride("font_color", WorkbenchStyle.Muted);
                label.TooltipText = "";
                continue;
            }
            float delta = c.Axes[i].Score - b.Axes[i].Score;
            label.Text = $"{ScoreRadar.AxisNames[i]} {delta:+0.0;-0.0;0.0}";
            label.AddThemeColorOverride("font_color", DeltaColor(delta, true));
            label.TooltipText = $"{ScoreRadar.AxisNames[i]}  对照 {b.Axes[i].Score:F1} → 当前 {c.Axes[i].Score:F1}  " +
                $"变化 {delta:+0.0;-0.0;0.0} 分";
        }
    }

    private static void SetDelta(Label label, float delta, bool higherIsBetter, string text)
    {
        label.Text = text;
        label.AddThemeColorOverride("font_color", DeltaColor(delta, higherIsBetter));
    }

    private static Color DeltaColor(float delta, bool higherIsBetter)
    {
        if (Mathf.Abs(delta) < .05f) return WorkbenchStyle.Muted;
        return (delta > 0) == higherIsBetter ? WorkbenchStyle.Mint : WorkbenchStyle.Coral;
    }

    private static string FormatIdentity(BenchmarkRunSnapshot snapshot, bool self)
    {
        var id = snapshot.Identity;
        var run = snapshot.Report;
        string slots = string.IsNullOrWhiteSpace(id.Slots) ? "未插内存" : id.Slots;
        string line = $"机箱 {id.CaseName}  ·  显卡 {id.GpuName}  ·  电源 {id.PsuName}  ·  " +
            $"内存 {id.RamName} [{slots}]  ·  负载 {run.Workload}  ·  风道 {run.Airflow}";
        return self ? line + "（本次）" : line;
    }

    private static string SensorLine(string chip, ChipReading reading)
    {
        var throttle = reading.Throttled ? $"降频损失 {reading.LossPercent:0.#}%" : "正常频率";
        return $"{chip}  预测稳态 {reading.Temperature:F1} °C    /    风扇 {reading.FanPercent}%    /    {throttle}";
    }
}
