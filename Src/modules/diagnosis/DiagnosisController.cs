using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Simulation;
using KemoCard.Frame.Mvc;

namespace ComputerSimulator.Modules.Diagnosis;

/// <summary>
/// 故障诊断 Controller：独占装配 / 负载规则，裁决选择、替换、复测与重置，
/// 并把每次状态变化作为不可变快照发布到功能内部总线。
/// 题面（案件编号、故障电源、替换额度）全部来自注入内容。
/// </summary>
internal sealed class DiagnosisController : BaseController<DiagnosisMod>, IDisposable
{
    private readonly ClearanceRules _rules;
    private bool _closed;

    /// <summary>
    /// 创建 Controller，安装内容题面并立刻用规则引擎实测本案的装配与原始电源故障基线。
    /// </summary>
    internal DiagnosisController(DiagnosisMod model, GameContent content) : base(model)
    {
        ArgumentNullException.ThrowIfNull(content);
        Model.Case = content.DiagnosisCase
            ?? throw new InvalidOperationException("内容缺少诊断案件定义（content/diagnosis_cases）。");
        _rules = new ClearanceRules(content);
        try
        {
            BenchSetup setup = Case.Setup;
            Model.Initialize(_rules.Inspect(setup), _rules.TestLoad(setup, Case.FaultyPsuIndex));
        }
        catch
        {
            _rules.Dispose();
            throw;
        }
    }

    /// <summary>规则与界面共用的硬件目录。</summary>
    internal BenchCatalog Catalog => _rules.Catalog;
    internal DiagnosisCaseContent Case => Model.Case;

    /// <summary>当前不可变状态快照。</summary>
    internal DiagnosisSnapshot State => Model.State;

    /// <summary>
    /// 计算下拉框中某个候选电源的展示数据。只读评估，不改变案件状态，
    /// 也不提前运行复测规则。
    /// </summary>
    internal DiagnosisCandidate EvaluateCandidate(int index)
    {
        int clamped = Math.Clamp(index, 0, Catalog.PowerSupplies.Length - 1);
        return new DiagnosisCandidate(
            clamped,
            Catalog.PowerSupplies[clamped],
            clamped == Case.FaultyPsuIndex);
    }

    /// <summary>选择待替换电源；仅在仍有替换额度且案件未结案时生效。</summary>
    internal bool SelectPsu(int index)
    {
        if (_closed || !State.CanChoosePsu)
        {
            return false;
        }

        if (index < 0 || index >= Catalog.PowerSupplies.Length || index == State.SelectedPsuIndex)
        {
            return false;
        }

        Publish(Model.SelectPsu(index));
        return true;
    }

    /// <summary>
    /// 把所选电源装入整机，消耗一次替换额度，并按当前已装电源重新实测装配检查。
    /// 新电源装不下时保留在机箱内，复测会被装配检查阻止。
    /// </summary>
    internal bool InstallSelected()
    {
        if (_closed || !State.CanInstall)
        {
            return false;
        }

        int index = State.SelectedPsuIndex;
        if (index < 0 || index >= Catalog.PowerSupplies.Length)
        {
            return false;
        }

        BenchSetup installedSetup = State.Setup with { PsuIndex = index };
        Publish(Model.InstallPsu(index, _rules.Inspect(installedSetup)));
        return true;
    }

    /// <summary>对当前已安装电源运行负载复测，并按规则结果结案。</summary>
    internal bool RunRetest()
    {
        if (_closed || !State.CanRetest)
        {
            return false;
        }

        BenchSetup installedSetup = State.Setup with { PsuIndex = State.InstalledPsuIndex };
        LoadResult result = _rules.TestLoad(installedSetup, State.InstalledPsuIndex);
        DiagnosisOutcome outcome = result.Status switch
        {
            "BLOCKED" => DiagnosisOutcome.Failed,
            "OPTIMAL" => DiagnosisOutcome.RepairedOptimal,
            _ => DiagnosisOutcome.RepairedDegraded,
        };

        Publish(Model.CompleteRetest(result, outcome));
        return true;
    }

    /// <summary>重新开始本案：恢复注入的替换额度、原装电源装配检查并清除复测结果。</summary>
    internal bool ResetCase()
    {
        if (_closed)
        {
            return false;
        }

        Publish(Model.Reset());
        return true;
    }

    /// <summary>
    /// 释放规则运行环境、Model 内部总线与 Controller 自身的订阅。
    /// 可重复调用；由组合根或界面离场时清理。
    /// </summary>
    internal void Close()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        base.Dispose();
        Model.Dispose();
        _rules.Dispose();
    }

    /// <summary>
    /// 让 <see cref="IDisposable"/> 与 <see cref="Close"/> 共享同一条清理路径，
    /// 避免调用方只释放订阅而漏掉规则环境。
    /// </summary>
    public new void Dispose() => Close();

    void IDisposable.Dispose() => Close();

    private void Publish(DiagnosisSnapshot snapshot) =>
        InternalBus.Send(DiagnosisEvents.StateChanged, snapshot);
}
