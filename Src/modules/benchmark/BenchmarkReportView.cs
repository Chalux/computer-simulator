using ComputerSimulator.Modules.Simulation;
using ComputerSimulator.Modules.Workbench;
using Godot;
using KemoCard.Frame.Mvc;
using KemoCard.Frame.UI;
using KemoCard.Frame.UI.Base;

namespace ComputerSimulator.Modules.Benchmark;

/// <summary>
/// 负载报告子视图：骨架在 <c>benchmark_report.tscn</c>，由工作台场景实例化。
/// 它没有自己的路由，因此控制器由所属窗口在绑定会话时注入；订阅仍走 BindingScope。
/// </summary>
public partial class BenchmarkReportView : BaseCmp
{
    private Label _title = null!, _total = null!, _conditions = null!, _sensors = null!, _stale = null!;
    private ScoreRadar _radar = null!;
    private Label _legend = null!;
    private PanelContainer _comparisonPanel = null!;
    private HBoxContainer _baselineRow = null!, _currentRow = null!, _summary = null!, _axisRow = null!;
    private Label _baselineTag = null!, _baselineText = null!, _currentTag = null!, _currentText = null!;
    private Label _compareHint = null!;
    private Label _totalCompare = null!, _totalDelta = null!;
    private Label _priceCompare = null!, _priceDelta = null!;
    private Label _noiseCompare = null!, _noiseDelta = null!;
    private GridContainer _axisGrid = null!;
    private Button _pin = null!, _clear = null!;

    private readonly List<Label> _axisDelta = [];
    private readonly BindingScope _stateScope = new();
    private BenchmarkController? _controller;
    private BenchmarkController? _boundController;
    private bool _ready;

    protected override void InitEvent()
    {
        Binder.OnPressed(GetNode<Button>("%PinButton"), PinComparison);
        Binder.OnPressed(GetNode<Button>("%ClearButton"), ClearComparison);
    }

    protected override void OnReady()
    {
        _title = GetNode<Label>("%Title");
        _total = GetNode<Label>("%Total");
        _conditions = GetNode<Label>("%Conditions");
        _sensors = GetNode<Label>("%Sensors");
        _stale = GetNode<Label>("%Stale");
        _radar = GetNode<ScoreRadar>("%Radar");
        _legend = GetNode<Label>("%Legend");
        _comparisonPanel = GetNode<PanelContainer>("%ComparisonPanel");
        _baselineRow = GetNode<HBoxContainer>("%BaselineRow");
        _currentRow = GetNode<HBoxContainer>("%CurrentRow");
        _summary = GetNode<HBoxContainer>("%Summary");
        _axisRow = GetNode<HBoxContainer>("%AxisRow");
        _baselineTag = GetNode<Label>("%BaselineTag");
        _baselineText = GetNode<Label>("%BaselineText");
        _currentTag = GetNode<Label>("%CurrentTag");
        _currentText = GetNode<Label>("%CurrentText");
        _compareHint = GetNode<Label>("%CompareHint");
        _totalCompare = GetNode<Label>("%TotalCompare");
        _totalDelta = GetNode<Label>("%TotalDelta");
        _priceCompare = GetNode<Label>("%PriceCompare");
        _priceDelta = GetNode<Label>("%PriceDelta");
        _noiseCompare = GetNode<Label>("%NoiseCompare");
        _noiseDelta = GetNode<Label>("%NoiseDelta");
        _axisGrid = GetNode<GridContainer>("%AxisGrid");
        _pin = GetNode<Button>("%PinButton");
        _clear = GetNode<Button>("%ClearButton");

        ApplyStyle();
        BuildAxisDeltas();
        _ready = true;

        if (_controller is not null) Attach(_controller);
    }

    /// <summary>由所属工作台注入本次会话的报告控制器；重复调用只重建状态订阅。</summary>
    internal void Bind(BenchmarkController controller)
    {
        _controller = controller;
        if (!_ready) return;

        if (ReferenceEquals(_boundController, controller) && _stateScope.HasBindings)
        {
            Render(controller.State);
            return;
        }

        Attach(controller);
    }

    protected override void OnExitTree()
    {
        _stateScope.UnbindAll();
        _boundController = null;
    }

    private void Attach(BenchmarkController controller)
    {
        _stateScope.UnbindAll();
        var bus = ((IEventBusProvider)controller).EventBus;
        var listener = bus.On(
            BenchmarkEvents.StateChanged,
            (state, _) => Render(state),
            this);
        _stateScope.Add(listener.Off);
        _boundController = controller;
        Render(controller.State);
    }

