using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Simulation;
using KemoCard.Frame.Mvc;

namespace ComputerSimulator.Modules.Diagnosis;

/// <summary>
/// 故障诊断案件的 Model：持有案件初始配置、替换额度与规则实测结果，
/// 只通过不可变快照对外暴露数据。状态变更入口仅由 <see cref="DiagnosisController"/> 调用。
/// 题面（案件编号、故障电源、初始配置、文案）由内容 Mod 定义，Controller 在构造时安装。
/// </summary>
internal sealed class DiagnosisMod : BaseMod
{
    internal const string ModIdValue = "diagnosis";

    internal DiagnosisMod() : base(ModIdValue)
    {
    }

    internal DiagnosisCaseContent Case { get; set; } = null!;

    private DiagnosisSnapshot? _state;
    private FitResult _initialFit = null!;

    /// <summary>当前不可变状态快照；Controller 完成规则初始化后才有值。</summary>
    internal DiagnosisSnapshot State => _state
        ?? throw new InvalidOperationException("诊断案件尚未完成规则初始化。");

    /// <summary>写入装配检查与故障基线，并把维修进度复位到初始状态。</summary>
    internal void Initialize(FitResult fit, LoadResult baseline)
    {
        BenchSetup setup = Case.Setup;
        _initialFit = fit;
        _state = new DiagnosisSnapshot(
            setup,
            fit,
            baseline,
            SelectedPsuIndex: Case.FaultyPsuIndex,
            InstalledPsuIndex: Case.FaultyPsuIndex,
            ReplacementAllowance: Case.ReplacementAllowance,
            Retest: null,
            Outcome: DiagnosisOutcome.Pending);
    }

    /// <summary>记录玩家在下拉框中选择的候选电源。</summary>
    internal DiagnosisSnapshot SelectPsu(int index)
    {
        DiagnosisSnapshot next = State with { SelectedPsuIndex = index };
        _state = next;
        return next;
    }

    /// <summary>记录一次替换：安装所选电源、刷新当前装配检查并消耗一次额度。</summary>
    internal DiagnosisSnapshot InstallPsu(int index, FitResult fit)
    {
        DiagnosisSnapshot next = State with
        {
            SelectedPsuIndex = index,
            InstalledPsuIndex = index,
            Fit = fit,
            ReplacementAllowance = State.ReplacementAllowance - 1,
            Outcome = DiagnosisOutcome.AwaitingRetest,
        };
        _state = next;
        return next;
    }

    /// <summary>记录复测结果与结案结论。</summary>
    internal DiagnosisSnapshot CompleteRetest(LoadResult result, DiagnosisOutcome outcome)
    {
        DiagnosisSnapshot next = State with { Retest = result, Outcome = outcome };
        _state = next;
        return next;
    }

    /// <summary>重新开始本案：恢复原装电源的装配检查与故障基线，只清除维修进度。</summary>
    internal DiagnosisSnapshot Reset()
    {
        DiagnosisSnapshot next = State with
        {
            SelectedPsuIndex = Case.FaultyPsuIndex,
            InstalledPsuIndex = Case.FaultyPsuIndex,
            Fit = _initialFit,
            ReplacementAllowance = Case.ReplacementAllowance,
            Retest = null,
            Outcome = DiagnosisOutcome.Pending,
        };
        _state = next;
        return next;
    }
}
