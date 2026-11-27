using ComputerSimulator.Modules.Simulation;

namespace ComputerSimulator.Modules.Benchmark;

/// <summary>
/// 对照区域当前应呈现的形态。控制器只裁决形态，具体文案与颜色由视图决定。
/// </summary>
public enum BenchmarkComparisonStatus
{
    /// <summary>当前报告不启用对照（神秘箱）。</summary>
    Disabled,

    /// <summary>基准与当前结果同工作负载，可显示增量。</summary>
    Comparable,

    /// <summary>当前结果与基准的工作负载不同，无法对比。</summary>
    WorkloadMismatch,

    /// <summary>当前结果本身就是基准，等待下一次同工作负载测试。</summary>
    PinnedAsBaseline,

    /// <summary>尚无对照，也没有有效当前结果。</summary>
    AwaitingFirstRun,

    /// <summary>尚无对照、没有有效结果，且有需要提示的失效原因。</summary>
    AwaitingFirstRunBlocked,

    /// <summary>尚无对照，但已有一次通过的当前结果可设为对照。</summary>
    AwaitingBaseline,

    /// <summary>对照已保留，等待下一次通过的测试。</summary>
    AwaitingRetest,

    /// <summary>对照已保留、没有有效结果，且有需要提示的失效原因。</summary>
    AwaitingRetestBlocked,

    /// <summary>兜底：保留对照但当前结果不可比较。</summary>
    Uncomparable,
}

/// <summary>
/// 单个分项的对照增量；<see cref="Compared"/> 为 <c>false</c> 表示基准或当前结果缺少该轴。
/// </summary>
public sealed record BenchmarkAxisDelta(bool Compared, float Delta, float BaselineScore, float CurrentScore);

/// <summary>控制器算好的对照增量；视图只负责格式化文案与着色。</summary>
public sealed record BenchmarkDeltas(float Total, int Price, float Noise, BenchmarkAxisDelta[] Axes);

/// <summary>一次对照派生结果：可比较性、按钮可用性与增量数据。</summary>
public sealed record BenchmarkComparison(
    BenchmarkComparisonStatus Status,
    bool CanCompare,
    bool SameRun,
    bool WorkloadMismatch,
    bool CanPin,
    bool CanClear,
    string Notice,
    BenchmarkDeltas? Deltas);

/// <summary>
/// 负载报告的功能状态快照。由 <see cref="BenchmarkController"/> 在每次变更后发布到
/// <see cref="BenchmarkMod.InternalBus"/>，视图据此整体重绘。
/// </summary>
public sealed record BenchmarkViewState(
    BenchmarkReport? Report,
    BenchmarkRunSnapshot? Baseline,
    BenchmarkRunSnapshot? Current,
    bool ComparisonEnabled,
    bool IsStale,
    BenchmarkComparison Comparison);
