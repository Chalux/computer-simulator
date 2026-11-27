using ComputerSimulator.Modules.Simulation;
using KemoCard.Frame.Mvc;

namespace ComputerSimulator.Modules.Benchmark;

/// <summary>
/// 负载报告功能的 Controller：裁决报告的展示、失效、对照固定与清除，
/// 计算视图所需的派生对照结果，并把每次变化作为不可变快照发布到功能内部总线。
/// 不引用任何 Godot 类型；文案与颜色留在视图。
/// </summary>
internal sealed class BenchmarkController : BaseController<BenchmarkMod>
{
    internal BenchmarkController(BenchmarkMod model) : base(model)
    {
    }

    /// <summary>当前状态快照。</summary>
    internal BenchmarkViewState State => CreateState();

    /// <summary>打开或关闭对照。关闭只影响让出对照界面，已保存的基准与当前结果保留。</summary>
    internal void SetComparisonEnabled(bool enabled)
    {
        if (Model.ComparisonEnabled == enabled)
        {
            return;
        }

        Model.SetComparisonEnabled(enabled);
        Publish();
    }

    /// <summary>展示一次成功测试的报告；对照启用时登记运行身份用于后续比较。</summary>
    internal void Display(BenchmarkReport report, BenchmarkRunIdentity? identity = null)
    {
        Model.ApplyResult(report, identity, Model.ComparisonEnabled);
        Publish();
    }

    /// <summary>
    /// 应用一次负载测试结果：通过则展示报告，未通过则作废当前结果并保留对照。
    /// 供工作台 / 委托控制器在规则运行后直接转发。
    /// </summary>
    internal void ApplyLoad(LoadResult load, BenchmarkRunIdentity? identity)
    {
        if (load.Report is { } report)
        {
            Display(report, identity);
            return;
        }

        InvalidateCurrent("本次测试未通过，复测通过后可继续。");
    }

    /// <summary>无运行身份的负载结果（委托模式不参与沙盒对照）。</summary>
    internal void ApplyLoad(LoadResult load) => ApplyLoad(load, identity: null);

    /// <summary>配置变更：报告标记为过期；对照启用时作废当前结果，已保存的对照仍在。</summary>
    internal void MarkStale()
    {
        Model.MarkStale();
        if (Model.ComparisonEnabled)
        {
            Model.InvalidateCurrent("配置已变更，复测通过后可继续。");
        }

        Publish();
    }

    /// <summary>测试失败或还原配置：作废当前结果，已保存的对照仍在。未启用对照时保持原状。</summary>
    internal void InvalidateCurrent(string? notice = null)
    {
        if (!Model.ComparisonEnabled)
        {
            return;
        }

        Model.InvalidateCurrent(notice ?? string.Empty);
        Publish();
    }

    /// <summary>把当前有效结果设为对照；没有新鲜当前结果时拒绝。</summary>
    internal void PinComparison()
    {
        if (!Model.CurrentFresh || Model.Current is null)
        {
            return;
        }

        Model.PinCurrentAsBaseline();
        Publish();
    }

    /// <summary>清除本次会话保存的对照；没有对照时拒绝。</summary>
    internal void ClearComparison()
    {
        if (Model.Baseline is null)
        {
            return;
        }

        Model.ClearBaseline();
        Publish();
    }

    /// <summary>释放 Model 的事件总线与 Controller 自身订阅；由持有方在离场时调用。</summary>
    internal void Close()
    {
        Dispose();
        Model.Dispose();
    }

    private BenchmarkViewState CreateState()
        => new(
            Model.Report,
            Model.Baseline,
            Model.Current,
            Model.ComparisonEnabled,
            Model.IsStale,
            CreateComparison());

    private BenchmarkComparison CreateComparison()
    {
        if (!Model.ComparisonEnabled)
        {
            return new BenchmarkComparison(
                BenchmarkComparisonStatus.Disabled,
                CanCompare: false,
                SameRun: false,
                WorkloadMismatch: false,
                CanPin: false,
                CanClear: false,
                Notice: string.Empty,
                Deltas: null);
        }

        BenchmarkRunSnapshot? baseline = Model.Baseline;
        BenchmarkRunSnapshot? current = Model.Current;

        bool sameRun = baseline is not null && current is not null && ReferenceEquals(baseline, current);
        bool workloadMatches = baseline is not null && current is not null &&
            string.Equals(baseline.Report.Workload, current.Report.Workload, StringComparison.Ordinal);
        bool canCompare = Model.CurrentFresh && !sameRun && workloadMatches;
        bool workloadMismatch = Model.CurrentFresh && !sameRun &&
            baseline is not null && current is not null && !workloadMatches;

        BenchmarkComparisonStatus status;
        if (canCompare)
        {
            status = BenchmarkComparisonStatus.Comparable;
        }
        else if (workloadMismatch)
        {
            status = BenchmarkComparisonStatus.WorkloadMismatch;
        }
        else if (sameRun)
        {
            status = BenchmarkComparisonStatus.PinnedAsBaseline;
        }
        else if (baseline is null && current is null)
        {
            status = Model.Notice.Length > 0
                ? BenchmarkComparisonStatus.AwaitingFirstRunBlocked
                : BenchmarkComparisonStatus.AwaitingFirstRun;
        }
        else if (baseline is null)
        {
            status = BenchmarkComparisonStatus.AwaitingBaseline;
        }
        else if (current is null)
        {
            status = Model.Notice.Length > 0
                ? BenchmarkComparisonStatus.AwaitingRetestBlocked
                : BenchmarkComparisonStatus.AwaitingRetest;
        }
        else
        {
            status = BenchmarkComparisonStatus.Uncomparable;
        }

        return new BenchmarkComparison(
            status,
            canCompare,
            sameRun,
            workloadMismatch,
            CanPin: Model.CurrentFresh && current is not null,
            CanClear: baseline is not null,
            Model.Notice,
            canCompare ? CreateDeltas(baseline!, current!) : null);
    }

    private static BenchmarkDeltas CreateDeltas(BenchmarkRunSnapshot baseline, BenchmarkRunSnapshot current)
    {
        BenchmarkReport before = baseline.Report;
        BenchmarkReport after = current.Report;

        int axisCount = Math.Max(before.Axes.Length, after.Axes.Length);
        var axes = new BenchmarkAxisDelta[axisCount];
        for (int i = 0; i < axisCount; i++)
        {
            if (i >= before.Axes.Length || i >= after.Axes.Length)
            {
                axes[i] = new BenchmarkAxisDelta(Compared: false, Delta: 0, BaselineScore: 0, CurrentScore: 0);
                continue;
            }

            axes[i] = new BenchmarkAxisDelta(
                Compared: true,
                Delta: after.Axes[i].Score - before.Axes[i].Score,
                BaselineScore: before.Axes[i].Score,
                CurrentScore: after.Axes[i].Score);
        }

        return new BenchmarkDeltas(
            after.Total.Score - before.Total.Score,
            after.Price - before.Price,
            after.Noise - before.Noise,
            axes);
    }

    private void Publish() => InternalBus.Send(BenchmarkEvents.StateChanged, CreateState());
}
