using ComputerSimulator.Modules.Simulation;
using Godot;

namespace ComputerSimulator.Modules.Benchmark;

public partial class BenchmarkReportView : ScrollContainer
{
    private readonly Label _title = new(), _total = new(), _conditions = new(), _sensors = new(), _stale = new();
    private readonly ScoreRadar _radar = new() { CustomMinimumSize = new Vector2(0, 215), SizeFlagsVertical = SizeFlags.ExpandFill };

    public override void _Ready()
    {
        HorizontalScrollMode = ScrollMode.Disabled;
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        foreach (string side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride("margin_" + side, 12);
        AddChild(margin);
        var page = new VBoxContainer();
        margin.AddChild(page);
        var heading = new HBoxContainer();
        page.AddChild(heading);
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _title.AddThemeFontSizeOverride("font_size", 20);
        _total.Modulate = new Color("#87e5c4");
        _total.AddThemeFontSizeOverride("font_size", 20);
        heading.AddChild(_title);
        heading.AddChild(_total);
        _conditions.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _conditions.Modulate = new Color("#b4c7cd");
        page.AddChild(_conditions);
        _stale.Text = "配置已变更 · 以下为上次测试";
        _stale.Modulate = new Color("#f2c47c");
        page.AddChild(_stale);
        page.AddChild(_radar);
        page.AddChild(new Label { Text = "金色环  100 分基准", Modulate = new Color("#d9b47d"),
            HorizontalAlignment = HorizontalAlignment.Center });
        page.AddChild(new HSeparator());
        _sensors.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        page.AddChild(_sensors);
    }

    public void Display(BenchmarkReport result)
    {
        _title.Text = result.Workload;
        _total.Text = $"总分  {result.Total.Score:F1} · {result.Total.Grade}";
        _conditions.Text = $"{result.Airflow}    /    室温 {result.Ambient:0.#} °C    /    配件参考总价 ¥{result.Price:N0}";
        _radar.Display(result.Axes);
        _sensors.Text = string.Join("\n", SensorLine("CPU", result.Cpu), SensorLine("GPU", result.Gpu),
            $"整机噪声   {result.Noise:F1} dBA");
        _stale.Hide();
    }

    public void MarkStale() => _stale.Show();

    private static string SensorLine(string chip, ChipReading reading)
    {
        var throttle = reading.Throttled ? $"降频损失 {reading.LossPercent:0.#}%" : "正常频率";
        return $"{chip}  预测稳态 {reading.Temperature:F1} °C    /    风扇 {reading.FanPercent}%    /    {throttle}";
    }
}
