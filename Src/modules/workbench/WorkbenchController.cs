using ComputerSimulator.Modules.Benchmark;
using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Simulation;
using KemoCard.Frame.Mvc;

namespace ComputerSimulator.Modules.Workbench;

/// <summary>工作台功能内部总线的事件键。id 在本功能内部唯一。</summary>
internal static class WorkbenchEvents
{
    internal static readonly EventKey<WorkbenchState> StateChanged = new(1);
}

/// <summary>
/// 工作台功能控制器：沙盒与神秘箱的装配检查、负载测试、提交与重开规则均在此执行。
/// 不引用任何 UI 类型；状态通过 <see cref="SubscribeState"/> 发布。
/// </summary>
internal sealed class WorkbenchController : BaseController<WorkbenchMod>
{
    private const string BlockedStatus = "BLOCKED";

    private readonly ClearanceRules _rules;
    private readonly BenchmarkController _benchmark;
    private readonly BenchSetup _sandboxInitial;
    private readonly CrateChallengeContent? _challenge;
    private readonly int[] _crateSlots;
    private readonly WorkbenchTarget? _sandboxTarget;
    private bool _closed;

    public WorkbenchController(
        WorkbenchMod model,
        BenchmarkController benchmark,
        bool crateMode,
        GameContent content) : base(model)
    {
        ArgumentNullException.ThrowIfNull(content);
        _benchmark = benchmark;
        CrateMode = crateMode;
        _rules = new ClearanceRules(content);
        Catalog = _rules.Catalog;
        // 沙盒默认值取自 Catalog.Initial；先复制其数组，避免外部经 Catalog 改动默认选择。
        _sandboxInitial = Catalog.Initial with { Slots = CopySlots(Catalog.Initial.Slots) };

        _challenge = content.CrateChallenge;
        if (crateMode && _challenge is null)
        {
            throw new InvalidOperationException("神秘箱模式缺少 content/challenges 定义。");
        }

        _crateSlots = _challenge is null ? Array.Empty<int>() : CopySlots(_challenge.Fixed.Slots);
        if (crateMode && !IsAllowedCratePart(_challenge!.Initial))
        {
            throw new InvalidOperationException("神秘箱初始配置不在箱内清单中。");
        }

        // 沙盒目标来自注入的初始配置，不假设任何固定容量或通道数。
        if (!crateMode)
        {
            var initialFit = _rules.Inspect(_sandboxInitial);
            _sandboxTarget = new WorkbenchTarget(initialFit.Capacity, initialFit.Channels);
        }

        var setup = crateMode ? CrateSetup() : SandboxSetup(_sandboxInitial.Front360);
        Model.State = CreateState(setup);
    }

    public bool CrateMode { get; }
    public CrateChallengeContent? Challenge => CrateMode ? _challenge : null;

    /// <summary>沙盒默认内存目标；神秘箱与委托模式为 <c>null</c>。</summary>
    public WorkbenchTarget? SandboxTarget => _sandboxTarget;

    public BenchCatalog Catalog { get; }

    /// <summary>当前状态的防御性快照。</summary>
    public WorkbenchState State => Snapshot();

    /// <summary>
    /// 订阅状态更新；释放返回句柄即取消订阅。订阅不会回放当前状态，请先读取 <see cref="State"/>。
    /// 不传 caller 时由控制器负责在 Dispose 时统一退订。
    /// </summary>
    public IDisposable SubscribeState(Action<WorkbenchState> handler, object? caller = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var listener = InternalBus.On(
            WorkbenchEvents.StateChanged,
            (state, _) => handler(state),
            caller ?? this);
        return new StateSubscription(listener);
    }

    /// <summary>重置为当前模式的默认选择；神秘箱提交后不可重置。</summary>
    public WorkbenchState Reset(bool front360)
    {
        if (_closed || (CrateMode && Model.State.Submitted)) return State;

        var setup = CrateMode ? CrateSetup() : SandboxSetup(front360);
        Model.State = CreateState(setup, best: Model.State.Best);
        _benchmark.InvalidateCurrent();
        Publish();
        return Snapshot();
    }

