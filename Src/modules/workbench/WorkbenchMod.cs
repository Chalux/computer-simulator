using ComputerSimulator.Modules.Benchmark;
using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Simulation;
using KemoCard.Frame.Mvc;

namespace ComputerSimulator.Modules.Workbench;

/// <summary>沙盒默认配置的目标内存规格；由注入的基准初始配置实测得出。</summary>
public sealed record WorkbenchTarget(int CapacityGb, int Channels);

/// <summary>
/// 工作台一次状态快照。Setup.Slots 由控制器复制，调用方修改不会回写权威状态。
/// </summary>
public sealed record WorkbenchState(
    BenchSetup Setup,
    FitResult Fit,
    LoadResult? Load,
    bool HasTestResult,
    bool Submitted,
    GradedScore? Best,
    BenchmarkRunIdentity? Identity = null);

/// <summary>
/// 持有本次工作台的装配配置、负载结果和神秘箱挑战进度。
/// </summary>
internal sealed class WorkbenchMod : BaseMod
{
    public WorkbenchMod() : base("workbench")
    {
    }

    internal WorkbenchState State { get; set; } = null!;
}
