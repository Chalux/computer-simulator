using ComputerSimulator.Modules.Benchmark;
using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Simulation;
using Godot;

namespace ComputerSimulator.Modules.Workbench;

public partial class ClearanceWorkbench
{
    internal WorkbenchController? AssemblyController { get; init; }
    internal BenchmarkController ReportController { get; init; } = null!;

    private WorkbenchState? _shownAssembly;
    private bool _renderingSetup;

    private void RenderWorkbenchState(WorkbenchState state)
    {
        var previous = _shownAssembly;
        _shownAssembly = state;

        if (!ReferenceEquals(previous?.Fit, state.Fit))
        {
            ShowSetup(state.Setup);
            ShowAssembly(
                state.Fit,
                AssemblyController!.Catalog,
                state.Setup,
                state.HasTestResult,
                AssemblyController.SandboxTarget);
            if (!state.HasTestResult && state.Load is null)
            {
                _views.CurrentTab = 0;
                _views.SetTabDisabled(1, true);
            }
        }

        if (state.Load is { } run && !ReferenceEquals(previous?.Load, run))
            ShowLoad(run);

        ShowCrateResult(state);
    }

    private void ShowSetup(BenchSetup setup)
    {
        _renderingSetup = true;
        _scenario.Select(setup.Front360 ? 1 : 0);
        _case.Select(setup.CaseIndex);
        _gpu.Select(setup.GpuIndex);
        _memory.Select(setup.RamIndex);
        _psu.Select(setup.PsuIndex);
        _airflow.Select(setup.AirflowIndex);
        _workload.Select(setup.WorkloadIndex);
        for (int i = 0; i < _slots.Count; i++)
            _slots[i].SetPressedNoSignal(setup.Slots.Contains(i));
        _offset.SetValueNoSignal(setup.Offset);
        _renderingSetup = false;

        _air.Visible = !setup.Front360;
        _water.Visible = setup.Front360;
        _loadPanel.Visible = setup.Front360;
        _views.TabsVisible = setup.Front360;
        string workload = Catalog.Benchmark.Workloads[setup.WorkloadIndex].Name;
        _goal.Text = CommissionMode
            ? $"使用 {workload} 负载完成测试。"
            : CrateMode ? $"从箱内选件并通过 {workload} 整机负载测试，即可提交评分。"
            : setup.Front360 ? $"目标：保留 {Catalog.Liquid.Name}，让显卡装入机箱。"
            : SandboxGoal();
        _hardwareName.Text = $"{Catalog.Board.Name}  /  {(setup.Front360 ? Catalog.Liquid.Name : Catalog.Cooler.Name)}";
        if (CrateMode) _hardwareName.Text += $"  /  {Catalog.Ram[setup.RamIndex].Name}";
        _offsetText.Text = $"{setup.Offset} mm";
        SyncOptionTooltip(_scenario);
        SyncOptionTooltip(_case);
        SyncOptionTooltip(_gpu);
        SyncOptionTooltip(_memory);
        SyncOptionTooltip(_workload);
        SyncOptionTooltip(_airflow);
        SyncOptionTooltip(_psu);
    }

    private string SandboxGoal()
    {
        WorkbenchTarget? target = AssemblyController?.SandboxTarget;
        return target is { CapacityGb: > 0 } value
            ? $"目标：保留 {value.CapacityGb} GB {ChannelText(value.Channels)}，让侧板顺利合上。"
            : "目标：保留初始内存配置，让侧板顺利合上。";
    }

    private static string ChannelText(int channels) => channels switch
    {
        1 => "单通道",
        2 => "双通道",
        _ => $"{channels} 通道",
    };