    /// <summary>
    /// 应用新的装配选择。神秘箱会锁定固定件；提交后拒绝任何调整。
    /// 实际配置变化时作废新鲜负载结果，但保留 HasTestResult 供视图显示“待复测”旧报告提示。
    /// </summary>
    public WorkbenchState ChangeSetup(BenchSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        if (_closed || (CrateMode && Model.State.Submitted)) return State;
        if (!TryNormalize(setup, out var next) || SetupEquals(next, Model.State.Setup)) return State;

        Model.State = CreateState(
            next,
            load: null,
            hasTestResult: Model.State.HasTestResult,
            submitted: Model.State.Submitted,
            best: Model.State.Best,
            identity: Model.State.HasTestResult ? Model.State.Identity : null);
        if (Model.State.HasTestResult) _benchmark.MarkStale();
        Publish();
        return Snapshot();
    }

    /// <summary>
    /// 对当前装配运行负载测试。装配阻断、模式不合法或神秘箱已提交时拒绝。
    /// 成功的沙盒测试附带运行身份，神秘箱身份保持为空。
    /// </summary>
    public WorkbenchState RunLoad()
    {
        if (_closed || (CrateMode && Model.State.Submitted)) return State;
        // 负载脚本只接受前置 360 样机；沙盒风冷方案与阻断装配都不执行测试。
        if (!Model.State.Setup.Front360
            || Model.State.Fit.Status == BlockedStatus
            || !IsInMode(Model.State.Setup))
            return State;

        var setup = Model.State.Setup;
        var load = _rules.TestLoad(setup, setup.PsuIndex);
        bool hasReport = load.Report is not null;

        Model.State = Model.State with
        {
            Load = load,
            HasTestResult = hasReport,
            Identity = hasReport && !CrateMode ? CaptureRun(setup) : null,
        };
        _benchmark.ApplyLoad(load, Model.State.Identity);
        Publish();
        return Snapshot();
    }

    /// <summary>
    /// 提交神秘箱当前配置的新鲜报告，并更新本次访问最佳。
    /// 没有新鲜成功报告、非神秘箱或已提交时拒绝。
    /// </summary>
    public WorkbenchState SubmitCrate()
    {
        if (_closed || !CrateMode || Model.State.Submitted) return State;

        var report = Model.State.Load?.Report;
        if (!Model.State.HasTestResult || report is null) return State;
        if (Model.State.Fit.Status == BlockedStatus || !IsInMode(Model.State.Setup)) return State;

        GradedScore total = report.Total;
        GradedScore best = Model.State.Best is { } previous && previous.Score >= total.Score
            ? previous
            : total;

        Model.State = Model.State with { Submitted = true, Best = best };
        Publish();
        return Snapshot();
    }

    /// <summary>神秘箱提交后重开：保留访问最佳，清空当前测试并回到默认选择。</summary>
    public WorkbenchState ReplayCrate()
    {
        if (_closed || !CrateMode || !Model.State.Submitted) return State;

        Model.State = CreateState(CrateSetup(), best: Model.State.Best);
        _benchmark.InvalidateCurrent();
        Publish();
        return Snapshot();
    }

    /// <summary>
    /// 释放规则脚本的 V8 环境；此后本控制器不再执行装配检查或负载测试。
    /// 同时清理功能总线，所有订阅随会话结束。
    /// 本方法不写入任何神秘箱数据，最佳成绩只保留在当前控制器实例内。
    /// </summary>
    public void Close()
    {
        if (_closed) return;
        _closed = true;
        Dispose();
        Model.Dispose();
        _rules.Dispose();
    }

    private WorkbenchState CreateState(
        BenchSetup setup,
        LoadResult? load = null,
        bool hasTestResult = false,
        bool submitted = false,
        GradedScore? best = null,
        BenchmarkRunIdentity? identity = null)
        => new(setup, _rules.Inspect(setup), load, hasTestResult, submitted, best, identity);

    private BenchSetup CrateSetup()
        => _challenge is null
            ? throw new InvalidOperationException("神秘箱模式缺少 content/challenges 定义。")
            : _challenge.Initial with { Slots = CopySlots(_challenge.Initial.Slots) };

    private BenchSetup SandboxSetup(bool front360)
    {
        var initial = _sandboxInitial;
        return new BenchSetup(
            initial.CaseIndex,
            initial.RamIndex,
            CopySlots(initial.Slots),
            initial.Offset,
            front360,
            initial.GpuIndex,
            initial.AirflowIndex,
            initial.WorkloadIndex,
            initial.PsuIndex);
    }

