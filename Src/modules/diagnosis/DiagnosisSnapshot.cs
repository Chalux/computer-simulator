using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Simulation;

namespace ComputerSimulator.Modules.Diagnosis;

/// <summary>
/// 故障诊断案件的阶段与结案结果。
/// </summary>
internal enum DiagnosisOutcome
{
    /// <summary>尚未更换任何部件，替换额度仍可用。</summary>
    Pending,

    /// <summary>已安装替换电源并消耗额度，等待负载复测。</summary>
    AwaitingRetest,

    /// <summary>复测通过，且负载状态为 OPTIMAL。</summary>
    RepairedOptimal,

    /// <summary>复测通过，但负载状态为 DEGRADED。</summary>
    RepairedDegraded,

    /// <summary>复测仍触发保护断电。</summary>
    Failed,
}

/// <summary>
/// 诊断案件的一次不可变状态快照。Controller 每次变更都生成新快照，
/// 旧引用不会被原地修改，订阅方可以安全持有并在任意时刻渲染。
/// </summary>
internal sealed record DiagnosisSnapshot(
    BenchSetup Setup,
    FitResult Fit,
    LoadResult Baseline,
    int SelectedPsuIndex,
    int InstalledPsuIndex,
    int ReplacementAllowance,
    LoadResult? Retest,
    DiagnosisOutcome Outcome)
{
    /// <summary>是否已经结案（通过或失败）。</summary>
    internal bool IsFinished => Outcome is DiagnosisOutcome.RepairedOptimal
        or DiagnosisOutcome.RepairedDegraded
        or DiagnosisOutcome.Failed;

    /// <summary>结案前有剩余替换额度即可选择备件；多额度案件可在复测前继续换件。</summary>
    internal bool CanChoosePsu => ReplacementAllowance > 0 && !IsFinished;

    /// <summary>是否可以安装所选电源；当前已装电源不能重复安装。</summary>
    internal bool CanInstall => CanChoosePsu
        && (Outcome == DiagnosisOutcome.Pending || SelectedPsuIndex != InstalledPsuIndex);

    /// <summary>是否可以运行负载复测；当前已装电源必须能装入机箱。</summary>
    internal bool CanRetest => Outcome == DiagnosisOutcome.AwaitingRetest
        && !string.Equals(Fit.Status, "BLOCKED", StringComparison.Ordinal);
}

/// <summary>
/// 候选电源的只读评估结果；只由 Controller 基于硬件目录计算，不改变案件状态。
/// </summary>
internal sealed record DiagnosisCandidate(int Index, PsuInfo Psu, bool SameAsOriginal);