    private void ShowAssembly(
        FitResult result,
        BenchCatalog catalog,
        BenchSetup setup,
        bool testedBefore,
        WorkbenchTarget? target = null)
    {
        _viewport.Display(result, catalog, setup);
        _section.Display(result);
        _metrics.Text = $"侧板余量  {result.Headroom:+0;-0;0} mm\n{result.Capacity} GB    /    {result.Channels} 通道    /    {result.Bandwidth:0.0} GB/s";
        if (result.GpuLimit.HasValue)
        {
            var kit = catalog.Liquid;
            _metrics.Text = $"冷排 {kit.CoreThickness} + 风扇 {kit.FanThickness} mm\n显卡有效限长  {result.GpuLimit} mm    /    余量  {result.GpuMargin:+0;-0;0} mm";
            if (result.PsuMargin.HasValue)
                _metrics.Text += $"\n电源仓余量  {result.PsuMargin:+0;-0;0} mm";
        }

        _runLoad.Disabled = result.Status == "BLOCKED";
        _powerResult.Text = _runLoad.Disabled ? "请先解除装配阻断。" : testedBefore ? "配置已变更，待复测。" : "等待测试。";
        SetTextColor(_powerResult, WorkbenchStyle.Muted);

        var color = StateColor(result.Status);
        SetStatus(result.Status switch
        {
            "BLOCKED" => "BLOCKED    装配阻断",
            "DEGRADED" => "DEGRADED    带宽降级",
            _ => "OPTIMAL    装配通过"
        }, color);
        SetTextColor(_messages, color);
        _messages.Text = result.Issues.Length > 0
            ? string.Join("\n", result.Issues.Select(issue => issue.Message))
            : result.GpuLimit.HasValue
                ? "装配目标达成。显卡和前置冷排均可安装。"
                : target is { CapacityGb: > 0 } goal
                    ? result.Capacity >= goal.CapacityGb && result.Channels >= goal.Channels
                        ? $"装配目标达成。{ChannelText(goal.Channels)}已启用，侧板可闭合。"
                        : $"装配通过。请保留 {goal.CapacityGb} GB / {goal.Channels} 通道内存以满足目标。"
                    : "装配通过。";
        _messages.TooltipText = _messages.Text;
    }

    private void ShowLoad(LoadResult result)
    {
        if (result.Report is not null)
        {
            _views.SetTabDisabled(1, false);
            _views.CurrentTab = 1;
        }
        else
        {
            _views.CurrentTab = 0;
            _views.SetTabDisabled(1, true);
        }

        _powerResult.Text = $"持续  {result.Sustained} W  /  峰值  {result.Peak} W\n{result.Message}";
        var color = StateColor(result.Status);
        SetTextColor(_powerResult, color);
        SetStatus(result.Status switch
        {
            "BLOCKED" => "BLOCKED    测试断电",
            "DEGRADED" => "DEGRADED    负载降级",
            _ => "OPTIMAL    负载通过"
        }, color);
    }

    private void ShowCrateResult(WorkbenchState state)
    {
        if (!CrateMode) return;

        bool locked = state.Submitted;
        _submit.Visible = !locked;
        _submit.Disabled = locked || state.Load?.Report is null;
        _replay.Visible = locked;
        _case.Disabled = locked;
        _gpu.Disabled = locked;
        _psu.Disabled = locked;
        _reset.Disabled = locked;
        _runLoad.Disabled = locked || state.Fit.Status == "BLOCKED";

        if (locked && state.Load?.Report is { } submitted && state.Best is { } best)
        {
            SetTextColor(_crateResult, StateColor("OPTIMAL"));
            _crateResult.Text = $"本次得分  {submitted.Total.Score:F1} · {submitted.Total.Grade}    " +
                $"本次访问最佳  {best.Score:F1} · {best.Grade}\n配置已锁定，点击“再次挑战”开始新一轮。";
        }
        else if (state.Load is { } run)
        {
            SetTextColor(_crateResult, run.Report is null ? StateColor("BLOCKED") : WorkbenchStyle.Muted);
            _crateResult.Text = run.Report is { } report
                ? $"当前配置测试总分  {report.Total.Score:F1} · {report.Total.Grade}，可提交。"
                : "测试未通过，提交不可用。";
        }
        else
        {
            SetTextColor(_crateResult, WorkbenchStyle.Muted);
            _crateResult.Text = state.Best is { } bestRun
                ? $"本次访问最佳  {bestRun.Score:F1} · {bestRun.Grade}\n当前配置尚未通过测试。"
                : $"完成当前配置的 {Catalog.Benchmark.Workloads[state.Setup.WorkloadIndex].Name} 负载测试后可提交评分。";
        }
    }
}