    private bool TryNormalize(BenchSetup input, out BenchSetup setup)
    {
        setup = input;

        if (!InRange(input.CaseIndex, Catalog.Cases.Length)
            || !InRange(input.GpuIndex, Catalog.Graphics.Length)
            || !InRange(input.PsuIndex, Catalog.PowerSupplies.Length))
            return false;

        if (CrateMode)
        {
            // 神秘箱固定件：挑战定义中的内存 / 插槽 / 偏移 / 前置冷排 / 风道 / 工况，
            // 玩家只能在箱内清单（机箱、显卡、电源）里选择；越界选择在控制器层拒绝。
            if (!IsAllowedCratePart(input))
                return false;

            var fixedSetup = _challenge!.Fixed;
            setup = new BenchSetup(
                input.CaseIndex,
                fixedSetup.RamIndex,
                CopySlots(_crateSlots),
                fixedSetup.Offset,
                fixedSetup.Front360,
                input.GpuIndex,
                fixedSetup.AirflowIndex,
                fixedSetup.WorkloadIndex,
                input.PsuIndex);
            return true;
        }

        if (input.Slots is null
            || !InRange(input.RamIndex, Catalog.Ram.Length)
            || !InRange(input.AirflowIndex, Catalog.Benchmark.Airflows.Length)
            || !InRange(input.WorkloadIndex, Catalog.Benchmark.Workloads.Length)
            || input.Offset < 0
            || input.Offset > Catalog.Cooler.Adjustable_y_max
            || !SlotsValid(input.Slots))
            return false;

        setup = new BenchSetup(
            input.CaseIndex,
            input.RamIndex,
            [.. input.Slots.Order()],
            input.Offset,
            input.Front360,
            input.GpuIndex,
            input.AirflowIndex,
            input.WorkloadIndex,
            input.PsuIndex);
        return true;
    }

    private bool SlotsValid(int[] slots)
    {
        var seen = new HashSet<int>();
        foreach (int index in slots)
        {
            if (!InRange(index, Catalog.Board.Slots.Length) || !seen.Add(index))
                return false;
        }
        return true;
    }

    private bool IsInMode(BenchSetup setup)
    {
        if (!CrateMode) return true;

        var fixedSetup = _challenge!.Fixed;
        return IsAllowedCratePart(setup)
            && setup.Front360 == fixedSetup.Front360
            && setup.RamIndex == fixedSetup.RamIndex
            && setup.Offset == fixedSetup.Offset
            && setup.AirflowIndex == fixedSetup.AirflowIndex
            && setup.WorkloadIndex == fixedSetup.WorkloadIndex
            && setup.Slots.AsSpan().SequenceEqual(_crateSlots);
    }

    /// <summary>机箱、显卡、电源都必须来自挑战定义的箱内清单。</summary>
    private bool IsAllowedCratePart(BenchSetup setup)
        => _challenge is { } challenge
            && Array.IndexOf(challenge.CaseIndices, setup.CaseIndex) >= 0
            && Array.IndexOf(challenge.GpuIndices, setup.GpuIndex) >= 0
            && Array.IndexOf(challenge.PsuIndices, setup.PsuIndex) >= 0;

    private BenchmarkRunIdentity CaptureRun(BenchSetup setup)
    {
        var slots = setup.Slots
            .Where(index => InRange(index, Catalog.Board.Slots.Length))
            .OrderBy(index => index)
            .Select(index => Catalog.Board.Slots[index].Name);
        return new BenchmarkRunIdentity(
            Catalog.Cases[setup.CaseIndex].Name,
            Catalog.Graphics[setup.GpuIndex].Name,
            Catalog.PowerSupplies[setup.PsuIndex].Name,
            Catalog.Ram[setup.RamIndex].Name,
            string.Join(" + ", slots));
    }

    private static bool SetupEquals(BenchSetup a, BenchSetup b)
        => a.CaseIndex == b.CaseIndex
            && a.RamIndex == b.RamIndex
            && a.Offset == b.Offset
            && a.Front360 == b.Front360
            && a.GpuIndex == b.GpuIndex
            && a.AirflowIndex == b.AirflowIndex
            && a.WorkloadIndex == b.WorkloadIndex
            && a.PsuIndex == b.PsuIndex
            && a.Slots.AsSpan().SequenceEqual(b.Slots);

    private static bool InRange(int index, int length) => index >= 0 && index < length;

    private static int[] CopySlots(int[] slots) => [.. slots];

    private WorkbenchState Snapshot()
        => Model.State with { Setup = Model.State.Setup with { Slots = CopySlots(Model.State.Setup.Slots) } };

    private void Publish() => InternalBus.Send(WorkbenchEvents.StateChanged, Snapshot());

    private sealed class StateSubscription(IEventListener<WorkbenchState> listener) : IDisposable
    {
        public void Dispose() => listener.Off();
    }
}