    private void ApplyStyle()
    {
        _title.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _title.AddThemeFontSizeOverride("font_size", 18);
        _title.AddThemeColorOverride("font_color", WorkbenchStyle.Text);
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _total.AddThemeFontOverride("font", WorkbenchStyle.MonoFont);
        _total.AddThemeFontSizeOverride("font_size", 18);
        _total.AddThemeColorOverride("font_color", WorkbenchStyle.Mint);
        _total.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        _conditions.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _conditions.AddThemeFontSizeOverride("font_size", 12);
        _conditions.AddThemeColorOverride("font_color", WorkbenchStyle.Muted);
        _conditions.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        _stale.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _stale.AddThemeFontSizeOverride("font_size", 11);
        _stale.AddThemeColorOverride("font_color", WorkbenchStyle.Amber);

        _sensors.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _sensors.AddThemeFontSizeOverride("font_size", 12);
        _sensors.AddThemeColorOverride("font_color", WorkbenchStyle.TextSoft);
        _sensors.AddThemeConstantOverride("line_spacing", 4);
        _sensors.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        _legend.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _legend.AddThemeFontSizeOverride("font_size", 11);
        _legend.AddThemeColorOverride("font_color", WorkbenchStyle.Amber);
        _legend.HorizontalAlignment = HorizontalAlignment.Center;
        _legend.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _comparisonPanel.AddThemeStyleboxOverride("panel",
            WorkbenchStyle.Box(WorkbenchStyle.Inset, WorkbenchStyle.BorderSoft, 10, 8, 2));

        StyleLabel("%ComparisonTitle", 12, WorkbenchStyle.Text);
        StyleLabel("%ComparisonCaption", 10, WorkbenchStyle.Faint);
        StyleLabel("%AxisCaption", 10, WorkbenchStyle.Faint);
        _baselineTag.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _baselineTag.AddThemeFontSizeOverride("font_size", 11);
        _baselineTag.AddThemeColorOverride("font_color", WorkbenchStyle.Amber);
        _baselineTag.CustomMinimumSize = new Vector2(24, 0);
        _currentTag.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _currentTag.AddThemeFontSizeOverride("font_size", 11);
        _currentTag.AddThemeColorOverride("font_color", WorkbenchStyle.Mint);
        _currentTag.CustomMinimumSize = new Vector2(24, 0);

        foreach (Label label in new[] { _baselineText, _currentText })
        {
            label.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
            label.AddThemeFontSizeOverride("font_size", 11);
            label.AddThemeColorOverride("font_color", WorkbenchStyle.TextSoft);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        }

        foreach (Label label in new[]
                 {
                     _totalCompare, _priceCompare, _noiseCompare, _totalDelta, _priceDelta, _noiseDelta,
                 })
        {
            label.AddThemeFontOverride("font", WorkbenchStyle.MonoFont);
            label.AddThemeFontSizeOverride("font_size", 11);
            label.AddThemeColorOverride("font_color", WorkbenchStyle.TextSoft);
            label.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        }

        foreach (string name in new[] { "%TotalCaption", "%PriceCaption", "%NoiseCaption" })
        {
            StyleLabel(name, 10, WorkbenchStyle.Faint);
        }

        _compareHint.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _compareHint.AddThemeFontSizeOverride("font_size", 10);
        _compareHint.AddThemeColorOverride("font_color", WorkbenchStyle.Muted);
        _compareHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _compareHint.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        WorkbenchStyle.ApplyGhost(_pin, 26);
        WorkbenchStyle.ApplyGhost(_clear, 26);
        _pin.CustomMinimumSize = new Vector2(80, 26);
        _clear.CustomMinimumSize = new Vector2(80, 26);
    }

    private void StyleLabel(string name, int size, Color color)
    {
        var label = GetNode<Label>(name);
        label.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
    }

    private void BuildAxisDeltas()
    {
        _axisGrid.Columns = ScoreRadar.AxisNames.Length;
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
    }

    private void PinComparison() => _controller?.PinComparison();

    private void ClearComparison() => _controller?.ClearComparison();

    private void Render(BenchmarkViewState state)
    {
        if (!_ready) return;

        if (state.Report is { } report)
        {
            _title.Text = report.Workload;
            _total.Text = $"总分  {report.Total.Score:F1} · {report.Total.Grade}";
            _conditions.Text = $"{report.Airflow}    /    室温 {report.Ambient:0.#} °C    /    配件参考总价 ¥{report.Price:N0}";
            _stale.Visible = state.IsStale;
            _radar.Display(report.Axes);
            _sensors.Text = string.Join("\n", SensorLine("CPU", report.Cpu), SensorLine("GPU", report.Gpu),
                $"整机噪声   {report.Noise:F1} dBA");
        }

        _comparisonPanel.Visible = state.ComparisonEnabled;
        if (!state.ComparisonEnabled) return;

        RenderComparison(state);
    }

    private void RenderComparison(BenchmarkViewState state)
    {
        BenchmarkComparison comparison = state.Comparison;
        BenchmarkRunSnapshot? baseline = state.Baseline;
        BenchmarkRunSnapshot? current = state.Current;

        _baselineRow.Visible = baseline is not null;
        if (baseline is not null)
            _baselineText.Text = FormatIdentity(baseline, comparison.SameRun);
        _currentRow.Visible = current is not null && !comparison.SameRun;
        if (current is not null && !comparison.SameRun)
            _currentText.Text = FormatIdentity(current, false);

        _summary.Visible = comparison.CanCompare;
        _axisRow.Visible = comparison.CanCompare;
        if (comparison.CanCompare)
            FillComparison(state);

        if (comparison.CanCompare && state.Report is not null && baseline is not null)
            _radar.Display(state.Report.Axes, baseline.Report.Axes);

        _legend.Text = comparison.CanCompare ? "金色环  100 分基准    ·    金色虚线  对照" : "金色环  100 分基准";

        _pin.Disabled = !comparison.CanPin;
        _pin.TooltipText = _pin.Disabled ? "配置已变更或没有有效测试，复测通过后才能设为对照"
            : baseline is null ? "将本次通过的测试设为对照"
            : "用本次通过的测试替换当前对照";
        _clear.Disabled = !comparison.CanClear;
        _clear.TooltipText = comparison.CanClear ? "清除本次会话保存的对照" : "当前没有对照";

        (string hint, bool warn) = DescribeComparison(state);
        _compareHint.Text = hint;
        _compareHint.Visible = hint.Length > 0;
        _compareHint.AddThemeColorOverride("font_color", warn ? WorkbenchStyle.Amber : WorkbenchStyle.Muted);
    }

    private void FillComparison(BenchmarkViewState state)
    {
        BenchmarkReport b = state.Baseline!.Report;
        BenchmarkReport c = state.Current!.Report;
        BenchmarkDeltas deltas = state.Comparison.Deltas!;

        _totalCompare.Text = $"{b.Total.Score:F1} {b.Total.Grade} → {c.Total.Score:F1} {c.Total.Grade}";
        SetDelta(_totalDelta, deltas.Total, true, deltas.Total.ToString("+0.0 分;-0.0 分;0.0 分"));

        _priceCompare.Text = $"¥{b.Price:N0} → ¥{c.Price:N0}";
        SetDelta(_priceDelta, deltas.Price, false, deltas.Price.ToString("+¥#,##0;-¥#,##0;¥0"));

        _noiseCompare.Text = $"{b.Noise:F1} → {c.Noise:F1} dBA";
        SetDelta(_noiseDelta, deltas.Noise, false, deltas.Noise.ToString("+0.0 dBA;-0.0 dBA;0.0 dBA"));

        for (int i = 0; i < _axisDelta.Count; i++)
        {
            Label label = _axisDelta[i];
            if (i >= deltas.Axes.Length || !deltas.Axes[i].Compared)
            {
                label.Text = $"{ScoreRadar.AxisNames[i]} —";
                label.AddThemeColorOverride("font_color", WorkbenchStyle.Muted);
                label.TooltipText = "";
                continue;
            }

            BenchmarkAxisDelta axis = deltas.Axes[i];
            label.Text = $"{ScoreRadar.AxisNames[i]} {axis.Delta:+0.0;-0.0;0.0}";
            label.AddThemeColorOverride("font_color", DeltaColor(axis.Delta, true));
            label.TooltipText = $"{ScoreRadar.AxisNames[i]}  对照 {axis.BaselineScore:F1} → 当前 {axis.CurrentScore:F1}  " +
                $"变化 {axis.Delta:+0.0;-0.0;0.0} 分";
        }
    }

    private static (string Hint, bool Warn) DescribeComparison(BenchmarkViewState state)
    {
        BenchmarkComparison comparison = state.Comparison;
        switch (comparison.Status)
        {
            case BenchmarkComparisonStatus.WorkloadMismatch:
                return ($"工作负载不同，无法对比（对照 {state.Baseline!.Report.Workload} · 当前 {state.Current!.Report.Workload}）。" +
                    "请切换回相同工作负载复测，或将本次结果设为对照替换基准。", true);
            case BenchmarkComparisonStatus.Comparable:
                return ("", false);
            case BenchmarkComparisonStatus.PinnedAsBaseline:
                return ("本次结果已设为对照；下次同工作负载测试将自动比较。", false);
            case BenchmarkComparisonStatus.AwaitingFirstRun:
                return ("尚无对照。完成一次通过的测试后可设为对照。", false);
            case BenchmarkComparisonStatus.AwaitingFirstRunBlocked:
                return (comparison.Notice, true);
            case BenchmarkComparisonStatus.AwaitingBaseline:
                return ("尚无对照。可将本次通过的测试设为对照。", false);
            case BenchmarkComparisonStatus.AwaitingRetest:
                return ("对照已保留；完成一次通过的测试后可继续比较。", false);
            case BenchmarkComparisonStatus.AwaitingRetestBlocked:
                return (comparison.Notice, true);
            default:
                return ("对照已保留；完成一次通过的测试后可继续比较。", false);
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
